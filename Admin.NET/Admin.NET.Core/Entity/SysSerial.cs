// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 系统流水号表
/// </summary>
[SugarTable(null, "系统流水号表")]
[SugarIndex("u_{table}_tt", nameof(Type), OrderByType.Asc, nameof(TenantId), OrderByType.Asc, IsUnique = true)]
public class SysSerial : EntityTenantId
{
    /// <summary>
    /// 序列号
    /// </summary>
    [IgnoreUpdateSeedColumn]
    [SugarColumn(ColumnDescription = "序列号")]
    [Required]
    public virtual long Seq { get; set; }

    /// <summary>
    /// 有效期
    /// </summary>
    [IgnoreUpdateSeedColumn]
    [SugarColumn(ColumnDescription = "有效期")]
    [Required]
    public virtual DateTime Expy { get; set; }

    /// <summary>
    /// 使用分类
    /// </summary>
    [SugarColumn(ColumnDescription = "使用分类", Length = 32)]
    [MaxLength(32)]
    [Required]
    public virtual string Type { get; set; }

    /// <summary>
    /// 重置间隔
    /// </summary>
    [SugarColumn(ColumnDescription = "重置间隔")]
    [Required]
    public virtual ResetIntervalEnum ResetInterval { get; set; }

    /// <summary>
    /// 格式化表达式
    /// </summary>
    [SugarColumn(ColumnDescription = "格式化表达式", Length = 128)]
    [MaxLength(128)]
    public virtual string Formater { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    [SugarColumn(ColumnDescription = "最小值")]
    public virtual long Min { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    [SugarColumn(ColumnDescription = "最大值")]
    public virtual long Max { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [SugarColumn(ColumnDescription = "排序")]
    public virtual int OrderNo { get; set; } = 100;

    /// <summary>
    /// 状态
    /// </summary>
    [SugarColumn(ColumnDescription = "状态")]
    [Required]
    public virtual StatusEnum Status { get; set; } = StatusEnum.Enable;

    /// <summary>
    /// 备注
    /// </summary>
    [IgnoreUpdateSeedColumn]
    [SugarColumn(ColumnDescription = "备注", Length = 128)]
    [MaxLength(128)]
    public virtual string? Remark { get; set; }
}