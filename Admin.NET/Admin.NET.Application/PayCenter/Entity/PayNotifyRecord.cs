// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 到账通知记录表（F7.1）
/// </summary>
/// <remarks>
/// 只增不改：本表不提供任何 update / delete <b>接口</b>。
/// 唯一索引 (ClientId, VoucherNo) 是通知去重（F4.5）的最终防线。
/// <para>
/// 例外：<see cref="OrderId"/> 与 <see cref="Applied"/> 两个字段会在写入它的同一次业务事务内回填
/// ——因为去重必须"先插后判"（靠 DB 唯一索引抗并发），而订单Id与是否累加要到之后才知道。
/// 这两个字段是**派生状态**而非业务数据，回填是必要且安全的；
/// 除此之外本表不做任何就地修改（人工关联 §5.4 也只允许把 <see cref="Applied"/> 置为 true）。
/// </para>
/// </remarks>
[SugarTable("pay_notify_record", "到账通知记录表")]
[SysTable]
[SugarIndex("u_{table}_cv", nameof(ClientId), OrderByType.Asc, nameof(VoucherNo), OrderByType.Asc, IsUnique = true)]
[SugarIndex("i_{table}_oid", nameof(OrderId), OrderByType.Asc)]
public class PayNotifyRecord : EntityBase
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
    /// 本次到账金额
    /// </summary>
    [SugarColumn(ColumnDescription = "到账金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal Amount { get; set; }

    /// <summary>
    /// 到账时间（通知方传入）
    /// </summary>
    [SugarColumn(ColumnDescription = "到账时间")]
    public virtual DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号（去重键）
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
    /// 原始请求报文（F7.1 可追溯）
    /// </summary>
    [SugarColumn(ColumnDescription = "原始请求报文", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true)]
    public virtual string RawBody { get; set; }

    /// <summary>
    /// 是否已实际累加到订单（异常到账入台账时为 false）
    /// </summary>
    [SugarColumn(ColumnDescription = "是否已累加")]
    public virtual bool Applied { get; set; }
}
