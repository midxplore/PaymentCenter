// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Mvc;

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总（按日流水 + 日终快照）
/// </summary>
[ApiDescriptionSettings(Order = 408, Description = "运营汇总")]
public class PaySummaryService : IDynamicApiController, ITransient
{
    private const string Footnote =
        "预占发生按建单 Created 合计；过期重传重新预占暂不计入。过期释放=请求金额−已到账。日终列自快照作业启用日起可复盘。";

    private readonly SqlSugarRepository<PayOrderEvent> _eventRep;
    private readonly SqlSugarRepository<PayOrder> _orderRep;
    private readonly SqlSugarRepository<PayAuditLog> _auditRep;
    private readonly SqlSugarRepository<PayOpsDailySnapshot> _snapshotRep;
    private readonly PayOpsSnapshotService _snapshotService;
    private readonly PayAuditService _payAuditService;

    public PaySummaryService(
        SqlSugarRepository<PayOrderEvent> eventRep,
        SqlSugarRepository<PayOrder> orderRep,
        SqlSugarRepository<PayAuditLog> auditRep,
        SqlSugarRepository<PayOpsDailySnapshot> snapshotRep,
        PayOpsSnapshotService snapshotService,
        PayAuditService payAuditService)
    {
        _eventRep = eventRep;
        _orderRep = orderRep;
        _auditRep = auditRep;
        _snapshotRep = snapshotRep;
        _snapshotService = snapshotService;
        _payAuditService = payAuditService;
    }

    /// <summary>
    /// 按日运营汇总（含此刻池况）
    /// </summary>
    [ApiDescriptionSettings(Name = "Daily"), HttpPost]
    [DisplayName("运营汇总按日查询")]
    public async Task<PayOpsSummaryOutput> Daily(PayOpsDailyInput input)
    {
        var (start, end) = NormalizeRange(input);
        var days = await BuildDailyRowsAsync(start, end);
        var current = await _snapshotService.GetPoolNowAsync();
        return new PayOpsSummaryOutput
        {
            Current = current,
            Days = days,
            Footnote = Footnote
        };
    }

    /// <summary>
    /// 导出按日运营汇总
    /// </summary>
    [ApiDescriptionSettings(Name = "ExportDaily"), HttpPost, NonUnify]
    [DisplayName("导出运营汇总按日")]
    public async Task<IActionResult> ExportDaily(PayOpsDailyInput input)
    {
        var (start, end) = NormalizeRange(input);
        var days = await BuildDailyRowsAsync(start, end);
        if (days.Count == 0) throw Oops.Oh("该日期区间无汇总数据可导出");

        var data = days.Select(ToExportRow).ToList();
        await _payAuditService.WriteAsync(PayAuditActionEnum.Export,
            "OpsDaily",
            0,
            null,
            null,
            new { RowCount = data.Count, StartDate = start, EndDate = end },
            $"导出运营汇总按日：{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}，共 {data.Count} 行");

        return new XlsxFileResult<PayOpsDailyExportDto>(data, $"运营汇总按日_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    private static (DateTime start, DateTime end) NormalizeRange(PayOpsDailyInput input)
    {
        if (input == null) throw Oops.Bah("查询条件不能为空");
        var start = input.StartDate.Date;
        var end = input.EndDate.Date;
        if (start > end) throw Oops.Bah("开始日期不能晚于结束日期");
        if ((end - start).TotalDays > PayConst.ExportMaxRangeDays)
            throw Oops.Bah($"查询区间不能超过 {PayConst.ExportMaxRangeDays} 天");
        return (start, end);
    }

    private async Task<List<PayOpsDailyRowOutput>> BuildDailyRowsAsync(DateTime start, DateTime end)
    {
        var rangeStart = start;
        var rangeEndExclusive = end.AddDays(1);

        var events = await _eventRep.AsQueryable()
            .Where(u => u.CreateTime >= rangeStart && u.CreateTime < rangeEndExclusive)
            .Where(u => u.EventType == PayEventTypeEnum.Created
                || u.EventType == PayEventTypeEnum.Completed
                || u.EventType == PayEventTypeEnum.Expired
                || u.EventType == PayEventTypeEnum.MarkUnpaid)
            .Select(u => new
            {
                u.OrderId,
                u.EventType,
                u.Amount,
                u.ReceivedTotal,
                u.CreateTime
            })
            .ToListAsync();

        var expiredOrderIds = events
            .Where(u => u.EventType == PayEventTypeEnum.Expired)
            .Select(u => u.OrderId)
            .Distinct()
            .ToList();
        Dictionary<long, (decimal Request, decimal Received)> orderReqMap;
        if (expiredOrderIds.Count == 0)
        {
            orderReqMap = new Dictionary<long, (decimal Request, decimal Received)>();
        }
        else
        {
            var orderRows = await _orderRep.AsQueryable()
                .Where(u => expiredOrderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.RequestAmount, u.ReceivedAmount })
                .ToListAsync();
            orderReqMap = orderRows.ToDictionary(
                u => u.Id,
                u => (Request: u.RequestAmount, Received: u.ReceivedAmount));
        }

        var audits = await _auditRep.AsQueryable()
            .Where(u => u.CreateTime >= rangeStart && u.CreateTime < rangeEndExclusive)
            .Where(u => u.Action == PayAuditActionEnum.AccountAdd
                || u.Action == PayAuditActionEnum.AccountUpdate
                || u.Action == PayAuditActionEnum.QuotaAdd)
            .Select(u => new { u.Action, u.BeforeJson, u.AfterJson, u.CreateTime })
            .ToListAsync();

        var snapshots = await _snapshotRep.AsQueryable()
            .Where(u => u.BizDate >= start && u.BizDate <= end)
            .ToListAsync();
        var snapMap = snapshots.ToDictionary(u => u.BizDate.Date, u => u);

        var rows = new List<PayOpsDailyRowOutput>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var dayStart = d;
            var dayEnd = d.AddDays(1);
            var dayEvents = events.Where(u => u.CreateTime >= dayStart && u.CreateTime < dayEnd).ToList();
            var dayAudits = audits.Where(u => u.CreateTime >= dayStart && u.CreateTime < dayEnd).ToList();

            decimal inject = 0m, reclaim = 0m;
            foreach (var a in dayAudits.Where(u =>
                         u.Action == PayAuditActionEnum.AccountAdd || u.Action == PayAuditActionEnum.QuotaAdd))
            {
                var delta = PayOpsMetrics.TryParseTotalQuotaDelta(a.BeforeJson, a.AfterJson);
                if (delta == null) continue;
                if (delta.Value >= 0m) inject += delta.Value;
                else reclaim += -delta.Value;
            }

            var lockOccurred = dayEvents
                .Where(u => u.EventType == PayEventTypeEnum.Created)
                .Sum(u => u.Amount);

            var completed = dayEvents.Where(u => u.EventType == PayEventTypeEnum.Completed).ToList();
            var receivedCompleted = completed.Sum(u => u.ReceivedTotal);
            var completedCount = completed.Count;

            decimal expireReleased = 0m;
            foreach (var ev in dayEvents.Where(u => u.EventType == PayEventTypeEnum.Expired))
            {
                if (orderReqMap.TryGetValue(ev.OrderId, out var amt))
                    expireReleased += PayOpsMetrics.ExpireReleased(amt.Request, amt.Received);
                else
                    // 订单已删等极端情况：用事件 ReceivedTotal 无法还原请求，记 0
                    expireReleased += 0m;
            }

            var row = new PayOpsDailyRowOutput
            {
                BizDate = d,
                QuotaInject = inject,
                QuotaReclaim = reclaim,
                QuotaNet = inject - reclaim,
                LockOccurred = lockOccurred,
                ReceivedCompleted = receivedCompleted,
                CompletedCount = completedCount,
                ExpireReleased = expireReleased,
                MarkUnpaidCount = dayEvents.Count(u => u.EventType == PayEventTypeEnum.MarkUnpaid),
                AccountAddCount = dayAudits.Count(u => u.Action == PayAuditActionEnum.AccountAdd),
                QuotaAdjustCount = dayAudits.Count(u => u.Action == PayAuditActionEnum.QuotaAdd),
                AccountUpdateCount = dayAudits.Count(u => u.Action == PayAuditActionEnum.AccountUpdate),
            };

            if (snapMap.TryGetValue(d, out var snap))
            {
                row.HasSnapshot = true;
                row.EodTotalQuotaSum = snap.TotalQuotaSum;
                row.EodUsedQuotaSum = snap.UsedQuotaSum;
                row.EodLockedQuotaSum = snap.LockedQuotaSum;
                row.EodRemainingQuotaSum = snap.TotalQuotaSum - snap.UsedQuotaSum - snap.LockedQuotaSum;
                row.EodUnpaidLockedSum = snap.UnpaidLockedSum;
                row.EodAccountEnabledCount = snap.AccountEnabledCount;
            }

            rows.Add(row);
        }

        rows.Reverse(); // 新→旧
        return rows;
    }

    private static PayOpsDailyExportDto ToExportRow(PayOpsDailyRowOutput u) => new()
    {
        BizDate = u.BizDate.ToString("yyyy-MM-dd"),
        QuotaInject = u.QuotaInject,
        QuotaReclaim = u.QuotaReclaim,
        QuotaNet = u.QuotaNet,
        LockOccurred = u.LockOccurred,
        ReceivedCompleted = u.ReceivedCompleted,
        CompletedCount = u.CompletedCount,
        ExpireReleased = u.ExpireReleased,
        MarkUnpaidCount = u.MarkUnpaidCount,
        AccountAddCount = u.AccountAddCount,
        QuotaAdjustCount = u.QuotaAdjustCount,
        AccountUpdateCount = u.AccountUpdateCount,
        EodTotalQuotaSum = FormatOpt(u.EodTotalQuotaSum),
        EodUsedQuotaSum = FormatOpt(u.EodUsedQuotaSum),
        EodLockedQuotaSum = FormatOpt(u.EodLockedQuotaSum),
        EodRemainingQuotaSum = FormatOpt(u.EodRemainingQuotaSum),
        EodUnpaidLockedSum = FormatOpt(u.EodUnpaidLockedSum),
        EodAccountEnabledCount = u.EodAccountEnabledCount?.ToString() ?? "—"
    };

    private static string FormatOpt(decimal? v) => v.HasValue ? v.Value.ToString("0.00") : "—";
}
