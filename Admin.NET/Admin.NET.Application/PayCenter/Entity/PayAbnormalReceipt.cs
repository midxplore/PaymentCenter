// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 异常到账台账表（F5）
/// </summary>
[SugarTable("pay_abnormal_receipt", "异常到账台账表")]
[SysTable]
[SugarIndex("u_{table}_cv", nameof(ClientId), OrderByType.Asc, nameof(VoucherNo), OrderByType.Asc, IsUnique = true)]
[SugarIndex("i_{table}_hs", nameof(HandleStatus), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
public class PayAbnormalReceipt : EntityBase
{
    /// <summary>
    /// 到账金额
    /// </summary>
    [SugarColumn(ColumnDescription = "到账金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal Amount { get; set; }

    /// <summary>
    /// 到账时间（通知方传入）
    /// </summary>
    [SugarColumn(ColumnDescription = "到账时间")]
    public virtual DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号
    /// </summary>
    [SugarColumn(ColumnDescription = "凭证号", Length = 64)]
    [MaxLength(64)]
    public virtual string VoucherNo { get; set; }

    /// <summary>
    /// 通知方标识（SysOpenAccess.Id）
    /// </summary>
    [SugarColumn(ColumnDescription = "通知方Id")]
    public virtual long ClientId { get; set; }

    /// <summary>
    /// 通知方上报的订单号（可能不存在或已过期）
    /// </summary>
    [SugarColumn(ColumnDescription = "上报订单号", Length = PayConst.OrderNoLength, IsNullable = true)]
    [MaxLength(PayConst.OrderNoLength)]
    public virtual string ReportOrderNo { get; set; }

    /// <summary>
    /// 原始请求报文（可追溯）
    /// </summary>
    [SugarColumn(ColumnDescription = "原始请求报文", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public virtual string RawBody { get; set; }

    /// <summary>
    /// 异常原因
    /// </summary>
    [SugarColumn(ColumnDescription = "异常原因")]
    public virtual PayAbnormalReasonEnum Reason { get; set; }

    /// <summary>
    /// 人工补录关联后的订单Id
    /// </summary>
    [SugarColumn(ColumnDescription = "关联订单Id", IsNullable = true)]
    public virtual long? RelatedOrderId { get; set; }

    /// <summary>
    /// 人工补录关联后的订单号
    /// </summary>
    [SugarColumn(ColumnDescription = "关联订单号", Length = PayConst.OrderNoLength, IsNullable = true)]
    [MaxLength(PayConst.OrderNoLength)]
    public virtual string RelatedOrderNo { get; set; }

    /// <summary>
    /// 处理状态（F5.3）
    /// </summary>
    [SugarColumn(ColumnDescription = "处理状态")]
    public virtual PayHandleStatusEnum HandleStatus { get; set; } = PayHandleStatusEnum.Pending;

    /// <summary>
    /// 处理人Id
    /// </summary>
    [SugarColumn(ColumnDescription = "处理人Id", IsNullable = true)]
    public virtual long? HandlerId { get; set; }

    /// <summary>
    /// 处理人姓名
    /// </summary>
    [SugarColumn(ColumnDescription = "处理人姓名", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string HandlerName { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    [SugarColumn(ColumnDescription = "处理时间", IsNullable = true)]
    public virtual DateTime? HandleTime { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    [SugarColumn(ColumnDescription = "处理备注", Length = 256, IsNullable = true)]
    [MaxLength(256)]
    public virtual string HandleRemark { get; set; }
}
