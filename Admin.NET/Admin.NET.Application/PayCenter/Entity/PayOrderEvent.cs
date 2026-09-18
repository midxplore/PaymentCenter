// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 订单事件流水表（F7.1 / F7.4）
/// </summary>
/// <remarks>
/// 只增不改：本表不提供任何 update / delete 接口，只在业务事务内 append。
/// </remarks>
[SugarTable("pay_order_event", "订单事件流水表")]
[SysTable]
[SugarIndex("i_{table}_oid", nameof(OrderId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
public class PayOrderEvent : EntityBase
{
    /// <summary>
    /// 关联订单Id
    /// </summary>
    [SugarColumn(ColumnDescription = "订单Id")]
    public virtual long OrderId { get; set; }

    /// <summary>
    /// 关联订单号（冗余，便于排查）
    /// </summary>
    [SugarColumn(ColumnDescription = "订单号", Length = PayConst.OrderNoLength)]
    [MaxLength(PayConst.OrderNoLength)]
    public virtual string OrderNo { get; set; }

    /// <summary>
    /// 事件类型
    /// </summary>
    [SugarColumn(ColumnDescription = "事件类型")]
    public virtual PayEventTypeEnum EventType { get; set; }

    /// <summary>
    /// 变更前状态
    /// </summary>
    [SugarColumn(ColumnDescription = "变更前状态", IsNullable = true)]
    public virtual PayOrderStatusEnum? FromStatus { get; set; }

    /// <summary>
    /// 变更后状态
    /// </summary>
    [SugarColumn(ColumnDescription = "变更后状态", IsNullable = true)]
    public virtual PayOrderStatusEnum? ToStatus { get; set; }

    /// <summary>
    /// 本次事件涉及金额
    /// </summary>
    [SugarColumn(ColumnDescription = "涉及金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal Amount { get; set; }

    /// <summary>
    /// 事件发生后的累计到账金额
    /// </summary>
    [SugarColumn(ColumnDescription = "累计到账金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal ReceivedTotal { get; set; }

    /// <summary>
    /// 说明（如「超额 10.00 元」）
    /// </summary>
    [SugarColumn(ColumnDescription = "说明", Length = 512, IsNullable = true)]
    [MaxLength(512)]
    public virtual string Remark { get; set; }

    /// <summary>
    /// 操作人Id（系统事件为空）
    /// </summary>
    [SugarColumn(ColumnDescription = "操作人Id", IsNullable = true)]
    public virtual long? OperatorId { get; set; }

    /// <summary>
    /// 操作人姓名（系统事件为空）
    /// </summary>
    [SugarColumn(ColumnDescription = "操作人姓名", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string OperatorName { get; set; }
}
