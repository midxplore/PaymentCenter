// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总日终快照（额度池存量，按自然日 +08）
/// </summary>
/// <remarks>
/// 每日午夜后作业写入「昨日」日终；同 BizDate 仅首次写入、不改历史。
/// 流水指标仍按事件/审计实时聚合，本表只存存量。
/// </remarks>
[SugarTable("pay_ops_daily_snapshot", "运营汇总日终快照")]
[SysTable]
[IncreTable]
[SugarIndex("u_{table}_bizdate", nameof(BizDate), OrderByType.Asc, IsUnique = true)]
public class PayOpsDailySnapshot : EntityBase
{
    /// <summary>
    /// 业务日（自然日，不含时分秒）
    /// </summary>
    [SugarColumn(ColumnDescription = "业务日", ColumnDataType = "date")]
    public virtual DateTime BizDate { get; set; }

    /// <summary>
    /// 日终：全池总额度合计
    /// </summary>
    [SugarColumn(ColumnDescription = "日终总额度", Length = 18, DecimalDigits = 2)]
    public virtual decimal TotalQuotaSum { get; set; }

    /// <summary>
    /// 日终：全池已用合计
    /// </summary>
    [SugarColumn(ColumnDescription = "日终已用", Length = 18, DecimalDigits = 2)]
    public virtual decimal UsedQuotaSum { get; set; }

    /// <summary>
    /// 日终：全池在途预占合计
    /// </summary>
    [SugarColumn(ColumnDescription = "日终在途预占", Length = 18, DecimalDigits = 2)]
    public virtual decimal LockedQuotaSum { get; set; }

    /// <summary>
    /// 日终：未到账（Failed）仍占用的预占合计
    /// </summary>
    [SugarColumn(ColumnDescription = "日终未到账占用", Length = 18, DecimalDigits = 2)]
    public virtual decimal UnpaidLockedSum { get; set; }

    /// <summary>
    /// 日终：启用中账号数
    /// </summary>
    [SugarColumn(ColumnDescription = "启用中账号数")]
    public virtual int AccountEnabledCount { get; set; }

    /// <summary>
    /// 快照落库时间
    /// </summary>
    [SugarColumn(ColumnDescription = "快照时间")]
    public virtual DateTime SnapshotTime { get; set; }
}
