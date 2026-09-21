// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Mvc;

namespace Admin.NET.Application;

/// <summary>
/// 收款数据导出（F7.5）
/// </summary>
/// <remarks>
/// <para>
/// <b>只读 + 留痕</b>：导出不修改任何业务数据，但导出本身是敏感动作（批量带走账号与金额），
/// 因此每次导出都往 <c>pay_audit_log</c> 追加一条 <see cref="PayAuditActionEnum.Export"/>：
/// 谁、什么时候、导了哪个区间、多少条。
/// </para>
/// <para>
/// <b>两道闸门防「一次拉全表」</b>：
/// <list type="number">
/// <item>时间区间不得超过 <see cref="PayConst.ExportMaxRangeDays"/> 天；</item>
/// <item>单次条数不得超过 <see cref="PayConst.ExportMaxRows"/> 条。</item>
/// </list>
/// 超限直接报错提示缩小范围，而不是让服务端悄悄把几百万行载入内存。
/// </para>
/// <para>
/// <b>导出文件是给人看的</b>：枚举一律导出中文描述（订单状态 / 异常原因 / 处理状态），
/// 时间列统一格式化，避免在 Excel 里出现一串枚举数字或 ISO 时间串。
/// </para>
/// </remarks>
[ApiDescriptionSettings(Order = 409, Description = "收款数据导出")]
public class PayExportService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayNotifyRecord> _payNotifyRecordRep;
    private readonly SqlSugarRepository<PayAbnormalReceipt> _payAbnormalReceiptRep;
    private readonly SqlSugarRepository<PayAuditLog> _payAuditLogRep;
    private readonly PayAuditService _payAuditService;

    public PayExportService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        SqlSugarRepository<PayAbnormalReceipt> payAbnormalReceiptRep,
        SqlSugarRepository<PayAuditLog> payAuditLogRep,
        PayAuditService payAuditService)
    {
        _payOrderRep = payOrderRep;
        _payAccountRep = payAccountRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payAbnormalReceiptRep = payAbnormalReceiptRep;
        _payAuditLogRep = payAuditLogRep;
        _payAuditService = payAuditService;
    }

    /// <summary>
    /// 导出收款订单（F7.5）
    /// </summary>
    /// <remarks>可选按账号 / 状态过滤，便于「某账号的订单」单独导出对账。</remarks>
    [ApiDescriptionSettings(Name = "ExportOrder"), HttpPost, NonUnify]
    [DisplayName("导出收款订单")]
    public async Task<IActionResult> ExportOrder(PayOrderExportInput input)
    {
        EnsureRange(input);

        var orders = await _payOrderRep.AsQueryable()
            .Where(u => u.CreateTime >= input.StartTime && u.CreateTime <= input.EndTime)
            .WhereIF(input.AccountId.HasValue, u => u.AccountId == input.AccountId.Value)
            .WhereIF(input.Status.HasValue, u => u.Status == input.Status.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Asc)
            .Take(PayConst.ExportMaxRows + 1)
            .ToListAsync();

        EnsureRowLimit(orders.Count, "订单");

        // 账号信息批量补齐（导出可能上万行，绝不做 N+1）
        var accountIds = orders.Select(u => u.AccountId).Where(u => u > 0).Distinct().ToList();
        var accountMap = accountIds.Count == 0
            ? new Dictionary<long, PayAccount>()
            : (await _payAccountRep.AsQueryable().Where(u => accountIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(u => u.Id);

        var data = orders.Select(u =>
        {
            var account = accountMap.GetValueOrDefault(u.AccountId);
            return new PayOrderExportDto
            {
                OrderNo = u.OrderNo,
                ExternalNo = u.ExternalNo,
                AccountType = account?.Type,
                AccountInfo = account?.AccountInfo,
                QrImageUrl = account?.QrImageUrl ?? "",
                RequestAmount = u.RequestAmount,
                ReceivedAmount = u.ReceivedAmount,
                OutstandingAmount = u.RequestAmount - u.ReceivedAmount,
                Status = u.Status.GetDescription(),
                CreateTime = u.CreateTime,
                ExpireTime = u.ExpireTime,
                CompleteTime = u.CompleteTime,
                OverpayRemark = u.OverpayRemark
            };
        }).ToList();

        if (data.Count == 0) throw Oops.Oh("该时间区间无订单数据可导出");

        await WriteExportAuditAsync("Order", data.Count, input);

        return new XlsxFileResult<PayOrderExportDto>(data, $"收款订单_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// 导出到账流水（F7.5）
    /// </summary>
    /// <remarks>含「是否已累加」列：异常到账入台账的记录为「否」，一眼能看出哪些钱没进订单。</remarks>
    [ApiDescriptionSettings(Name = "ExportNotify"), HttpPost, NonUnify]
    [DisplayName("导出到账流水")]
    public async Task<IActionResult> ExportNotify(PayExportInput input)
    {
        EnsureRange(input);

        var records = await _payNotifyRecordRep.AsQueryable()
            .Where(u => u.CreateTime >= input.StartTime && u.CreateTime <= input.EndTime)
            .OrderBy(u => u.CreateTime, OrderByType.Asc)
            .Take(PayConst.ExportMaxRows + 1)
            .ToListAsync();

        EnsureRowLimit(records.Count, "到账流水");

        var data = records.Select(u => new PayNotifyExportDto
        {
            OrderNo = u.OrderNo,
            Amount = u.Amount,
            NotifyTime = u.NotifyTime,
            VoucherNo = u.VoucherNo,
            ClientId = u.ClientId,
            AppliedText = u.Applied ? "是" : "否",
            CreateTime = u.CreateTime
        }).ToList();

        if (data.Count == 0) throw Oops.Oh("该时间区间无到账流水可导出");

        await WriteExportAuditAsync("NotifyRecord", data.Count, input);

        return new XlsxFileResult<PayNotifyExportDto>(data, $"收款到账流水_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// 导出异常到账台账（F7.5）
    /// </summary>
    [ApiDescriptionSettings(Name = "ExportAbnormal"), HttpPost, NonUnify]
    [DisplayName("导出异常到账台账")]
    public async Task<IActionResult> ExportAbnormal(PayExportInput input)
    {
        EnsureRange(input);

        var records = await _payAbnormalReceiptRep.AsQueryable()
            .Where(u => u.CreateTime >= input.StartTime && u.CreateTime <= input.EndTime)
            .OrderBy(u => u.CreateTime, OrderByType.Asc)
            .Take(PayConst.ExportMaxRows + 1)
            .ToListAsync();

        EnsureRowLimit(records.Count, "异常到账");

        var data = records.Select(u => new PayAbnormalExportDto
        {
            ReportOrderNo = u.ReportOrderNo,
            Amount = u.Amount,
            NotifyTime = u.NotifyTime,
            VoucherNo = u.VoucherNo,
            Reason = u.Reason.GetDescription(),
            HandleStatus = u.HandleStatus.GetDescription(),
            RelatedOrderNo = u.RelatedOrderNo,
            HandlerName = u.HandlerName,
            HandleTime = u.HandleTime,
            HandleRemark = u.HandleRemark
        }).ToList();

        if (data.Count == 0) throw Oops.Oh("该时间区间无异常到账数据可导出");

        await WriteExportAuditAsync("AbnormalReceipt", data.Count, input);

        return new XlsxFileResult<PayAbnormalExportDto>(data, $"异常到账台账_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// 导出业务审计日志（F7.3 / F7.5）
    /// </summary>
    /// <remarks>审计表只增不删，导出同样受区间与条数限制。</remarks>
    [ApiDescriptionSettings(Name = "ExportAuditLog"), HttpPost, NonUnify]
    [DisplayName("导出业务审计日志")]
    public async Task<IActionResult> ExportAuditLog(PayExportInput input)
    {
        EnsureRange(input);

        var logs = await _payAuditLogRep.AsQueryable()
            .Where(u => u.CreateTime >= input.StartTime && u.CreateTime <= input.EndTime)
            .OrderBy(u => u.CreateTime, OrderByType.Asc)
            .Take(PayConst.ExportMaxRows + 1)
            .ToListAsync();

        EnsureRowLimit(logs.Count, "审计日志");

        var data = logs.Select(u => new PayAuditLogExportDto
        {
            CreateTime = u.CreateTime,
            Action = u.Action.GetDescription(),
            TargetType = u.TargetType,
            TargetNo = u.TargetNo,
            BeforeJson = u.BeforeJson,
            AfterJson = u.AfterJson,
            Remark = u.Remark,
            OperatorName = u.OperatorName,
            OperatorIp = u.OperatorIp
        }).ToList();

        if (data.Count == 0) throw Oops.Oh("该时间区间无审计日志可导出");

        await WriteExportAuditAsync("AuditLog", data.Count, input);

        return new XlsxFileResult<PayAuditLogExportDto>(data, $"业务审计日志_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// 校验导出时间区间
    /// </summary>
    private static void EnsureRange(PayExportInput input)
    {
        if (input.StartTime > input.EndTime)
            throw Oops.Oh("开始时间不能晚于结束时间");

        if ((input.EndTime - input.StartTime).TotalDays > PayConst.ExportMaxRangeDays)
            throw Oops.Oh($"导出时间区间不能超过 {PayConst.ExportMaxRangeDays} 天，请缩小范围");
    }

    /// <summary>
    /// 校验导出条数上限
    /// </summary>
    /// <remarks>
    /// 查询时统一 <c>Take(MaxRows + 1)</c>：多取一条就能判断「是否超限」，
    /// 不必为了报错再跑一次 <c>Count</c>。多出来的那条不参与导出。
    /// </remarks>
    private static void EnsureRowLimit(int actualCount, string what)
    {
        if (actualCount > PayConst.ExportMaxRows)
            throw Oops.Oh($"{what}超过单次导出上限 {PayConst.ExportMaxRows} 条，请缩小时间范围");
    }

    /// <summary>
    /// 写一条导出审计（F7.5）
    /// </summary>
    private async Task WriteExportAuditAsync(string targetType, int rowCount, PayExportInput input)
    {
        await _payAuditService.WriteAsync(PayAuditActionEnum.Export,
            targetType,
            0,
            null,
            null,
            new { RowCount = rowCount, StartTime = input.StartTime, EndTime = input.EndTime },
            $"导出 {targetType}：{input.StartTime:yyyy-MM-dd HH:mm:ss} ~ {input.EndTime:yyyy-MM-dd HH:mm:ss}，共 {rowCount} 条");
    }
}
