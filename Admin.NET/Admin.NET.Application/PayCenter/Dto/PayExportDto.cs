// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Excel;

namespace Admin.NET.Application;

/// <summary>
/// 导出时间区间输入（F7.5）
/// </summary>
public class PayExportInput
{
    /// <summary>
    /// 开始时间（含）
    /// </summary>
    [Required(ErrorMessage = "开始时间不能为空")]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间（含）
    /// </summary>
    [Required(ErrorMessage = "结束时间不能为空")]
    public DateTime EndTime { get; set; }
}

/// <summary>
/// 订单导出输入（F7.5）
/// </summary>
/// <remarks>
/// 在时间区间之上再加两个可选过滤，导出「某账号的订单」或「某状态的订单」。
/// </remarks>
public class PayOrderExportInput : PayExportInput
{
    /// <summary>
    /// 收款账号Id（可选，F7.2 账号维度）
    /// </summary>
    public long? AccountId { get; set; }

    /// <summary>
    /// 订单状态（可选）
    /// </summary>
    public PayOrderStatusEnum? Status { get; set; }
}

/// <summary>
/// 订单导出数据（F7.5）
/// </summary>
/// <remarks>
/// 字段顺序 = 对账时的阅读顺序：先定位（订单号 / 外部单号 / 账号），再看金额，最后看时间。
/// 状态导出为<b>中文描述</b>而不是枚举值——导出文件是给人看的，给三方对账也直接可读。
/// </remarks>
[ExcelExporter(Name = "收款订单", AutoFitAllColumn = true)]
public class PayOrderExportDto
{
    /// <summary>
    /// 订单号
    /// </summary>
    [ExporterHeader(DisplayName = "订单号", IsBold = true)]
    public string OrderNo { get; set; }

    /// <summary>
    /// 外部业务单号
    /// </summary>
    [ExporterHeader(DisplayName = "外部业务单号")]
    public string ExternalNo { get; set; }

    /// <summary>
    /// 收款类型
    /// </summary>
    [ExporterHeader(DisplayName = "收款类型")]
    public string AccountType { get; set; }

    /// <summary>
    /// 收款账号
    /// </summary>
    [ExporterHeader(DisplayName = "收款账号")]
    public string AccountInfo { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    [ExporterHeader(DisplayName = "请求金额")]
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 累计到账金额
    /// </summary>
    [ExporterHeader(DisplayName = "累计到账金额")]
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 未达成金额（负数表示超额到账）
    /// </summary>
    [ExporterHeader(DisplayName = "未达成金额")]
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    [ExporterHeader(DisplayName = "订单状态")]
    public string Status { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [ExporterHeader(DisplayName = "创建时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [ExporterHeader(DisplayName = "过期时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime ExpireTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [ExporterHeader(DisplayName = "完成时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime? CompleteTime { get; set; }

    /// <summary>
    /// 超额到账备注（F4.4）
    /// </summary>
    [ExporterHeader(DisplayName = "超额备注")]
    public string OverpayRemark { get; set; }
}

/// <summary>
/// 到账流水导出数据（F7.5）
/// </summary>
[ExcelExporter(Name = "到账流水", AutoFitAllColumn = true)]
public class PayNotifyExportDto
{
    /// <summary>
    /// 订单号
    /// </summary>
    [ExporterHeader(DisplayName = "订单号", IsBold = true)]
    public string OrderNo { get; set; }

    /// <summary>
    /// 本次到账金额
    /// </summary>
    [ExporterHeader(DisplayName = "本次到账金额")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 到账时间（通知方传入）
    /// </summary>
    [ExporterHeader(DisplayName = "到账时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号
    /// </summary>
    [ExporterHeader(DisplayName = "凭证号")]
    public string VoucherNo { get; set; }

    /// <summary>
    /// 通知方标识
    /// </summary>
    [ExporterHeader(DisplayName = "通知方Id")]
    public long ClientId { get; set; }

    /// <summary>
    /// 是否已累加到订单
    /// </summary>
    [ExporterHeader(DisplayName = "是否已累加")]
    public string AppliedText { get; set; }

    /// <summary>
    /// 接收时间
    /// </summary>
    [ExporterHeader(DisplayName = "接收时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 异常到账台账导出数据（F7.5）
/// </summary>
[ExcelExporter(Name = "异常到账台账", AutoFitAllColumn = true)]
public class PayAbnormalExportDto
{
    /// <summary>
    /// 上报订单号
    /// </summary>
    [ExporterHeader(DisplayName = "上报订单号", IsBold = true)]
    public string ReportOrderNo { get; set; }

    /// <summary>
    /// 到账金额
    /// </summary>
    [ExporterHeader(DisplayName = "到账金额")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 到账时间
    /// </summary>
    [ExporterHeader(DisplayName = "到账时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号
    /// </summary>
    [ExporterHeader(DisplayName = "凭证号")]
    public string VoucherNo { get; set; }

    /// <summary>
    /// 异常原因
    /// </summary>
    [ExporterHeader(DisplayName = "异常原因")]
    public string Reason { get; set; }

    /// <summary>
    /// 处理状态
    /// </summary>
    [ExporterHeader(DisplayName = "处理状态")]
    public string HandleStatus { get; set; }

    /// <summary>
    /// 关联订单号
    /// </summary>
    [ExporterHeader(DisplayName = "关联订单号")]
    public string RelatedOrderNo { get; set; }

    /// <summary>
    /// 处理人
    /// </summary>
    [ExporterHeader(DisplayName = "处理人")]
    public string HandlerName { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    [ExporterHeader(DisplayName = "处理时间", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime? HandleTime { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    [ExporterHeader(DisplayName = "处理备注")]
    public string HandleRemark { get; set; }
}

/// <summary>
/// 业务审计日志导出数据（F7.3）
/// </summary>
[ExcelExporter(Name = "业务审计日志", AutoFitAllColumn = true)]
public class PayAuditLogExportDto
{
    /// <summary>
    /// 操作时间
    /// </summary>
    [ExporterHeader(DisplayName = "操作时间", IsBold = true, Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 动作
    /// </summary>
    [ExporterHeader(DisplayName = "动作")]
    public string Action { get; set; }

    /// <summary>
    /// 目标类型
    /// </summary>
    [ExporterHeader(DisplayName = "目标类型")]
    public string TargetType { get; set; }

    /// <summary>
    /// 目标标识
    /// </summary>
    [ExporterHeader(DisplayName = "目标标识")]
    public string TargetNo { get; set; }

    /// <summary>
    /// 变更前
    /// </summary>
    [ExporterHeader(DisplayName = "变更前")]
    public string BeforeJson { get; set; }

    /// <summary>
    /// 变更后
    /// </summary>
    [ExporterHeader(DisplayName = "变更后")]
    public string AfterJson { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    [ExporterHeader(DisplayName = "说明")]
    public string Remark { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [ExporterHeader(DisplayName = "操作人")]
    public string OperatorName { get; set; }

    /// <summary>
    /// 操作IP
    /// </summary>
    [ExporterHeader(DisplayName = "操作IP")]
    public string OperatorIp { get; set; }
}
