// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 异常到账台账的人工关联与处置（F5.2 / F5.3）
/// </summary>
/// <remarks>
/// <para>
/// 核心断言不是「关联成功」，而是<b>关联复用了到账通知的同一套累加逻辑</b>：
/// 部分到账不动额度、完成时按「累计到账」转已用并按「请求金额」释放锁定，
/// 与 <see cref="PayNotifyService"/> 的口径必须完全一致。
/// </para>
/// <para>另外钉住处置的边界：终态订单不能关联、已处置的台账不能重复处置、忽略不动金额。</para>
/// </remarks>
public class PayAbnormalLinkTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_abnormal";

    /// <summary>本类专用订单号前缀</summary>
    private const string OrderNoPrefix = "UTABN";

    /// <summary>本类专用凭证号前缀（同时用作台账与审计的清理依据）</summary>
    private const string VoucherPrefix = "UTABN-V-";

    private readonly ISqlSugarClient _db;
    private readonly PayAbnormalService _abnormalService;
    private readonly PayNotifyService _notifyService;
    private readonly IDisposable _caller;

    public PayAbnormalLinkTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _abnormalService = App.GetRequiredService<PayAbnormalService>();
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

    private void Cleanup()
    {
        // ★ 到账通知产生的审计行：targettype='Order'、targetid=订单 id、targetno=订单号。
        //   订单号现在是**纯数字、不带测试前缀**，所以只能靠子查询定位 ——
        //   下面那条按 VoucherPrefix 过滤 targetno 的语句**抓不到**它们。
        //   必须**先于删订单**执行：子查询依赖 pay_order 还在。
        _db.Ado.ExecuteCommand(
            "delete from pay_audit_log where targettype = 'Order' and targetid in (select id from pay_order where orderno like @prefix)",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order_event where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_notify_record where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_abnormal_receipt where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_audit_log where targetno like @prefix",
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
            AccountInfo = "unittest-abnormal-account",
            TotalQuota = total,
            UsedQuota = used,
            LockedQuota = locked,
            Status = status,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private PayOrder SeedOrder(long accountId, decimal request, decimal received, PayOrderStatusEnum status,
        double expireMinutesOffset = +30)
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

    /// <summary>造一条异常台账 + 与之对应的通知记录（真实流程里两者成对出现）</summary>
    private PayAbnormalReceipt SeedAbnormal(string voucherNo, decimal amount, string reportOrderNo,
        PayAbnormalReasonEnum reason = PayAbnormalReasonEnum.OrderNotFound)
    {
        var abnormal = new PayAbnormalReceipt
        {
            Amount = amount,
            NotifyTime = DateTime.Now,
            VoucherNo = voucherNo,
            ClientId = 0,
            ReportOrderNo = reportOrderNo,
            RawBody = "{\"unittest\":true}",
            Reason = reason,
            HandleStatus = PayHandleStatusEnum.Pending
        };
        _db.Insertable(abnormal).ExecuteCommand();

        _db.Insertable(new PayNotifyRecord
        {
            OrderId = 0,
            OrderNo = reportOrderNo,
            Amount = amount,
            NotifyTime = abnormal.NotifyTime,
            VoucherNo = voucherNo,
            ClientId = 0,
            RawBody = abnormal.RawBody,
            Applied = false
        }).ExecuteCommand();

        return abnormal;
    }

    private PayAccount GetAccount(long id) => _db.Queryable<PayAccount>().First(u => u.Id == id);

    private PayOrder GetOrder(long id) => _db.Queryable<PayOrder>().First(u => u.Id == id);

    private List<PayOrderEvent> GetEvents(long orderId) =>
        _db.Queryable<PayOrderEvent>().Where(u => u.OrderId == orderId).OrderBy(u => u.CreateTime).ToList();

    // ─────────────────────────── 关联：复用同一套累加逻辑 ───────────────────────────

    [Fact]
    public async Task 人工关联未达标_应记部分到账且额度继续锁定()
    {
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Pending);
        var voucher = VoucherPrefix + "partial";
        var abnormal = SeedAbnormal(voucher, 40m, "REPORTED-NOT-EXIST");

        var output = await _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo,
            HandleRemark = "核对银行流水后人工挂账"
        });

        // 订单：部分到账，累计 40
        Assert.Equal(PayOrderStatusEnum.Partial, output.OrderStatus);
        Assert.Equal(40m, output.ReceivedAmount);
        Assert.Equal(PayOrderStatusEnum.Partial, GetOrder(order.Id).Status);
        Assert.Equal(40m, GetOrder(order.Id).ReceivedAmount);

        // 额度：部分到账不动额度（与到账通知口径一致）
        var account = GetAccount(accountId);
        Assert.Equal(0m, account.UsedQuota);
        Assert.Equal(100m, account.LockedQuota);

        // 台账：已关联 + 处理人 + 关联订单号
        var after = _db.Queryable<PayAbnormalReceipt>().First(u => u.Id == abnormal.Id);
        Assert.Equal(PayHandleStatusEnum.Linked, after.HandleStatus);
        Assert.Equal(order.Id, after.RelatedOrderId);
        Assert.Equal(order.OrderNo, after.RelatedOrderNo);
        Assert.NotNull(after.HandleTime);
        Assert.Equal("核对银行流水后人工挂账", after.HandleRemark);

        // 事件：人工关联 + 部分到账 两条，顺序为「先人工关联、后金额生效」
        var events = GetEvents(order.Id);
        Assert.Equal(2, events.Count);
        Assert.Equal(PayEventTypeEnum.ManualLinked, events[0].EventType);
        Assert.Equal(PayEventTypeEnum.PartialReceived, events[1].EventType);
        Assert.Contains(voucher, events[0].Remark);

        // 通知记录回填 Applied（该凭证号的钱确实入账了）
        Assert.True(_db.Queryable<PayNotifyRecord>().First(u => u.VoucherNo == voucher).Applied);

        // 审计留痕
        var audits = _db.Queryable<PayAuditLog>().Where(u => u.TargetNo == voucher).ToList();
        Assert.Single(audits);
        Assert.Equal(PayAuditActionEnum.AbnormalHandle, audits[0].Action);
        Assert.Contains("人工关联到订单", audits[0].Remark);
    }

    [Fact]
    public async Task 人工关联达标_应按累计到账转已用并按请求金额释放锁定()
    {
        // 请求 100，此前已到账 60（部分到账，额度仍锁定 100）
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 60m, PayOrderStatusEnum.Partial);
        var abnormal = SeedAbnormal(VoucherPrefix + "complete", 40m, order.OrderNo);

        var output = await _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        });

        Assert.Equal(PayOrderStatusEnum.Completed, output.OrderStatus);
        Assert.Equal(100m, output.ReceivedAmount);

        // 额度：已用 = 累计到账 100（含此前那 60），锁定按请求金额全额释放
        var account = GetAccount(accountId);
        Assert.Equal(100m, account.UsedQuota);
        Assert.Equal(0m, account.LockedQuota);

        var events = GetEvents(order.Id);
        Assert.Equal(2, events.Count);
        Assert.Equal(PayEventTypeEnum.ManualLinked, events[0].EventType);
        Assert.Equal(PayEventTypeEnum.Completed, events[1].EventType);
        Assert.Equal(100m, events[1].ReceivedTotal);
    }

    [Fact]
    public async Task 人工关联超额到账_应完成并记录超额金额()
    {
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Pending);
        var abnormal = SeedAbnormal(VoucherPrefix + "overpay", 150m, order.OrderNo);

        var output = await _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        });

        Assert.Equal(PayOrderStatusEnum.Completed, output.OrderStatus);
        Assert.Equal(150m, output.ReceivedAmount);

        // 超额如实记账（设计决策 #2）：已用 150，锁定按请求金额 100 释放
        var account = GetAccount(accountId);
        Assert.Equal(150m, account.UsedQuota);
        Assert.Equal(0m, account.LockedQuota);
        Assert.Contains("超额到账 50.00", GetOrder(order.Id).OverpayRemark);
    }

    // ─────────────────────────── 关联的边界 ───────────────────────────

    [Fact]
    public async Task 关联到已过期订单_应报错且不改动任何数据()
    {
        var accountId = SeedAccount(200m, locked: 0m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Expired);
        var abnormal = SeedAbnormal(VoucherPrefix + "expired", 40m, order.OrderNo,
            PayAbnormalReasonEnum.OrderExpired);

        await Assert.ThrowsAnyAsync<Exception>(() => _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        }));

        Assert.Equal(PayOrderStatusEnum.Expired, GetOrder(order.Id).Status);
        Assert.Equal(PayHandleStatusEnum.Pending,
            _db.Queryable<PayAbnormalReceipt>().First(u => u.Id == abnormal.Id).HandleStatus);
        Assert.Empty(GetEvents(order.Id));
        Assert.Equal(0m, GetAccount(accountId).UsedQuota);
    }

    [Fact]
    public async Task 关联到已完成订单_应报错()
    {
        var accountId = SeedAccount(200m, used: 100m, locked: 0m);
        var order = SeedOrder(accountId, 100m, 100m, PayOrderStatusEnum.Completed);
        var abnormal = SeedAbnormal(VoucherPrefix + "completed", 40m, order.OrderNo,
            PayAbnormalReasonEnum.OrderCompleted);

        await Assert.ThrowsAnyAsync<Exception>(() => _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        }));

        Assert.Equal(100m, GetAccount(accountId).UsedQuota);
    }

    [Fact]
    public async Task 关联到不存在的订单_应报错()
    {
        var abnormal = SeedAbnormal(VoucherPrefix + "noorder", 40m, "NOT-EXIST");

        await Assert.ThrowsAnyAsync<Exception>(() => _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = "NOT-EXIST"
        }));

        Assert.Equal(PayHandleStatusEnum.Pending,
            _db.Queryable<PayAbnormalReceipt>().First(u => u.Id == abnormal.Id).HandleStatus);
    }

    [Fact]
    public async Task 重复关联同一条台账_应报错且不重复累加()
    {
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Pending);
        var abnormal = SeedAbnormal(VoucherPrefix + "dup", 40m, order.OrderNo);

        await _abnormalService.Link(new LinkAbnormalInput { Id = abnormal.Id, TargetOrderNo = order.OrderNo });

        await Assert.ThrowsAnyAsync<Exception>(() => _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        }));

        // 金额只累加了一次
        Assert.Equal(40m, GetOrder(order.Id).ReceivedAmount);
        Assert.Equal(2, GetEvents(order.Id).Count);
    }

    [Fact]
    public async Task 确认无需处理_应标记已忽略且不动订单与额度()
    {
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Pending);
        var voucher = VoucherPrefix + "ignore";
        var abnormal = SeedAbnormal(voucher, 40m, order.OrderNo);

        await _abnormalService.Ignore(new IgnoreAbnormalInput
        {
            Id = abnormal.Id,
            HandleRemark = "重复上报，已在另一凭证号入账"
        });

        var after = _db.Queryable<PayAbnormalReceipt>().First(u => u.Id == abnormal.Id);
        Assert.Equal(PayHandleStatusEnum.Ignored, after.HandleStatus);
        Assert.NotNull(after.HandleTime);
        Assert.Equal("重复上报，已在另一凭证号入账", after.HandleRemark);

        // 金额与订单完全不动
        Assert.Equal(0m, GetOrder(order.Id).ReceivedAmount);
        Assert.Equal(PayOrderStatusEnum.Pending, GetOrder(order.Id).Status);
        Assert.Equal(100m, GetAccount(accountId).LockedQuota);
        Assert.Empty(GetEvents(order.Id));

        // 忽略后不可再关联
        await Assert.ThrowsAnyAsync<Exception>(() => _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = order.OrderNo
        }));

        // 审计留痕
        Assert.Single(_db.Queryable<PayAuditLog>().Where(u => u.TargetNo == voucher).ToList());
    }

    // ─────────────────────────── 台账查询（F5.3） ───────────────────────────

    [Fact]
    public async Task 台账分页_可按处理状态过滤()
    {
        var accountId = SeedAccount(200m, locked: 100m);
        var order = SeedOrder(accountId, 100m, 0m, PayOrderStatusEnum.Pending);
        var reportNo = OrderNoPrefix + "PAGE";

        var pending = SeedAbnormal(VoucherPrefix + "page-1", 10m, reportNo);
        var ignored = SeedAbnormal(VoucherPrefix + "page-2", 20m, reportNo);
        await _abnormalService.Ignore(new IgnoreAbnormalInput { Id = ignored.Id, HandleRemark = "测试打款" });

        var page = await _abnormalService.Page(new PagePayAbnormalInput
        {
            ReportOrderNo = reportNo,
            HandleStatus = PayHandleStatusEnum.Pending,
            Page = 1,
            PageSize = 20
        });

        var item = Assert.Single(page.Items);
        Assert.Equal(pending.Id, item.Id);
        Assert.Equal(PayHandleStatusEnum.Pending, item.HandleStatus);
        Assert.Equal("待处理", item.HandleStatusText);
        Assert.Equal(PayAbnormalReasonEnum.OrderNotFound, item.Reason);
        Assert.Equal("无匹配订单", item.ReasonText);
    }

    [Fact]
    public async Task 端到端_订单过期后到账进台账_再人工关联到新单()
    {
        // 串起 F3.2（过期）→ F3.3/F5.1（终态后到账入台账）→ F5.2（人工关联到新单）
        // 这正是异常台账存在的意义：钱确实到了，但原订单已经收口，需要人来决定挂到哪里。
        var expireService = App.GetRequiredService<PayOrderExpireService>();

        // 1) 造一个已到期的待到账订单（预占 100）并让它过期
        var oldAccountId = SeedAccount(200m, locked: 100m);
        var oldOrder = SeedOrder(oldAccountId, 100m, 0m, PayOrderStatusEnum.Pending, -1);
        Assert.True(await expireService.ExpireOrderAsync(oldOrder.Id));
        Assert.Equal(PayOrderStatusEnum.Expired, GetOrder(oldOrder.Id).Status);

        // 2) 该订单的到账通知姗姗来迟 → 进异常台账，订单与额度都不动
        var voucher = VoucherPrefix + "e2e";
        var late = await _notifyService.Notify(new NotifyInput
        {
            OrderNo = oldOrder.OrderNo,
            Amount = 100m,
            VoucherNo = voucher,
            NotifyTime = DateTime.Now
        });
        Assert.Equal(PayConst.NotifyResultAbnormal, late.Result);

        var abnormal = _db.Queryable<PayAbnormalReceipt>().First(u => u.VoucherNo == voucher);
        Assert.Equal(PayAbnormalReasonEnum.OrderExpired, abnormal.Reason);
        Assert.Equal(PayHandleStatusEnum.Pending, abnormal.HandleStatus);
        Assert.Equal(0m, GetAccount(oldAccountId).UsedQuota);

        // 3) 重新下新单（业务上给同一客户重新生成收款单），预占 100
        var newAccountId = SeedAccount(200m, locked: 100m);
        var newOrder = SeedOrder(newAccountId, 100m, 0m, PayOrderStatusEnum.Pending);

        // 4) 人工把该笔到账挂到新单上
        var linked = await _abnormalService.Link(new LinkAbnormalInput
        {
            Id = abnormal.Id,
            TargetOrderNo = newOrder.OrderNo,
            HandleRemark = "原单已过期，经客户确认挂到新单"
        });

        Assert.Equal(PayOrderStatusEnum.Completed, linked.OrderStatus);
        Assert.Equal(100m, GetOrder(newOrder.Id).ReceivedAmount);
        Assert.Equal(100m, GetAccount(newAccountId).UsedQuota);
        Assert.Equal(0m, GetAccount(newAccountId).LockedQuota);

        // 原单保持终态、额度不受影响（终态不可逆）
        Assert.Equal(PayOrderStatusEnum.Expired, GetOrder(oldOrder.Id).Status);
        Assert.Equal(0m, GetAccount(oldAccountId).UsedQuota);
        Assert.Equal(0m, GetAccount(oldAccountId).LockedQuota);
    }
}
