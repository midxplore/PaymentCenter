// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

// 标准 DI 日志：Admin.NET.Application 的全局 using 里没有它，就地引入。
using Microsoft.Extensions.Logging;

namespace Admin.NET.Application;

/// <summary>
/// 订单过期处理服务（F3.2）
/// </summary>
/// <remarks>
/// <para>
/// 业务逻辑放在服务里、由 <see cref="PayOrderExpireJob"/> 触发，而不是直接写在 Job 的
/// <c>ExecuteAsync</c> 里：这样过期逻辑可以脱离调度器被测试与手工触发，
/// Job 只负责「按框架约定声明调度计划」这一件事。
/// </para>
/// <para>
/// 与到账通知（<see cref="PayNotifyService"/>）的并发关系：两边都用<b>条件更新</b>抢同一行，
/// 谁先改成功谁负责后续的额度结转，另一方走异常台账。因此本服务不需要额外加锁。
/// </para>
/// </remarks>
[ApiDescriptionSettings(Order = 406, Description = "收款订单过期")]
public class PayOrderExpireService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly PayAccountService _payAccountService;
    private readonly ISqlSugarClient _db;
    private readonly ILogger<PayOrderExpireService> _logger;

    public PayOrderExpireService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        PayAccountService payAccountService,
        ISqlSugarClient db,
        ILogger<PayOrderExpireService> logger)
    {
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payAccountRep = payAccountRep;
        _payAccountService = payAccountService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 扫描并过期一批订单（F3.2，供定时任务调用）
    /// </summary>
    /// <remarks>
    /// 只捞「非终态且已过期」的订单 Id，逐条独立处理：
    /// 单条失败不会影响同批次其它订单，也不会让整轮扫描中断。
    /// </remarks>
    /// <param name="batchSize">本批次最多处理条数</param>
    /// <param name="cancellationToken"></param>
    /// <returns>实际过期成功的订单数</returns>
    [NonAction]
    public async Task<int> ExpireBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) batchSize = PayConst.ExpireScanBatchSize;

        var now = DateTime.Now;
        var candidateIds = await _payOrderRep.AsQueryable()
            .Where(u => (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial)
                        && u.ExpireTime <= now)
            // 先到期的先处理：积压时保证「最早的欠账」优先被清掉
            .OrderBy(u => u.ExpireTime, OrderByType.Asc)
            .Take(batchSize)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0) return 0;

        var expiredCount = 0;
        foreach (var orderId in candidateIds)
        {
            try
            {
                if (await ExpireOrderAsync(orderId, cancellationToken)) expiredCount++;
            }
            catch (Exception ex)
            {
                // 单条失败只记日志：下一轮扫描还会捞到它，不会丢单
                _logger.LogError(ex, "【收款订单过期】订单 Id={OrderId} 过期处理失败，等待下一轮重试", orderId);
            }
        }
        return expiredCount;
    }

    /// <summary>
    /// 过期单个订单：条件抢占 → 额度结转 → 事件流水（F3.2，单条一个独立事务）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么先抢占再读金额</b>：到账通知的累加语句
    /// （<c>UPDATE pay_order SET ReceivedAmount += amount WHERE Status IN (待到账, 部分到账)</c>）
    /// 与这里的状态抢占是同一行的两次条件更新。若先读到账金额再抢占，
    /// 两次读取之间到账通知可能已经把金额加上去了，本服务就会按<b>偏小</b>的旧值结转，
    /// 造成 UsedQuota 少记、LockedQuota 多释放（账实不符）。
    /// 抢占成功后再读，读到的是「冻结后」的准确金额——抢占成功即意味着到账通知再也改不动这行。
    /// </para>
    /// </remarks>
    /// <param name="orderId">订单 Id</param>
    /// <param name="cancellationToken"></param>
    /// <returns>是否由本次调用完成过期（false = 已被到账通知终结或尚未到期）</returns>
    [NonAction]
    public async Task<bool> ExpireOrderAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var tenant = _db.AsTenant();

        tenant.BeginTran();
        try
        {
            // ── 步骤 1：条件抢占订单（与到账通知抢同一行）────────────────────
            var claimed = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    Status = PayOrderStatusEnum.Expired,
                    UpdateTime = now
                })
                .Where(u => u.Id == orderId
                    && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial)
                    && u.ExpireTime <= now)
                .ExecuteCommandAsync(cancellationToken);

            if (claimed == 0)
            {
                // 已被到账通知推进到终态（或已被上一轮过期、或还没到期）→ 什么都不做
                tenant.RollbackTran();
                return false;
            }

            // ── 步骤 2：抢占成功后重读（金额已冻结，见方法注释）──────────────
            var order = await _payOrderRep.GetByIdAsync(orderId);
            if (order == null)
            {
                tenant.RollbackTran();
                return false;
            }

            // 抢占条件已保证变更前状态 ∈ {待到账, 部分到账}；
            // 到账金额 > 0 即说明此前收到过部分到账，据此还原「变更前状态」写进流水
            var fromStatus = order.ReceivedAmount > 0m
                ? PayOrderStatusEnum.Partial
                : PayOrderStatusEnum.Pending;
            var released = order.RequestAmount - order.ReceivedAmount;

            // ── 步骤 3：额度结转 ────────────────────────────────────────────
            // 已到账部分转「已用」，锁定按「请求金额」全额释放 → 净释放 = 请求金额 − 已到账（§4.1）
            await _payAccountRep.AsUpdateable()
                .SetColumns(u => new PayAccount
                {
                    UsedQuota = u.UsedQuota + order.ReceivedAmount,
                    LockedQuota = u.LockedQuota - order.RequestAmount,
                    UpdateTime = now
                })
                // 下界守卫：防止极端并发下把锁定额度减成负数
                .Where(u => u.Id == order.AccountId && u.LockedQuota >= order.RequestAmount)
                .ExecuteCommandAsync(cancellationToken);

            // 释放预占后账号可能从「已用完」恢复为「启用」，重新参与匹配（F1.4）
            await _payAccountService.SyncStatusByQuotaAsync(order.AccountId);

            // ── 步骤 4：事件流水（只增不改，F7.1）────────────────────────────
            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                EventType = PayEventTypeEnum.Expired,
                FromStatus = fromStatus,
                ToStatus = PayOrderStatusEnum.Expired,
                Amount = order.ReceivedAmount,
                ReceivedTotal = order.ReceivedAmount,
                // 分支依据是「有没有收到过到账」，而不是「释放额是否为正」：
                // 未到账时 released 恰好等于请求金额（也是正数），用 released 判会走错分支
                Remark = order.ReceivedAmount > 0m
                    ? $"订单过期，已到账 {order.ReceivedAmount:0.00} 转已用，释放未达成部分 {released:0.00}"
                    : $"订单过期，未收到到账，释放全部预占 {order.RequestAmount:0.00}",
                OperatorId = null,
                OperatorName = "系统"
            });

            tenant.CommitTran();
            return true;
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
    }
}
