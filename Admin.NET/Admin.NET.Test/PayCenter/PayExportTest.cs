// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;
using Microsoft.AspNetCore.Mvc;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 收款数据导出（F7.5）
/// </summary>
/// <remarks>
/// <para>
/// 直接调服务、不造 HTTP 管道：导出返回的是 <c>XlsxFileResult&lt;T&gt;</c>，
/// 它把待导出的行挂在 <c>Data</c> 上、文件名挂在 <c>FileDownloadName</c> 上，
/// 这两样就能覆盖「导了什么、导了多少、叫什么名」的全部断言，
/// 不必真去解析 xlsx 二进制（那测的是 Magicodes 而不是我们的逻辑）。
/// </para>
/// <para>
/// <b>确定性与非确定性断言的取舍</b>：
/// <list type="bullet">
/// <item>订单导出可按 <c>AccountId</c> 过滤 → 用<b>精确条数</b>断言；</item>
/// <item>流水 / 台账 / 审计日志没有账号维度，时间窗内可能有别的用例并发写入 →
/// 只断言<b>包含本类造数</b>，不断言总条数（否则就是给未来的自己埋随机失败）。</item>
/// </list>
/// </para>
/// <para>造数用独立账号类型与前缀，构造与析构各清理一次。</para>
/// </remarks>
public class PayExportTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_payexport";

    /// <summary>本类专用订单号前缀</summary>
    private const string OrderNoPrefix = "UTEX";

    /// <summary>本类专用外部单号前缀</summary>
    private const string ExternalNoPrefix = "UTEX-E-";

    /// <summary>本类专用凭证号前缀</summary>
    private const string VoucherPrefix = "UTEX-V-";

    private readonly ISqlSugarClient _db;
    private readonly PayExportService _exportService;

    /// <summary>本次用例的时间窗起点（造数都在它之后）</summary>
    private readonly DateTime _windowStart = DateTime.Now.AddMinutes(-1);

    public PayExportTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _exportService = App.GetRequiredService<PayExportService>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数</summary>
    /// <remarks>
    /// 审计表按 <c>action = Export</c> 清：只有导出会产生该动作，清掉不会碰到别的用例的审计。
    /// </remarks>
    private void Cleanup()
    {
        _db.Ado.ExecuteCommand("delete from pay_order_event where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_notify_record where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_abnormal_receipt where voucherno like @prefix",
            new SugarParameter("@prefix", VoucherPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where orderno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
        _db.Ado.ExecuteCommand("delete from pay_audit_log where action = @action",
            new SugarParameter("@action", (int)PayAuditActionEnum.Export));
    }

    private long SeedAccount()
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = "unittest-payexport-account",
            TotalQuota = 1000m,
            Status = PayAccountStatusEnum.Enabled,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private void SeedOrder(long accountId, string suffix, decimal request, decimal received)
    {
        _db.Insertable(new PayOrder
        {
            OrderNo = OrderNoPrefix + suffix,
            ExternalNo = ExternalNoPrefix + suffix,
            RequestAmount = request,
            AccountId = accountId,
            ReceivedAmount = received,
            Status = received >= request ? PayOrderStatusEnum.Completed : PayOrderStatusEnum.Partial,
            ExpireTime = DateTime.Now.AddMinutes(30),
            ClientId = 0
        }).ExecuteCommand();
    }

    /// <summary>把导出结果里的行取出来（避免在断言里到处写强制转换）</summary>
    private static List<T> RowsOf<T>(IActionResult result) where T : class, new()
    {
        var file = Assert.IsType<XlsxFileResult<T>>(result);
        Assert.False(string.IsNullOrWhiteSpace(file.FileDownloadName));
        Assert.EndsWith(".xlsx", file.FileDownloadName);
        return file.Data.ToList();
    }

    [Fact]
    public async Task 导出订单_应只含指定账号的订单且字段完整()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "001", 100m, 100m);
        SeedOrder(accountId, "002", 50m, 20m);

        var rows = RowsOf<PayOrderExportDto>(await _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1),
            AccountId = accountId
        }));

        Assert.Equal(2, rows.Count);
        var first = rows.Single(u => u.OrderNo == OrderNoPrefix + "001");
        Assert.Equal(ExternalNoPrefix + "001", first.ExternalNo);
        Assert.Equal(TestType, first.AccountType);
        Assert.Equal("unittest-payexport-account", first.AccountInfo);
        Assert.Equal(100m, first.RequestAmount);
        Assert.Equal(100m, first.ReceivedAmount);
        Assert.Equal(0m, first.OutstandingAmount);
        // 状态导出为中文描述（导出文件是给人看的）
        Assert.Equal("已完成", first.Status);

        // 部分到账那笔：未达成金额 = 50 − 20
        var second = rows.Single(u => u.OrderNo == OrderNoPrefix + "002");
        Assert.Equal(30m, second.OutstandingAmount);
        Assert.Equal("部分到账", second.Status);
    }

    [Fact]
    public async Task 导出订单_可按状态过滤()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "C01", 100m, 100m);
        SeedOrder(accountId, "P01", 100m, 10m);

        var rows = RowsOf<PayOrderExportDto>(await _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1),
            AccountId = accountId,
            Status = PayOrderStatusEnum.Completed
        }));

        Assert.Single(rows);
        Assert.Equal(OrderNoPrefix + "C01", rows[0].OrderNo);
    }

    [Fact]
    public async Task 导出订单_区间内无数据应报错而不是产出空文件()
    {
        // 远古区间必然为空：空文件对用户是「以为导了但什么都没有」，必须显式报错
        await Assert.ThrowsAnyAsync<Exception>(() => _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = new DateTime(2000, 1, 1),
            EndTime = new DateTime(2000, 1, 2)
        }));
    }

    [Fact]
    public async Task 导出_开始时间晚于结束时间应报错()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = DateTime.Now,
            EndTime = DateTime.Now.AddDays(-1)
        }));
    }

    [Fact]
    public async Task 导出_区间超过上限应报错()
    {
        // 区间上限存在的意义：导出不分页，区间开太大就是一次全表扫描 + 全量载入内存
        await Assert.ThrowsAnyAsync<Exception>(() => _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = DateTime.Now.AddDays(-(PayConst.ExportMaxRangeDays + 1)),
            EndTime = DateTime.Now
        }));
    }

    [Fact]
    public async Task 导出订单_应写入一条导出审计()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "A01", 10m, 0m);

        await _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1),
            AccountId = accountId
        });

        // 导出是敏感动作（批量带走账号与金额），必须留痕
        var logs = _db.Queryable<PayAuditLog>()
            .Where(u => u.Action == PayAuditActionEnum.Export)
            .ToList();

        Assert.Single(logs);
        Assert.Equal("Order", logs[0].TargetType);
        Assert.Contains("共 1 条", logs[0].Remark);
        Assert.Contains("RowCount", logs[0].AfterJson);
    }

    [Fact]
    public async Task 导出到账流水_应含本类造数且区分是否已累加()
    {
        var accountId = SeedAccount();
        var order = new PayOrder
        {
            OrderNo = OrderNoPrefix + "N01",
            ExternalNo = ExternalNoPrefix + "N01",
            RequestAmount = 100m,
            AccountId = accountId,
            Status = PayOrderStatusEnum.Pending,
            ExpireTime = DateTime.Now.AddMinutes(30),
            ClientId = 0
        };
        _db.Insertable(order).ExecuteCommand();

        _db.Insertable(new PayNotifyRecord
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            Amount = 60m,
            NotifyTime = DateTime.Now,
            VoucherNo = VoucherPrefix + "001",
            ClientId = 0,
            Applied = true
        }).ExecuteCommand();
        _db.Insertable(new PayNotifyRecord
        {
            OrderId = 0,
            OrderNo = order.OrderNo,
            Amount = 40m,
            NotifyTime = DateTime.Now,
            VoucherNo = VoucherPrefix + "002",
            ClientId = 0,
            Applied = false
        }).ExecuteCommand();

        var rows = RowsOf<PayNotifyExportDto>(await _exportService.ExportNotify(new PayExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1)
        }));

        var applied = rows.Single(u => u.VoucherNo == VoucherPrefix + "001");
        Assert.Equal("是", applied.AppliedText);
        var notApplied = rows.Single(u => u.VoucherNo == VoucherPrefix + "002");
        Assert.Equal("否", notApplied.AppliedText);
    }

    [Fact]
    public async Task 导出异常到账台账_应含本类造数且枚举导出为中文()
    {
        _db.Insertable(new PayAbnormalReceipt
        {
            Amount = 88m,
            NotifyTime = DateTime.Now,
            VoucherNo = VoucherPrefix + "A01",
            ClientId = 0,
            ReportOrderNo = OrderNoPrefix + "MISS",
            Reason = PayAbnormalReasonEnum.OrderNotFound,
            HandleStatus = PayHandleStatusEnum.Pending
        }).ExecuteCommand();

        var rows = RowsOf<PayAbnormalExportDto>(await _exportService.ExportAbnormal(new PayExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1)
        }));

        var row = rows.Single(u => u.VoucherNo == VoucherPrefix + "A01");
        Assert.Equal(88m, row.Amount);
        Assert.Equal("无匹配订单", row.Reason);
        Assert.Equal("待处理", row.HandleStatus);
    }

    [Fact]
    public async Task 导出业务审计日志_应含刚写入的导出审计()
    {
        var accountId = SeedAccount();
        SeedOrder(accountId, "L01", 10m, 0m);
        await _exportService.ExportOrder(new PayOrderExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1),
            AccountId = accountId
        });

        var rows = RowsOf<PayAuditLogExportDto>(await _exportService.ExportAuditLog(new PayExportInput
        {
            StartTime = _windowStart,
            EndTime = DateTime.Now.AddMinutes(1)
        }));

        // 上一步的导出审计应当出现在审计导出里（自证闭环）
        Assert.Contains(rows, u => u.Action == "数据导出" && u.TargetType == "Order");
    }
}
