// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成关联表
/// </summary>
[SysTable]
[SugarTable(null, "代码生成关联表")]
[SugarIndex("index_{table}_cgi", nameof(CodeGenId), OrderByType.Asc)]
[SugarIndex("index_{table}_tn", nameof(TableName), OrderByType.Asc)]
public partial class SysCodeGenTable : EntityBase
{
    /// <summary>
    /// 代码生成Id
    /// </summary>
    [SugarColumn(ColumnDescription = "代码生成Id")]
    [Required]
    public virtual long CodeGenId { get; set; }

    /// <summary>
    /// 数据库配置id
    /// </summary>
    [SugarColumn(ColumnDescription = "数据库配置id", Length = 64)]
    [MaxLength(64)]
    public virtual string ConfigId { get; set; }

    /// <summary>
    /// 数据库表名
    /// </summary>
    [SugarColumn(ColumnDescription = "数据库表名", Length = 128)]
    [MaxLength(128)]
    public virtual string TableName { get; set; }

    /// <summary>
    /// 表实体名称
    /// </summary>
    [SugarColumn(ColumnDescription = "表实体名称", Length = 128)]
    [MaxLength(128)]
    public virtual string EntityName { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [SugarColumn(ColumnDescription = "模块名称", Length = 64)]
    [MaxLength(64)]
    public virtual string ModuleName { get; set; }

    /// <summary>
    /// 业务名
    /// </summary>
    [MaxLength(128)]
    [SugarColumn(ColumnDescription = "业务名", Length = 128)]
    public virtual string BusName { get; set; }

    /// <summary>
    /// 上级联表字段
    /// </summary>
    [SugarColumn(ColumnDescription = "上级联表字段", Length = 32)]
    public virtual string? LastLinkPropertyName { get; set; }

    /// <summary>
    /// 下级联表字段
    /// </summary>
    [SugarColumn(ColumnDescription = "下级联表字段", Length = 32)]
    public virtual string? NextLinkPropertyName { get; set; }

    /// <summary>
    /// 关联字段
    /// </summary>
    [Navigate(NavigateType.OneToMany, nameof(SysCodeGenColumn.CodeGenTableId))]
    public virtual List<SysCodeGenColumn> ColumnList { get; set; }
}