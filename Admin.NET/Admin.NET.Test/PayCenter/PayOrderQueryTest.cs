// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 后台订单查询（F7.1 / F7.2）
/// </summary>
/// <remarks>
/// <para>
/// 本类要钉住的是<b>两个审计视角</b>与<b>字段口径一致</b>：
/// <list type="bullet">
/// <item>订单维度：按订单号 / 外部单号 / 状态 / 时间过滤；</item>
/// <item>账号维度（F7.2）：传 <c>AccountId</c> 看该账号名下订单明细；</item>
/// <item>详情必须一次给全三张表（订单 + 事件流水 + 到账明细）；</item>
/// <item><c>Page</c> 与 <c>Detail</c> 的状态文本 / 未达成金额必须同口径
/// （这两处各写一遍必然随时间漂移，所以实现里收敛到了同一个方法）。</item>
/// </list>
/// </para>
/// <para>造数用独立账号类型与前缀，构造与析构各清理一次（绕过软删除过滤器）。</para>
/// </remarks>
public class PayOrderQueryTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_orderquery";

    /// <summary>本类专用订单号前缀</summary>
    private const string OrderNoPrefix = "UTOQ";

    /// <summary>本类专用外部单号前缀</summary>
    private const string ExternalNoPrefix = "UTOQ-E-";

    /// <summary>本类专用凭证号前缀</summary>
    private const string VoucherPrefix = "UTOQ-V-";

    private readonly ISqlSugarClient _db;
    private readonly PayOrderService _orderService;

    public PayOrderQueryTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _orderService = App.GetRequiredService<PayOrderService>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数（事件/通知 → 订单 → 账号，顺序不能颠倒）</summary>
    private void Cleanup()
    {
        _db.Ado.ExecuteCommand("delete from pay_order_event where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_notify_record where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long SeedAccount()
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = "unittest-orderquery-account",
            TotalQuota = 1000m,
            Status = PayAccountStatusEnum.Enabled,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private PayOrder SeedOrder(long accountId, string suffix, decimal request, decimal received = 0m,
        PayOrderStatusEnum status = PayOrderStatusEnum.Pending)
    {
        var order = new PayOrder
        {
            OrderNo = OrderNoPrefix + suffix,
            ExternalNo = ExternalNoPrefix + suffix,
            RequestAmount = request,
            AccountId = accountId,
            ReceivedAmount = received,
            Status = status,
            ExpireTime = DateTime.Now.AddMinutes(30),
            ClientId = 0
        };
        _db.Insertable(order).ExecuteCommand();
        return order;
    }

    private void SeedEvent(PayOrder order, PayEventTypeEnum type, PayOrderStatusEnum? from,
        PayOrderStatusEnum? to, decimal amount, decimal receivedTotal, string remark)
    {
        _db.Insertable(new PayOrderEvent
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            EventType = type,
            FromStatus = from,
            ToStatus = to,
            Amount = amount,
            ReceivedTotal = receivedTotal,
            Remark = remark
        }).ExecuteCommand();
    }

    private void SeedNotify(PayOrder order, decimal amount, string voucherNo, bool applied)
    {
        _db.Insertable(new PayNotifyRecord
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Amount = amount,
            NotifyTime = DateTime.Now,
            VoucherNo = voucherNo,
            ClientId = 0,
            Applied = applied
        }).ExecuteCommand();
    }

    [Fact]
    public async Task 分页_按订单号模糊匹配()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "0001", 100m);
        SeedOrder(accountId, "0002", 100m);
        SeedOrder(accountId, "9999", 100m);

        var paged = await _orderService.Page(new PagePayOrderInput
        {
            OrderNo = OrderNoPrefix + "000",
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(2, paged.Total);
        Assert.All(paged.Items, u => Assert.StartsWith(OrderNoPrefix + "000", u.OrderNo));
    }

    [Fact]
    public async Task 分页_账号维度过滤_应只返回该账号的订单()
    {
        // F7.2 账号维度审计：这是「查某个收款账号收了多少钱、都是哪些单」的唯一入口
        var accountA = SeedAccount();
        var accountB = SeedAccount();
        SeedOrder(accountA, "A001", 10m);
        SeedOrder(accountA, "A002", 20m);
        SeedOrder(accountB, "B001", 30m);

        var paged = await _orderService.Page(new PagePayOrderInput
        {
            AccountId = accountA,
            Page = 1,
            PageSize = 50
        });

        Assert.Equal(2, paged.Total);
        Assert.All(paged.Items, u => Assert.Equal(accountA, u.AccountId));
        Assert.Equal(30m, paged.Items.Sum(u => u.RequestAmount));
    }

    [Fact]
    public async Task 分页_按状态过滤()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "P001", 10m, 0m, PayOrderStatusEnum.Pending);
        SeedOrder(accountId, "C001", 10m, 10m, PayOrderStatusEnum.Completed);
        SeedOrder(accountId, "E001", 10m, 0m, PayOrderStatusEnum.Expired);

        var paged = await _orderService.Page(new PagePayOrderInput
        {
            AccountId = accountId,
            Status = PayOrderStatusEnum.Expired,
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(1, paged.Total);
        Assert.Equal(OrderNoPrefix + "E001", paged.Items.First().OrderNo);
    }

    [Fact]
    public async Task 分页_应补齐账号信息与计算字段()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "F001", 100m, 40m, PayOrderStatusEnum.Partial);

        var paged = await _orderService.Page(new PagePayOrderInput
        {
            AccountId = accountId,
            Page = 1,
            PageSize = 10
        });

        var item = paged.Items.Single();
        Assert.Equal("unittest-orderquery-account", item.AccountInfo);
        Assert.Equal(TestType, item.AccountType);
        // 未达成金额 = 请求 − 已到账
        Assert.Equal(60m, item.OutstandingAmount);
        Assert.Equal("部分到账", item.StatusText);
    }

    [Fact]
    public async Task 分页_超额到账时未达成金额应为负数()
    {
        // 如实呈现多收，不截断为 0——否则对账时看不出超额
        var accountId = SeedAccount();
        SeedOrder(accountId, "O001", 100m, 130m, PayOrderStatusEnum.Completed);

        var paged = await _orderService.Page(new PagePayOrderInput
        {
            AccountId = accountId,
            Page = 1,
            PageSize = 10
        });

        Assert.Equal(-30m, paged.Items.Single().OutstandingAmount);
    }

    [Fact]
    public async Task 详情_应一次返回订单与事件流水与到账明细()
    {
        var accountId = SeedAccount();
        var order = SeedOrder(accountId, "D001", 100m, 60m, PayOrderStatusEnum.Partial);
        SeedEvent(order, PayEventTypeEnum.Created, null, PayOrderStatusEnum.Pending, 0m, 0m, "订单创建");
        SeedEvent(order, PayEventTypeEnum.PartialReceived, PayOrderStatusEnum.Pending,
            PayOrderStatusEnum.Partial, 60m, 60m, "部分到账 60.00");
        SeedNotify(order, 60m, VoucherPrefix + "001", true);

        var detail = await _orderService.Detail(new BaseIdInput { Id = order.Id });

        Assert.Equal(OrderNoPrefix + "D001", detail.Order.OrderNo);
        Assert.Equal("部分到账", detail.Order.StatusText);
        Assert.Equal(2, detail.Events.Count);
        // 事件按时间升序：创建在前、部分到账在后
        Assert.Equal(PayEventTypeEnum.Created, detail.Events[0].EventType);
        Assert.Equal(PayEventTypeEnum.PartialReceived, detail.Events[1].EventType);
        Assert.Equal("部分到账", detail.Events[1].ToStatusText);
        Assert.Single(detail.NotifyRecords);
        Assert.Equal(VoucherPrefix + "001", detail.NotifyRecords[0].VoucherNo);
        Assert.True(detail.NotifyRecords[0].Applied);
    }

    [Fact]
    public async Task 详情_Page与Detail的状态口径必须一致()
    {
        // 这条守的是「两处各写一遍必然漂移」：实现里收敛到了同一个 ToOutput。
        var accountId = SeedAccount();
        var order = SeedOrder(accountId, "S001", 100m, 40m, PayOrderStatusEnum.Partial);

        var fromPage = (await _orderService.Page(new PagePayOrderInput
        {
            AccountId = accountId,
            Page = 1,
            PageSize = 10
        })).Items.Single();
        var fromDetail = (await _orderService.Detail(new BaseIdInput { Id = order.Id })).Order;

        Assert.Equal(fromPage.StatusText, fromDetail.StatusText);
        Assert.Equal(fromPage.OutstandingAmount, fromDetail.OutstandingAmount);
        Assert.Equal(fromPage.AccountInfo, fromDetail.AccountInfo);
    }

    [Fact]
    public async Task 按订单号查详情_不存在应报错()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _orderService.DetailByNo(OrderNoPrefix + "NOPE"));
    }

    [Fact]
    public async Task 详情_订单不存在应报错()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _orderService.Detail(new BaseIdInput { Id = 999999999999 }));
    }

    [Fact]
    public void 状态下拉_应覆盖全部枚举且带中文描述()
    {
        var options = _orderService.GetStatusOptions();

        Assert.Equal(Enum.GetValues<PayOrderStatusEnum>().Length, options.Count);
        Assert.All(options, u => Assert.False(string.IsNullOrWhiteSpace(u.Label)));
        Assert.Contains(options, u => u.Value == (int)PayOrderStatusEnum.Expired && u.Label == "已过期");
    }
}
