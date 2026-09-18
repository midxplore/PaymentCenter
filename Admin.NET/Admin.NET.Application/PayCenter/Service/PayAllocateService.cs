// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 收款匹配服务（F2）★核心
/// </summary>
/// <remarks>
/// <para>
/// 并发安全策略（设计文档 §5.1）：<b>PostgreSQL 原生单语句原子匹配</b> ——
/// 用 <c>UPDATE ... WHERE Id = (SELECT ... FOR UPDATE SKIP LOCKED LIMIT 1)</c>
/// 把「挑出最佳适配账号」与「抢占其额度」合成一条语句，详见 <see cref="TryLockQuotaAsync"/>。
/// </para>
/// <para>
/// 关键顺序约束：步骤 2 的额度预占<b>在事务外</b>执行（锁定即生效）；
/// 步骤 3 落订单若失败，<b>必须补偿释放</b>（<see cref="ReleaseQuotaAsync"/>），否则额度会泄漏。
/// </para>
/// <para>鉴权说明（F6）：本服务属对外接口族（§7），接口挂骨架内置的签名鉴权
/// （<c>AuthenticationSchemes = Signature</c>）+ <c>scope=allocate</c> 权限范围校验。</para>
/// </remarks>
[ApiDescriptionSettings(Name = "pay", Order = 401, Description = "收款匹配")]
public class PayAllocateService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly PayAccountService _payAccountService;
    private readonly PayOrderNoGenerator _payOrderNoGenerator;
    private readonly PayAuditService _payAuditService;
    private readonly SysConfigService _sysConfigService;
    private readonly ISqlSugarClient _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PayAllocateService(SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        PayAccountService payAccountService,
        PayOrderNoGenerator payOrderNoGenerator,
        PayAuditService payAuditService,
        SysConfigService sysConfigService,
        ISqlSugarClient db,
        IHttpContextAccessor httpContextAccessor)
    {
        _payAccountRep = payAccountRep;
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payAccountService = payAccountService;
        _payOrderNoGenerator = payOrderNoGenerator;
        _payAuditService = payAuditService;
        _sysConfigService = sysConfigService;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 查询匹配收款账号（F2）
    /// </summary>
    /// <remarks>
    /// 流程见设计文档 §5.1：幂等前置检查 → 循环 CAS 预占额度 → 事务内落订单与事件流水。
    /// 无可用账号时返回 P1001 且<b>不生成订单</b>（F2.5）。
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Allocate"), HttpPost]
    [DisplayName("查询匹配收款账号")]
    [Authorize(AuthenticationSchemes = SignatureAuthenticationDefaults.AuthenticationScheme)]
    [PayScope(PayConst.ScopeAllocate)]
    public async Task<AllocateOutput> Allocate(AllocateInput input)
    {
        var type = input.Type?.Trim();
        if (string.IsNullOrWhiteSpace(type)) throw Oops.Oh(ErrorCodeEnum.P1001);
        if (input.Amount <= 0) throw Oops.Oh(ErrorCodeEnum.P1003);

        var externalNo = string.IsNullOrWhiteSpace(input.ExternalNo) ? null : input.ExternalNo.Trim();
        var clientId = await ResolveRequiredClientIdAsync();

        // ── 步骤 1：幂等前置检查（F2.6）────────────────────────────────
        // ★ 幂等键是 (ClientId, ExternalNo) 组合，**必须带 ClientId**。
        //   只按 ExternalNo 查会让不同接入方之间互相串单：
        //   B 用了与 A 相同的业务单号时，会直接命中 A 的订单并把它（含收款账号明文）返回给 B，
        //   而 B 自己的收款请求根本没被分配 —— 钱可能进错账户，双方都收不到任何报错。
        //   详见 PayOrder.ExternalNo 的说明。
        if (externalNo != null)
        {
            var existing = await FindByIdempotencyKeyAsync(clientId, externalNo);
            if (existing != null)
            {
                // 幂等命中也要留痕：它是「接入方在重试」的直接证据，
                // 出现异常高频时可以据此定位「接入方把幂等接口当轮询用了」。
                await _payAuditService.WriteOpenApiAsync(PayAuditActionEnum.ApiCall,
                    PayConst.AuditTargetTypeOrder, existing.Id, existing.OrderNo,
                    $"查询匹配·幂等命中：外部单号 {externalNo} 已存在订单（金额 {existing.RequestAmount:0.00}），本次未重新匹配、未占用额度");

                return await BuildOutputAsync(existing, type, true);
            }
        }

        // ── 步骤 2：单语句原子匹配 + 预占（事务外）─────────────────────
        var lockedAccountId = await TryLockQuotaAsync(type, input.Amount);
        // 步骤 4：无可用候选 → 返回无可用账号，不生成订单（F2.5）
        if (lockedAccountId == null) throw Oops.Oh(ErrorCodeEnum.P1001);

        // ── 步骤 3：事务内生成订单 + 事件流水 ─────────────────────────
        PayOrder order;
        try
        {
            order = await CreateOrderAsync(type, input.Amount, externalNo, lockedAccountId.Value, clientId);
        }
        catch (Exception)
        {
            // 补偿释放步骤 2 的预占，避免额度泄漏
            await ReleaseQuotaAsync(lockedAccountId.Value, input.Amount);

            // (ClientId, ExternalNo) 唯一索引冲突（并发提交同一调用方的同一外部单号）
            // → 回退为返回已存在的那笔订单
            if (externalNo != null)
            {
                var existing = await FindByIdempotencyKeyAsync(clientId, externalNo);
                if (existing != null) return await BuildOutputAsync(existing, type, true);
            }
            throw;
        }

        return await BuildOutputAsync(order, type, false);
    }

    /// <summary>
    /// 按幂等键 <c>(ClientId, ExternalNo)</c> 查已存在的订单（F2.6）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 抽成独立方法是为了让「幂等查询必须带 ClientId」这条规则**只有一处实现** ——
    /// 原先它散落在步骤 1 与 catch 回退两个分支里，任何一处漏写 ClientId 都会重新打开跨调用方串单的缺口。
    /// </para>
    /// <para>
    /// <paramref name="clientId"/> 为 null 时只匹配 <c>ClientId IS NULL</c> 的订单
    /// （SqlSugar 会翻译成 <c>clientid IS NULL</c>），语义与唯一索引一致：
    /// PostgreSQL 组合唯一索引中 NULL 之间互不冲突，所以无调用方标识的订单不会被彼此命中。
    /// </para>
    /// </remarks>
    /// <param name="clientId">调用方Id</param>
    /// <param name="externalNo">外部业务单号</param>
    /// <returns>已存在的订单；不存在返回 null</returns>
    [NonAction]
    public async Task<PayOrder> FindByIdempotencyKeyAsync(long? clientId, string externalNo)
    {
        return await _payOrderRep.AsQueryable()
            .Where(u => u.ExternalNo == externalNo && u.ClientId == clientId)
            .FirstAsync();
    }

    /// <summary>
    /// 查询收款订单状态（§7.3）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>归属校验</b>：只返回**本调用方自己创建**的订单。
    /// 签名鉴权只回答「你是谁」，不回答「这条数据是不是你的」——
    /// 若只按 <c>OrderNo</c> 查，任何持有效密钥的接入方都能读到别人的订单
    /// （返回体含对方的 <c>ExternalNo</c> 与累计到账金额）。
    /// </para>
    /// <para>
    /// 「不存在」与「不是你的」统一返回 <see cref="ErrorCodeEnum.P1004"/>：
    /// 若对后者返回一个不同的错误，等于提供了一个「订单号是否存在」的探测接口。
    /// </para>
    /// </remarks>
    /// <param name="orderNo">系统订单号</param>
    /// <returns></returns>
    [DisplayName("查询收款订单状态")]
    [Authorize(AuthenticationSchemes = SignatureAuthenticationDefaults.AuthenticationScheme)]
    [PayScope(PayConst.ScopeAllocate)]
    public async Task<OrderQueryOutput> GetStatus([FromQuery] string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo)) throw Oops.Oh(ErrorCodeEnum.P1004);

        var clientId = await ResolveRequiredClientIdAsync();

        var order = await _payOrderRep.AsQueryable()
            .Where(u => u.OrderNo == orderNo.Trim() && u.ClientId == clientId)
            .FirstAsync() ?? throw Oops.Oh(ErrorCodeEnum.P1004);

        return new OrderQueryOutput
        {
            OrderNo = order.OrderNo,
            ExternalNo = order.ExternalNo,
            Status = order.Status,
            StatusText = order.Status.GetDescription(),
            RequestAmount = order.RequestAmount,
            ReceivedAmount = order.ReceivedAmount,
            ExpireTime = order.ExpireTime,
            CompleteTime = order.CompleteTime
        };
    }

    // ─────────────────────────── 内部实现 ───────────────────────────

    /// <summary>
    /// 匹配候选账号并原子预占额度（设计文档 §5.1 步骤 2）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PostgreSQL 原生单语句原子匹配</b>：把「挑出最佳适配账号」与「抢占其额度」合并为一条
    /// <c>UPDATE ... WHERE Id = (SELECT ... FOR UPDATE LIMIT 1)</c>。
    /// </para>
    /// <para>
    /// 为什么不用「先 SELECT 候选、再逐个 CAS 条件更新」：那种写法在 SELECT 与 UPDATE 之间存在时间窗口，
    /// 并发请求会挑中同一行、抢输后重试，于是
    /// ①「最佳适配」从硬保证退化为尽力而为（最终落到的可能不是最优账号）；
    /// ② 候选批次耗尽时会误报 P1001「无可用收款账号」，即使池中仍有额度。
    /// 单语句原子匹配把「选行」和「加锁」放进同一条语句，窗口与重试都不存在了。
    /// </para>
    /// <para>
    /// ★ <b>必须是阻塞式 <c>FOR UPDATE</c>，不能用 <c>FOR UPDATE SKIP LOCKED</c></b>——
    /// 后者只在「同一 Type 下有多个候选账号」时才成立；一旦该 Type 只有<b>一个</b>可用账号
    /// （商户单账号是常态），并发请求会把这个唯一候选跳过，子查询返回空集，
    /// 调用方收到<b>虚假的</b> <c>P1001「无可用收款账号」</c>，而额度其实还有。
    /// 实测：单账号、额度 1000（容量 33 笔）、10 并发 → 只成功 2 笔。
    /// 详见 <see cref="TryLockQuotaAsync"/> 的备注。
    /// </para>
    /// <para>
    /// 候选排序 <c>ORDER BY 剩余 ASC, CreateTime ASC</c>：在「剩余 ≥ 请求金额」的约束下，
    /// 最小化剩余 = 最小化 |剩余 − 金额|，即最佳适配（F2.2）；剩余相同时按创建时间，保证规则确定可复现。
    /// </para>
    /// <para>
    /// <b>状态置位同语句完成</b>（F1.4）：预占后剩余恰好归零时，<c>CASE</c> 分支把 <c>Status</c> 一并置为
    /// 「已用完」。这样「启用 ⇒ 剩余 &gt; 0」是不变量，而不是调用方需要记得维护的约定。
    /// </para>
    /// <para>语句在事务外执行（锁定即生效，与订单落库解耦）；订单落库失败时由调用方补偿释放。</para>
    /// </remarks>
    /// <param name="type">收款类型</param>
    /// <param name="amount">请求金额</param>
    /// <returns>锁定成功的账号Id；无可用账号返回 null</returns>
    [NonAction]
    public async Task<long?> TryLockQuotaAsync(string type, decimal amount)
    {
        // ⚠️ 四条必须保留的细节，改动前务必知悉：
        //   1) 标识符一律不加引号。PG 建表时 SqlSugar 未加引号，列名已被折叠为小写；
        //      写成 "LockedQuota" 会按原样大小写去找列，直接报列不存在。
        //   2) 必须手写 IsDelete = false。原生 SQL 不经过 SqlSugar 的全局假删除过滤器。
        //   3) 外层 WHERE 再判一次额度。子查询选中行与 UPDATE 取到行锁之间，
        //      该行可能已被并发事务改动；PG 在外层条件不满足时会放弃更新（影响 0 行），
        //      这正是我们要的「抢不到就不占」语义。
        //      ★ 用阻塞式 FOR UPDATE 时这一条尤其关键：等到锁之后必须**重新求值**额度，
        //        不能沿用子查询当时的判断，否则会把并发已扣减的额度重复预占（超发）。
        //   4) 状态置位与额度扣减必须写在同一条语句里（见下 CASE）。
        //      若拆成「先锁定、再由调用方补一次状态同步」，两者之间就存在窗口：
        //      该账号在窗口内仍为「启用」且剩余为 0，会破坏
        //      「启用 ⇒ 剩余额度 > 0」这条不变量（F1.4 的自动下架语义也随之失效）。
        //   5) ★ 用 FOR UPDATE，**不要**用 FOR UPDATE SKIP LOCKED。
        //      SKIP LOCKED 会「跳过别人锁住的行去选下一个候选」，但本查询是 LIMIT 1
        //      的最佳适配——同 Type 下只有一个可用账号时（常态），
        //      并发请求会把唯一候选跳过 → 子查询空集 → 误报 P1001「无可用收款账号」，
        //      而额度其实还有，等于丢单。阻塞式 FOR UPDATE 让后续请求排队，
        //      等前一条语句提交后在外层 WHERE 上重新求值，结果才正确。
        //      等待时间有界：本方法在事务外执行，锁只持有这一条语句的执行时间。
        const string sql = @"
UPDATE pay_account
   SET LockedQuota = LockedQuota + @amount,
       Status      = CASE
                       WHEN TotalQuota <= UsedQuota + LockedQuota + @amount THEN @exhausted
                       ELSE Status
                     END,
       UpdateTime  = @now
 WHERE Id = (
        SELECT Id
          FROM pay_account
         WHERE Type = @type
           AND Status = @enabled
           AND IsDelete = false
           AND TotalQuota >= UsedQuota + LockedQuota + @amount
         ORDER BY (TotalQuota - UsedQuota - LockedQuota) ASC, CreateTime ASC
         LIMIT 1
           FOR UPDATE
       )
   AND Status = @enabled
   AND IsDelete = false
   AND TotalQuota >= UsedQuota + LockedQuota + @amount
RETURNING Id;";

        var ids = await _db.Ado.SqlQueryAsync<long>(sql,
            new SugarParameter("@amount", amount),
            new SugarParameter("@now", DateTime.Now),
            new SugarParameter("@type", type),
            new SugarParameter("@enabled", (int)PayAccountStatusEnum.Enabled),
            new SugarParameter("@exhausted", (int)PayAccountStatusEnum.Exhausted));

        return ids.Count > 0 ? ids[0] : null;
    }

    /// <summary>
    /// 释放已预占的额度（步骤 3 失败时的补偿，或订单过期时的回收）
    /// </summary>
    /// <param name="accountId">账号Id</param>
    /// <param name="amount">释放金额</param>
    /// <returns></returns>
    [NonAction]
    public async Task ReleaseQuotaAsync(long accountId, decimal amount)
    {
        // 带下界守卫，避免任何异常路径把 LockedQuota 减成负数。
        await _payAccountRep.AsUpdateable()
            .SetColumns(u => new PayAccount
            {
                LockedQuota = u.LockedQuota - amount,
                UpdateTime = DateTime.Now
            })
            .Where(u => u.Id == accountId && u.LockedQuota >= amount)
            .ExecuteCommandAsync();

        // 额度恢复后可能重新具备可匹配性，同步状态（F1.4）
        await _payAccountService.SyncStatusByQuotaAsync(accountId);
    }

    /// <summary>
    /// 事务内生成订单与「创建」事件流水（设计文档 §5.1 步骤 3）
    /// </summary>
    /// <param name="type">收款类型</param>
    /// <param name="amount">请求金额</param>
    /// <param name="externalNo">外部业务单号（可为空）</param>
    /// <param name="accountId">已锁定额度的账号Id</param>
    /// <param name="clientId">调用方Id（幂等键与审计用）</param>
    /// <returns></returns>
    [NonAction]
    public async Task<PayOrder> CreateOrderAsync(string type, decimal amount, string externalNo, long accountId, long? clientId)
    {
        var expireMinutes = await _sysConfigService.GetConfigValueByCode<int>(PayConst.OrderExpireMinutes);
        if (expireMinutes <= 0) expireMinutes = PayConst.DefaultOrderExpireMinutes;

        var now = DateTime.Now;
        // 订单号在事务外生成：PayOrderNoGenerator 是纯内存的「时间戳 + 雪花尾段」，不碰数据库，
        // 嵌套进外层事务只会白白拉长事务持有时间。
        var orderNo = _payOrderNoGenerator.Next();

        var order = new PayOrder
        {
            OrderNo = orderNo,
            ExternalNo = externalNo,
            RequestAmount = amount,
            AccountId = accountId,
            ReceivedAmount = 0m,
            Status = PayOrderStatusEnum.Pending,
            ExpireTime = now.AddMinutes(expireMinutes),
            ClientId = clientId
        };

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            // 3.2 订单（(ClientId, ExternalNo) 唯一索引在此处拦截并发同单号）
            await _payOrderRep.InsertAsync(order);

            // 3.3 事件流水（只增不改）
            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                EventType = PayEventTypeEnum.Created,
                FromStatus = null,
                ToStatus = PayOrderStatusEnum.Pending,
                Amount = amount,
                ReceivedTotal = 0m,
                // 审计流水只落 AccountId，不落账号明文（设计决策 #1 / §8.3）
                Remark = $"匹配到收款账号Id {accountId}，预占额度 {amount:0.00}",
                OperatorId = null,
                OperatorName = "系统"
            });

            // 3.4 调用审计（F7.3）：与订单**同事务**提交。
            //     资金变动与「谁发起的」必须同生共死 —— 不能出现「订单落了库但审计没落」的缺口，
            //     否则事后无法回答「这笔钱是哪个接入方发起的」。
            //     因此这里传 db: _db 复用同一事务，而不是提交后再补一条。
            await _payAuditService.WriteOpenApiAsync(PayAuditActionEnum.ApiCall,
                PayConst.AuditTargetTypeOrder, order.Id, order.OrderNo,
                $"查询匹配：类型 {type}，金额 {amount:0.00}，分配收款账号Id {accountId}，预占额度 {amount:0.00}",
                _db);

            // F1.4 的「额度耗尽自动下架」已由步骤 2 的原子语句一并完成（CASE 分支置 Exhausted），
            // 此处不再补一次状态同步：那会多一次往返，也会重新引入「锁定」与「置位」之间的窗口。

            // 3.5 提交
            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }

        return order;
    }

    /// <summary>
    /// 获取订单号（时间戳 + 雪花尾段，自包含）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 委托给 <see cref="PayOrderNoGenerator"/>：纯内存生成，<b>不碰数据库</b>，
    /// 不依赖 <c>sysserial</c> 表与后台流水号配置（F2.3 方案 B）。
    /// </para>
    /// <para>
    /// 历史写法均已废弃，且原因不同：
    /// <list type="bullet">
    /// <item><c>SysSerialService.NextSeqNo</c> 逐号取：每次取号抢一把全局分布式锁，
    /// 30 并发起就会因锁等待超时直接失败（可用性缺陷，不是性能问题）；</item>
    /// <item>号段批量预取（Leaf-segment）：解决了并发，但仍在 <c>sysserial</c> 上挂着
    /// 一份配置与租户上下文依赖，不利于本模块整体拆分出去独立部署。</item>
    /// </list>
    /// 雪花号在进程内生成，取号路径上既没有锁竞争，也没有外部依赖。
    /// </para>
    /// </remarks>
    /// <returns></returns>
    [NonAction]
    public string AcquireOrderNo() => _payOrderNoGenerator.Next();

    /// <summary>
    /// 解析调用方标识（<c>SysOpenAccess.Id</c>），用于幂等键隔离、订单归属与审计
    /// </summary>
    /// <remarks>
    /// 身份来源与口径统一在 <see cref="PayCallerContext"/>，本方法只是它的实例化入口。
    /// </remarks>
    /// <returns></returns>
    [NonAction]
    public Task<long?> ResolveClientIdAsync()
        => Task.FromResult(PayCallerContext.ClientId(_httpContextAccessor.HttpContext));

    /// <summary>
    /// 解析调用方标识，解析不到即抛错（用于幂等键与订单归属）
    /// </summary>
    /// <remarks>
    /// 为什么不能退化成 null：见 <see cref="PayCallerContext.RequireClientId"/>。
    /// </remarks>
    /// <returns></returns>
    [NonAction]
    public Task<long> ResolveRequiredClientIdAsync()
        => Task.FromResult(PayCallerContext.RequireClientId(_httpContextAccessor.HttpContext));

    /// <summary>
    /// 组装对外返回（含账号明文，见设计决策 #1：不脱敏）
    /// </summary>
    /// <param name="order">订单</param>
    /// <param name="type">请求的收款类型（账号已被删除时兜底）</param>
    /// <param name="idempotentHit">是否命中幂等键</param>
    /// <returns></returns>
    [NonAction]
    public async Task<AllocateOutput> BuildOutputAsync(PayOrder order, string type, bool idempotentHit)
    {
        var account = await _payAccountRep.GetByIdAsync(order.AccountId);
        return new AllocateOutput
        {
            OrderNo = order.OrderNo,
            Type = account?.Type ?? type,
            AccountInfo = account?.AccountInfo,
            RequestAmount = order.RequestAmount,
            ExpireTime = order.ExpireTime,
            IdempotentHit = idempotentHit
        };
    }
}
