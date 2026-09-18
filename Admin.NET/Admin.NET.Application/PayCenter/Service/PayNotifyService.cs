// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 到账通知服务（F4）★核心
/// </summary>
/// <remarks>
/// <para>流程见设计文档 §5.2：凭证号去重 → 查订单 → 终态判断 → 累加到账（CAS）→ 判定新状态 → 事件流水。</para>
/// <para>
/// 并发安全要点：<b>所有状态变更都带条件守卫</b>，保证与过期任务（F3.2）、其他到账通知并发时不出错。
/// </para>
/// <para>鉴权说明（F6）：本服务属对外接口族（§7.2），接口挂骨架内置的签名鉴权
/// （<c>AuthenticationSchemes = Signature</c>）+ <c>scope=notify</c> 权限范围校验。</para>
/// </remarks>
[ApiDescriptionSettings(Name = "pay", Order = 402, Description = "到账通知")]
public class PayNotifyService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayNotifyRecord> _payNotifyRecordRep;
    private readonly SqlSugarRepository<PayAbnormalReceipt> _payAbnormalReceiptRep;
    private readonly PayAccountService _payAccountService;
    private readonly PayAuditService _payAuditService;
    private readonly ISqlSugarClient _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PayNotifyService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        SqlSugarRepository<PayAbnormalReceipt> payAbnormalReceiptRep,
        PayAccountService payAccountService,
        PayAuditService payAuditService,
        ISqlSugarClient db,
        IHttpContextAccessor httpContextAccessor)
    {
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payAccountRep = payAccountRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payAbnormalReceiptRep = payAbnormalReceiptRep;
        _payAccountService = payAccountService;
        _payAuditService = payAuditService;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 到账通知（F4，§7.2）
    /// </summary>
    /// <remarks>
    /// 单事务内完成。去重、累加、状态流转、额度结转、事件流水要么全成、要么全不成，
    /// 避免出现「凭证号已占位但金额没累加」这种导致资金永久丢失的中间态。
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Notify"), HttpPost]
    [DisplayName("到账通知")]
    [Authorize(AuthenticationSchemes = SignatureAuthenticationDefaults.AuthenticationScheme)]
    [PayScope(PayConst.ScopeNotify)]
    public async Task<NotifyOutput> Notify(NotifyInput input)
    {
        var orderNo = input.OrderNo?.Trim();
        var voucherNo = input.VoucherNo?.Trim();
        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(voucherNo) || input.Amount <= 0)
            throw Oops.Oh(ErrorCodeEnum.P1015);

        var notifyTime = input.NotifyTime ?? DateTime.Now;
        // 通知方身份：既是 (ClientId, VoucherNo) 去重键的一半，也是审计主体。
        // ★ 解析不到即抛错，**不能**退化成 0：
        //   退化成 0 会让所有接入方共享同一个「0 号调用方」的去重命名空间，
        //   于是 A 用过的凭证号会把 B 的到账通知静默去重掉 —— 钱被吞、双方都看不到报错。
        var clientId = await ResolveRequiredClientIdAsync();
        var rawBody = input.ToJson();

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            // ── 步骤 1：凭证号去重（F4.5）────────────────────────────
            // 先插后判：唯一索引 (ClientId, VoucherNo) 是去重的最终防线，天然抗并发。
            // Applied 先置 false，累加成功后再回填 true（异常分支保持 false）。
            var record = new PayNotifyRecord
            {
                OrderId = 0,
                OrderNo = orderNo,
                Amount = input.Amount,
                NotifyTime = notifyTime,
                VoucherNo = voucherNo,
                ClientId = clientId,
                RawBody = rawBody,
                Applied = false
            };
            await _payNotifyRecordRep.InsertAsync(record);

            // ── 步骤 2：查订单 ────────────────────────────────────────
            // ⚠️ 这里**刻意不按 ClientId 过滤**，与 allocate / status 的处理相反。
            //    原因：这两个接口的调用方是**不同角色** ——
            //      · allocate 由「业务系统」调用，它创建订单，所以订单归属它，查询必须按 ClientId 隔离；
            //      · notify 由「支付渠道」调用，它**不创建订单**，只上报「某笔订单到账了」，
            //        一笔订单天然可能由渠道侧统一上报，若按 ClientId 过滤，渠道方永远查不到别人的订单，
            //        到账通知会整体失效（全部落异常台账）。
            //    所以 notify 的信任模型是「渠道方是受信角色」：
            //    它的凭证应只授予 scope=notify 并单独管理，且每次调用都会写审计（见下方 AuditNotifyAsync）。
            var order = await _payOrderRep.AsQueryable().Where(u => u.OrderNo == orderNo).FirstAsync();
            if (order == null)
            {
                await WriteAbnormalAsync(clientId, orderNo, input.Amount, notifyTime, voucherNo,
                    PayAbnormalReasonEnum.OrderNotFound, rawBody);
                await AuditNotifyAsync(0, orderNo,
                    $"订单不存在（金额 {input.Amount:0.00}，凭证号 {voucherNo}），已入异常台账", _db);
                tenant.CommitTran();
                return BuildOutput(PayConst.NotifyResultAbnormal, "无匹配订单，已入异常台账", null);
            }

            record.OrderId = order.Id;
            // 回填关联订单Id：订单在去重插入之后才查到，此处补上便于排查（异常分支同样需要）
            await _payNotifyRecordRep.AsUpdateable()
                .SetColumns(u => new PayNotifyRecord { OrderId = order.Id })
                .Where(u => u.Id == record.Id)
                .ExecuteCommandAsync();

            // ── 步骤 3：终态判断 ──────────────────────────────────────
            if (order.Status == PayOrderStatusEnum.Expired)
            {
                await WriteAbnormalAsync(clientId, orderNo, input.Amount, notifyTime, voucherNo,
                    PayAbnormalReasonEnum.OrderExpired, rawBody);
                await AuditNotifyAsync(order.Id, order.OrderNo,
                    $"订单已过期（金额 {input.Amount:0.00}，凭证号 {voucherNo}），已入异常台账", _db);
                tenant.CommitTran();
                return BuildOutput(PayConst.NotifyResultAbnormal, "订单已过期，已入异常台账", order);
            }
            if (order.Status == PayOrderStatusEnum.Completed)
            {
                await WriteAbnormalAsync(clientId, orderNo, input.Amount, notifyTime, voucherNo,
                    PayAbnormalReasonEnum.OrderCompleted, rawBody);
                await AuditNotifyAsync(order.Id, order.OrderNo,
                    $"订单已完成（金额 {input.Amount:0.00}，凭证号 {voucherNo}），已入异常台账", _db);
                tenant.CommitTran();
                return BuildOutput(PayConst.NotifyResultAbnormal, "订单已完成，已入异常台账", order);
            }

            // ── 步骤 4~6：累加 + 判定新状态 + 事件流水 ────────────────
            var applied = await ApplyReceiptCoreAsync(order, input.Amount);
            if (applied == null)
            {
                // 影响行数 == 0：订单刚被并发终结（过期任务，或另一笔到账把它推到已完成）。
                // 按订单**当前真实状态**判定异常原因，不要一律记「已过期」。
                var terminal = await _payOrderRep.GetByIdAsync(order.Id);
                var reason = terminal?.Status == PayOrderStatusEnum.Completed
                    ? PayAbnormalReasonEnum.OrderCompleted
                    : PayAbnormalReasonEnum.OrderExpired;
                await WriteAbnormalAsync(clientId, orderNo, input.Amount, notifyTime, voucherNo, reason, rawBody);
                await AuditNotifyAsync(order.Id, order.OrderNo,
                    $"订单刚被并发终结（金额 {input.Amount:0.00}，凭证号 {voucherNo}，原因 {reason.GetDescription()}），已入异常台账", _db);
                tenant.CommitTran();
                return BuildOutput(PayConst.NotifyResultAbnormal,
                    reason == PayAbnormalReasonEnum.OrderCompleted ? "订单刚被并发完成，已入异常台账" : "订单刚被终结，已入异常台账",
                    terminal ?? order);
            }

            // 回填通知记录：本次凭证号已实际累加
            await _payNotifyRecordRep.AsUpdateable()
                .SetColumns(u => new PayNotifyRecord { Applied = true })
                .Where(u => u.Id == record.Id)
                .ExecuteCommandAsync();

            // 调用审计（F7.3）：与「累加到账」同事务提交，保证资金变动必有归属记录
            await AuditNotifyAsync(applied.OrderId, applied.OrderNo,
                $"到账通知受理：本次到账 {input.Amount:0.00}，累计 {applied.ReceivedAmount:0.00} / 应到 {applied.RequestAmount:0.00}"
                + (applied.Overpay > 0 ? $"，超额 {applied.Overpay:0.00}" : "")
                + $"，凭证号 {voucherNo}，结果 {applied.Status.GetDescription()}", _db);

            tenant.CommitTran();

            return new NotifyOutput
            {
                Result = PayConst.NotifyResultAccepted,
                ResultText = applied.Status == PayOrderStatusEnum.Completed ? "订单已完成" : "已受理，部分到账",
                OrderNo = applied.OrderNo,
                OrderStatus = applied.Status.GetDescription(),
                ReceivedAmount = applied.ReceivedAmount,
                RequestAmount = applied.RequestAmount
            };
        }
        catch (Exception ex)
        {
            tenant.RollbackTran();

            // 唯一索引冲突 = 凭证号重复（F4.5）→ 业务上已受理，不累加、不报错
            if (IsUniqueViolation(ex))
            {
                var dup = await _payOrderRep.AsQueryable().Where(u => u.OrderNo == orderNo).FirstAsync();

                // 审计写在**事务外**（上面已 RollbackTran）：重复通知是「通知方在重试」的证据，
                // 高频出现说明接入方没按契约处理 duplicated 响应，值得留痕。
                await AuditNotifyAsync(dup?.Id ?? 0, dup?.OrderNo ?? orderNo,
                    $"凭证号重复，已忽略（金额 {input.Amount:0.00}，凭证号 {voucherNo}）", null);

                return BuildOutput(PayConst.NotifyResultDuplicated, "重复通知，已忽略", dup);
            }
            throw;
        }
    }

    // ─────────────────────────── 内部实现 ───────────────────────────

    /// <summary>
    /// 到账累加核心（§5.2 步骤 4~6）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>供到账通知与人工关联（§5.4）复用</b>：人工关联必须走同一套累加逻辑，
    /// 这样「部分到账 / 完成 / 超额」的判定与额度结转才不会出现两套实现。
    /// </para>
    /// <para>调用方需自行保证在事务内调用。</para>
    /// </remarks>
    /// <param name="order">订单（调用方已校验非终态）</param>
    /// <param name="amount">本次到账金额</param>
    /// <returns>累加结果；影响行数为 0（订单被并发终结）时返回 null</returns>
    [NonAction]
    public async Task<ReceiptApplyResult> ApplyReceiptCoreAsync(PayOrder order, decimal amount)
    {
        var now = DateTime.Now;

        // 步骤 4：累加到账金额（带状态守卫的 CAS，防与过期任务并发）
        var rows = await _payOrderRep.AsUpdateable()
            .SetColumns(u => new PayOrder
            {
                ReceivedAmount = u.ReceivedAmount + amount,
                UpdateTime = now
            })
            .Where(u => u.Id == order.Id
                && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial))
            .ExecuteCommandAsync();
        if (rows == 0) return null;

        // 重读：并发下不能信任进入本方法前读到的旧值
        var fresh = await _payOrderRep.GetByIdAsync(order.Id);
        var requestAmount = fresh.RequestAmount;
        var received = fresh.ReceivedAmount;
        var overpay = received > requestAmount ? received - requestAmount : 0m;

        if (received >= requestAmount)
        {
            // ── 步骤 5（完成）──────────────────────────────────────
            // 已用额度按「实际累计到账」计入（含超额部分，设计决策 #2：如实记账）；
            // 锁定额度按「请求金额」全额释放。
            //
            // ⚠️ 这里必须是累计到账 received，而不是本次到账 amount：
            //    分次到账场景下（如请求 100，先到 60 再到 50），前面的 60 在「部分到账」分支
            //    是不动额度的（§5.2 步骤 5 部分到账分支明确「额度继续锁定」），
            //    若此处只加本次的 50，UsedQuota 会少记 60，与 §5.3 过期结转的算法也不一致。
            //
            // ⚠️ 「完成」必须带状态守卫，保证同一订单只被完成一次：
            //    否则两笔并发到账都判定达标时，会把同一个订单的 LockedQuota 释放两次 → 额度被凭空放大。
            var completedRows = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    Status = PayOrderStatusEnum.Completed,
                    CompleteTime = now,
                    OverpayRemark = overpay > 0 ? $"超额到账 {overpay:0.00}" : fresh.OverpayRemark,
                    UpdateTime = now
                })
                .Where(u => u.Id == fresh.Id
                    && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial))
                .ExecuteCommandAsync();

            if (completedRows == 1)
            {
                // 由本请求完成该订单 → 由本请求负责额度结转与「完成」事件
                await _payAccountRep.AsUpdateable()
                    .SetColumns(u => new PayAccount
                    {
                        UsedQuota = u.UsedQuota + received,
                        LockedQuota = u.LockedQuota - requestAmount,
                        UpdateTime = now
                    })
                    // 下界守卫：并发下 LockedQuota 可能已被过期任务释放过，防止减成负数
                    .Where(u => u.Id == fresh.AccountId && u.LockedQuota >= requestAmount)
                    .ExecuteCommandAsync();

                // 额度变动后同步账号状态（F1.4：超额到账可能让剩余额度归零）
                await _payAccountService.SyncStatusByQuotaAsync(fresh.AccountId);

                // 步骤 6：事件流水（只增不改）
                await _payOrderEventRep.InsertAsync(new PayOrderEvent
                {
                    OrderId = fresh.Id,
                    OrderNo = fresh.OrderNo,
                    EventType = PayEventTypeEnum.Completed,
                    FromStatus = fresh.Status,
                    ToStatus = PayOrderStatusEnum.Completed,
                    Amount = amount,
                    ReceivedTotal = received,
                    Remark = overpay > 0
                        ? $"订单完成，本次到账 {amount:0.00}，累计 {received:0.00}，超额 {overpay:0.00}"
                        : $"订单完成，本次到账 {amount:0.00}，累计 {received:0.00}",
                    OperatorId = null,
                    OperatorName = "系统"
                });
            }
            else
            {
                // 防御分支：订单已被并发请求完成。
                // 正常路径不会进入（本请求已在步骤 4 锁住该行），此处兜底：
                // 金额已并入对方读到的 ReceivedTotal 一并结转，不重复动额度，只补一条流水保证逐笔可追溯。
                await _payOrderEventRep.InsertAsync(new PayOrderEvent
                {
                    OrderId = fresh.Id,
                    OrderNo = fresh.OrderNo,
                    EventType = PayEventTypeEnum.PartialReceived,
                    FromStatus = PayOrderStatusEnum.Completed,
                    ToStatus = PayOrderStatusEnum.Completed,
                    Amount = amount,
                    ReceivedTotal = received,
                    Remark = $"并发到账 {amount:0.00}，已并入同一订单的完成结算（累计 {received:0.00}），不重复结转额度",
                    OperatorId = null,
                    OperatorName = "系统"
                });
            }
        }
        else
        {
            // ── 步骤 5（部分到账）──────────────────────────────────
            // 额度继续锁定，不动 UsedQuota / LockedQuota
            await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder { Status = PayOrderStatusEnum.Partial, UpdateTime = now })
                .Where(u => u.Id == fresh.Id
                    && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial))
                .ExecuteCommandAsync();

            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = fresh.Id,
                OrderNo = fresh.OrderNo,
                EventType = PayEventTypeEnum.PartialReceived,
                FromStatus = fresh.Status,
                ToStatus = PayOrderStatusEnum.Partial,
                Amount = amount,
                ReceivedTotal = received,
                Remark = $"部分到账，本次 {amount:0.00}，累计 {received:0.00} / 应到 {requestAmount:0.00}",
                OperatorId = null,
                OperatorName = "系统"
            });
        }

        return new ReceiptApplyResult
        {
            OrderId = fresh.Id,
            OrderNo = fresh.OrderNo,
            Status = received >= requestAmount ? PayOrderStatusEnum.Completed : PayOrderStatusEnum.Partial,
            ReceivedAmount = received,
            RequestAmount = requestAmount,
            Overpay = overpay
        };
    }

    /// <summary>
    /// 写入异常到账台账（F5.1）
    /// </summary>
    /// <remarks>调用方需自行保证在事务内调用。</remarks>
    /// <param name="clientId">通知方标识</param>
    /// <param name="reportOrderNo">通知方上报的订单号</param>
    /// <param name="amount">到账金额</param>
    /// <param name="notifyTime">到账时间</param>
    /// <param name="voucherNo">凭证号</param>
    /// <param name="reason">异常原因</param>
    /// <param name="rawBody">原始报文</param>
    /// <returns></returns>
    [NonAction]
    public async Task WriteAbnormalAsync(long clientId, string reportOrderNo, decimal amount,
        DateTime notifyTime, string voucherNo, PayAbnormalReasonEnum reason, string rawBody)
    {
        await _payAbnormalReceiptRep.InsertAsync(new PayAbnormalReceipt
        {
            Amount = amount,
            NotifyTime = notifyTime,
            VoucherNo = voucherNo,
            ClientId = clientId,
            ReportOrderNo = reportOrderNo,
            RawBody = rawBody,
            Reason = reason,
            HandleStatus = PayHandleStatusEnum.Pending
        });
    }

    /// <summary>
    /// 组装对外返回
    /// </summary>
    /// <param name="result">结果码</param>
    /// <param name="resultText">结果说明</param>
    /// <param name="order">订单（可能为空）</param>
    /// <returns></returns>
    [NonAction]
    public static NotifyOutput BuildOutput(string result, string resultText, PayOrder order)
    {
        return new NotifyOutput
        {
            Result = result,
            ResultText = resultText,
            OrderNo = order?.OrderNo,
            OrderStatus = order == null ? null : order.Status.GetDescription(),
            ReceivedAmount = order?.ReceivedAmount ?? 0m,
            RequestAmount = order?.RequestAmount ?? 0m
        };
    }

    /// <summary>
    /// 解析调用方标识（<c>SysOpenAccess.Id</c>），用于凭证去重键与审计
    /// </summary>
    /// <remarks>
    /// 身份来源与口径统一在 <see cref="PayCallerContext"/>，本方法只是它的实例化入口。
    /// </remarks>
    /// <returns></returns>
    [NonAction]
    public Task<long?> ResolveClientIdAsync()
        => Task.FromResult(PayCallerContext.ClientId(_httpContextAccessor.HttpContext));

    /// <summary>
    /// 解析调用方标识，解析不到即抛错（用于凭证去重键）
    /// </summary>
    /// <remarks>
    /// 为什么不能退化成 0：见 <see cref="PayCallerContext.RequireClientId"/>。
    /// 对到账通知而言，退化成 0 会让不同渠道方共享去重命名空间，
    /// 结果是「别人的凭证号把我的到账静默去重掉」—— 资金直接丢失且无报错。
    /// </remarks>
    /// <returns></returns>
    [NonAction]
    public Task<long> ResolveRequiredClientIdAsync()
        => Task.FromResult(PayCallerContext.RequireClientId(_httpContextAccessor.HttpContext));

    /// <summary>
    /// 写一条到账通知的调用审计（F7.3）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 到账通知是**唯一会推进资金状态**的开放接口（把订单从待支付推到部分到账/已完成），
    /// 所以它的每一次调用都必须留下「哪个接入方、以哪个凭证号、报了多少钱、结果如何」。
    /// </para>
    /// <para>
    /// 在业务事务内调用时传 <paramref name="db"/>，让审计与状态变更同事务提交。
    /// </para>
    /// </remarks>
    /// <param name="orderId">订单Id（订单不存在时为 0）</param>
    /// <param name="orderNo">订单号（订单不存在时为上报值）</param>
    /// <param name="remark">说明</param>
    /// <param name="db">可选：复用调用方的事务</param>
    [NonAction]
    private Task AuditNotifyAsync(long orderId, string orderNo, string remark, ISqlSugarClient db)
        => _payAuditService.WriteOpenApiAsync(PayAuditActionEnum.ApiCall,
            PayConst.AuditTargetTypeOrder, orderId, orderNo, $"到账通知：{remark}", db);

    /// <summary>
    /// 判断异常是否为「唯一约束冲突」
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于把凭证号重复（F4.5）从普通异常里识别出来。
    /// 主判据是 PostgreSQL 的错误码：唯一约束冲突固定为 <c>SqlState = 23505</c>，
    /// 按错误码判定不依赖驱动报错文案，也不受数据库语言环境影响。
    /// </para>
    /// <para>
    /// 会沿 <see cref="Exception.InnerException"/> 链查找，因为 SqlSugar 可能把驱动异常包一层；
    /// 另保留一条「报错文案」兜底，防止某条链路上驱动异常类型被吞掉。
    /// </para>
    /// </remarks>
    /// <param name="ex"></param>
    /// <returns></returns>
    [NonAction]
    public static bool IsUniqueViolation(Exception ex)
    {
        // PostgreSQL 唯一约束冲突错误码（等价于 Npgsql 的 PostgresErrorCodes.UniqueViolation）
        const string UniqueViolationSqlState = "23505";

        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is Npgsql.PostgresException pg && pg.SqlState == UniqueViolationSqlState)
                return true;

            if (e.Message?.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase) == true)
                return true;
        }
        return false;
    }
}
