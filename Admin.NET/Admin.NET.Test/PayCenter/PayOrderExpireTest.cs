// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 订单自动过期与额度结转（F3.2 / F3.3）
/// </summary>
/// <remarks>
/// <para>
/// 过期是<b>资金口径</b>最容易被写错的地方：锁定要按「请求金额」全额释放，
/// 已到账部分要转「已用」，净释放 = 请求金额 − 已到账金额（§4.1）。
/// 这里把三种形态（未到账 / 部分到账 / 已完成）都钉住，并验证重复调用不会重复释放。
/// </para>
/// <para>
/// 另有两例覆盖过期与到账通知的<b>竞态</b>：先到账完成 → 过期任务必须放手；
/// 先过期 → 后续到账必须进异常台账且不改订单（F3.3）。
/// </para>
/// </remarks>
public class PayOrderExpireTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_expire";

    /// <summary>本类专用订单号前缀</summary>
    private const string OrderNoPrefix = "UTEXP";

    /// <summary>本类专用凭证号前缀</summary>
    private const string VoucherPrefix = "UTEXP-V-";

    private readonly ISqlSugarClient _db;
    private readonly PayOrderExpireService _expireService;
    private readonly PayNotifyService _notifyService;
    private readonly IDisposable _caller;

    public PayOrderExpireTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _expireService = App.GetRequiredService<PayOrderExpireService>();
        _notifyService = App.GetRequiredService<PayNotifyService>();
        // ★ 扮演一个调用方：Notify 必须能解析出 ClientId（见 PayTestCaller 的说明）。
        //   构造函数级而不是逐用例，避免「忘了加 → 抛错原因变成『无调用方』→ 用例假绿」。
        _caller = PayTestCaller.Begin();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        _caller.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数（审计 → 事件 → 订单 → 通知记录 → 台账 → 账号，顺序不能颠倒）</summary>
    private void Cleanup()
    {
        // ★ 审计行也要清：pay_audit_log 只增不改，但测试造数必须可重复运行，
        //   否则每跑一次就留一批 targetid 指向已删订单的孤儿审计行。
        //   必须**先于删订单**执行：子查询依赖 pay_order 还在。
        _db.Ado.ExecuteCommand(
            "delete from pay_audit_log where targettype = 'Order' and targetid in (select id from pay_order where orderno like @prefix)",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand(
            "delete from pay_order_event where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_notify_record where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_abnormal_receipt where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long SeedAccount(decimal total, decimal used = 0m, decimal locked = 0m,
        PayAccountStatusEnum status = PayAccountStatusEnum.Enabled)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = "unittest-expire-account",
            TotalQuota = total,
            UsedQuota = used,
            LockedQuota = locked,
            Status = status,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    /// <summary>直接造订单（绕过匹配接口，以便自由控制过期时间与已到账金额）</summary>
    private PayOrder SeedOrder(long accountId, decimal request, decimal received, double expireMinutesOffset,
        PayOrderStatusEnum status = PayOrderStatusEnum.Pending)
    {
        var order = new PayOrder
        {
            OrderNo = OrderNoPrefix + Guid.NewGuid().ToString("N")[..12],
            RequestAmount = request,
            AccountId = accountId,
            ReceivedAmount = received,
            Status = status,
            ExpireTime = DateTime.Now.AddMinutes(expireMinutesOffset),
            ClientId = 0
        };
        _db.Insertable(order).ExecuteCommand();
        return order;
    }

    private PayAccount GetAccount(long id) => _db.Queryable<PayAccount>().First(u => u.Id == id);

    private PayOrder GetOrder(long id) => _db.Queryable<PayOrder>().First(u => u.Id == id);

    private List<PayOrderEvent> GetEvents(long orderId) =>
        _db.Queryable<PayOrderEvent>().Where(u => u.OrderId == orderId).OrderBy(u => u.CreateTime).ToList();

    private static NotifyInput BuildNotify(string orderNo, decimal amount, string voucherNo) => new()
    {
        OrderNo = orderNo,
        Amount = amount,
        VoucherNo = voucherNo,
        NotifyTime = DateTime.Now
    };

    // ─────────────────────────── 三种形态的额度结转 ───────────────────────────

    [Fact]
    public async Task 未到账订单过期_应释放全部预占且不动已用()
    {
        var accountId = SeedAccount(100m, locked: 40m);
        var order = SeedOrder(accountId, 40m, 0m, -1);

        var expired = await _expireService.ExpireOrderAsync(order.Id);

        Assert.True(expired);
        Assert.Equal(PayOrderStatusEnum.Expired, GetOrder(order.Id).Status);

        // 未到账 → 全部释放，Used 不变
        var account = GetAccount(accountId);
        Assert.Equal(0m, account.LockedQuota);
        Assert.Equal(0m, account.UsedQuota);

        var events = GetEvents(order.Id);
        Assert.Single(events);
        Assert.Equal(PayEventTypeEnum.Expired, events[0].EventType);
        Assert.Equal(PayOrderStatusEnum.Pending, events[0].FromStatus);
        Assert.Equal(PayOrderStatusEnum.Expired, events[0].ToStatus);
        Assert.Equal(0m, events[0].Amount);
        Assert.Contains("释放全部预占", events[0].Remark);
    }

    [Fact]
    public async Task 部分到账订单过期_已到账应转已用且只释放未达成部分()
    {
        // 请求 100，已到账 60 → 净释放应为 40
        var accountId = SeedAccount(100m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 60m, -1, PayOrderStatusEnum.Partial);

        Assert.True(await _expireService.ExpireOrderAsync(order.Id));

        var account = GetAccount(accountId);
        Assert.Equal(60m, account.UsedQuota);   // 已到账转已用
        Assert.Equal(0m, account.LockedQuota);  // 按请求金额全额释放

        var events = GetEvents(order.Id);
        Assert.Single(events);
        Assert.Equal(PayOrderStatusEnum.Partial, events[0].FromStatus);
        Assert.Equal(60m, events[0].Amount);
        Assert.Equal(60m, events[0].ReceivedTotal);
        Assert.Contains("释放未达成部分 40.00", events[0].Remark);
    }

    [Fact]
    public async Task 过期释放预占后_已用完账号应恢复为启用()
    {
        var accountId = SeedAccount(100m, locked: 100m, status: PayAccountStatusEnum.Exhausted);
        var order = SeedOrder(accountId, 100m, 0m, -1);

        Assert.True(await _expireService.ExpireOrderAsync(order.Id));

        // 预占释放 → 剩余额度恢复 → 自动回到启用（F1.4）
        var account = GetAccount(accountId);
        Assert.Equal(0m, account.LockedQuota);
        Assert.Equal(PayAccountStatusEnum.Enabled, account.Status);
    }

    // ─────────────────────────── 不该动的行不能被碰 ───────────────────────────

    [Fact]
    public async Task 未到期订单_不应被过期()
    {
        var accountId = SeedAccount(100m, locked: 40m);
        var order = SeedOrder(accountId, 40m, 0m, +10);

        Assert.False(await _expireService.ExpireOrderAsync(order.Id));

        Assert.Equal(PayOrderStatusEnum.Pending, GetOrder(order.Id).Status);
        Assert.Equal(40m, GetAccount(accountId).LockedQuota);
        Assert.Empty(GetEvents(order.Id));
    }

    [Fact]
    public async Task 已完成订单_不应被过期且不重复释放额度()
    {
        // 已完成订单的额度早已结转：Used=40、Locked 已释放
        var accountId = SeedAccount(100m, used: 40m, locked: 0m);
        var order = SeedOrder(accountId, 40m, 40m, -1, PayOrderStatusEnum.Completed);

        Assert.False(await _expireService.ExpireOrderAsync(order.Id));

        var account = GetAccount(accountId);
        Assert.Equal(40m, account.UsedQuota);
        Assert.Equal(0m, account.LockedQuota);
        Assert.Equal(PayOrderStatusEnum.Completed, GetOrder(order.Id).Status);
        Assert.Empty(GetEvents(order.Id));
    }

    [Fact]
    public async Task 重复调用过期_不应重复释放额度也不重复记事件()
    {
        var accountId = SeedAccount(100m, locked: 40m);
        var order = SeedOrder(accountId, 40m, 0m, -1);

        Assert.True(await _expireService.ExpireOrderAsync(order.Id));
        Assert.False(await _expireService.ExpireOrderAsync(order.Id));

        Assert.Equal(0m, GetAccount(accountId).LockedQuota);
        Assert.Single(GetEvents(order.Id));   // 事件只记一次
    }

    // ─────────────────────────── 批量扫描 ───────────────────────────

    [Fact]
    public async Task 批量扫描_只处理已到期的非终态订单()
    {
        // 锁定 150 = ①40 + ②50 + ③60（终态的 ④ 早已释放，不再计入锁定）
        var accountId = SeedAccount(1000m, locked: 150m);
        SeedOrder(accountId, 40m, 0m, -1);                                        // ① 到期未到账 → 应处理
        SeedOrder(accountId, 50m, 20m, -5, PayOrderStatusEnum.Partial);            // ② 到期部分到账 → 应处理
        SeedOrder(accountId, 60m, 0m, +30);                                       // ③ 未到期 → 不处理
        SeedOrder(accountId, 60m, 60m, -1, PayOrderStatusEnum.Completed);          // ④ 已完成 → 不处理

        var expiredCount = await _expireService.ExpireBatchAsync(200);

        Assert.Equal(2, expiredCount);

        // ① 全释放 40；② 释放 50 且已到账 20 转已用 → 150 − 40 − 50 = 60
        var account = GetAccount(accountId);
        Assert.Equal(20m, account.UsedQuota);
        Assert.Equal(60m, account.LockedQuota);
    }

    [Fact]
    public async Task 批量扫描_无到期订单时应为零且不改动任何数据()
    {
        var accountId = SeedAccount(100m, locked: 40m);
        SeedOrder(accountId, 40m, 0m, +10);

        Assert.Equal(0, await _expireService.ExpireBatchAsync(200));

        Assert.Equal(40m, GetAccount(accountId).LockedQuota);
    }

    // ─────────────────────────── 与到账通知的竞态 ───────────────────────────

    [Fact]
    public async Task 到账通知已把订单推到完成_过期任务应放手且不再动额度()
    {
        // 模拟真实竞态：订单已过期时刻，但到账通知先一步把它做完了
        var accountId = SeedAccount(100m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, -1);

        var notify = await _notifyService.Notify(BuildNotify(order.OrderNo, 100m, VoucherPrefix + "done"));
        Assert.Equal(PayConst.NotifyResultAccepted, notify.Result);
        Assert.Equal(PayOrderStatusEnum.Completed, GetOrder(order.Id).Status);

        // 到账通知已结转：Used=100、Locked=0
        Assert.Equal(100m, GetAccount(accountId).UsedQuota);
        Assert.Equal(0m, GetAccount(accountId).LockedQuota);

        // 过期任务此时必须放手——否则会二次释放锁定（把额度凭空放大）
        Assert.False(await _expireService.ExpireOrderAsync(order.Id));

        Assert.Equal(100m, GetAccount(accountId).UsedQuota);
        Assert.Equal(0m, GetAccount(accountId).LockedQuota);
        Assert.Equal(PayOrderStatusEnum.Completed, GetOrder(order.Id).Status);
        Assert.DoesNotContain(GetEvents(order.Id), e => e.EventType == PayEventTypeEnum.Expired);
    }

    [Fact]
    public async Task 订单已过期后再到账_应入异常台账且不改订单与额度()
    {
        var accountId = SeedAccount(100m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, -1);

        Assert.True(await _expireService.ExpireOrderAsync(order.Id));

        var notify = await _notifyService.Notify(BuildNotify(order.OrderNo, 100m, VoucherPrefix + "late"));

        // F3.3：终态后到账只入台账，订单与额度都不动
        Assert.Equal(PayConst.NotifyResultAbnormal, notify.Result);
        Assert.Equal(PayOrderStatusEnum.Expired, GetOrder(order.Id).Status);

        var account = GetAccount(accountId);
        Assert.Equal(0m, account.UsedQuota);
        Assert.Equal(0m, account.LockedQuota);

        var abnormal = _db.Queryable<PayAbnormalReceipt>()
            .Where(u => u.VoucherNo == VoucherPrefix + "late").First();
        Assert.NotNull(abnormal);
        Assert.Equal(PayAbnormalReasonEnum.OrderExpired, abnormal.Reason);
        Assert.Equal(PayHandleStatusEnum.Pending, abnormal.HandleStatus);

        // 清理本用例产生的台账（不属于本类的前缀造数）
        _db.Ado.ExecuteCommand("delete from pay_abnormal_receipt where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
    }
}
