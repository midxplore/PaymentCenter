// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成表字段配置表
/// </summary>
[SysTable]
[SugarTable(null, "代码生成表字段配置表")]
[SugarIndex("index_{table}_cti", nameof(CodeGenTableId), OrderByType.Asc)]
[SugarIndex("index_{table}_cn", nameof(ColumnName), OrderByType.Asc)]
public partial class SysCodeGenColumn : EntityBase
{
    /// <summary>
    /// 代码生成表Id
    /// </summary>
    [SugarColumn(ColumnDescription = "表Id")]
    public virtual long CodeGenTableId { get; set; }

    /// <summary>
    /// 数据库字段名
    /// </summary>
    [SugarColumn(ColumnDescription = "字段名称", Length = 128)]
    [Required, MaxLength(128)]
    public virtual string ColumnName { get; set; }

    /// <summary>
    /// 实体属性名
    /// </summary>
    [SugarColumn(ColumnDescription = "属性名称", Length = 128)]
    [Required, MaxLength(128)]
    public virtual string PropertyName { get; set; }

    /// <summary>
    /// .NET数据类型
    /// </summary>
    [SugarColumn(ColumnDescription = "NET数据类型", Length = 64)]
    [MaxLength(64)]
    public virtual string NetType { get; set; }

    /// <summary>
    /// 数据库中类型（物理类型）
    /// </summary>
    [SugarColumn(ColumnDescription = "数据库中类型", Length = 64)]
    [MaxLength(64)]
    public virtual string DataType { get; set; }

    /// <summary>
    /// 字段数据长度
    /// </summary>
    [SugarColumn(ColumnDescription = "字段数据长度")]
    public virtual int? ColumnLength { get; set; }

    /// <summary>
    /// 字段描述
    /// </summary>
    [SugarColumn(ColumnDescription = "字段描述", Length = 128)]
    [MaxLength(128)]
    public virtual string ColumnComment { get; set; }

    /// <summary>
    /// 控件类型
    /// </summary>
    [SugarColumn(ColumnDescription = "控件类型")]
    public virtual CodeGenEffectTypeEnum EffectType { get; set; }

    /// <summary>
    /// 控件配置
    /// </summary>
    [SugarColumn(ColumnDescription = "控件配置", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public virtual string? Config { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(ColumnDescription = "主键")]
    public virtual bool IsPrimarykey { get; set; }

    /// <summary>
    /// 是否通用字段
    /// </summary>
    [SugarColumn(ColumnDescription = "是否通用字段")]
    public virtual bool IsCommon { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    [SugarColumn(ColumnDescription = "是否必填")]
    public virtual bool IsRequired { get; set; }

    /// <summary>
    /// 增改
    /// </summary>
    [SugarColumn(ColumnDescription = "增改")]
    public virtual bool IsAddUpdate { get; set; }

    /// <summary>
    /// 导入导出
    /// </summary>
    [SugarColumn(ColumnDescription = "导入导出")]
    public virtual bool IsImport { get; set; }

    /// <summary>
    /// 是否可排序
    /// </summary>
    [SugarColumn(ColumnDescription = "是否可排序")]
    public virtual bool IsSortable { get; set; }

    /// <summary>
    /// 是否是统计字段
    /// </summary>
    [SugarColumn(ColumnDescription = "是否是统计字段")]
    public virtual bool IsStatistical { get; set; }

    /// <summary>
    /// 是否是查询条件
    /// </summary>
    [SugarColumn(ColumnDescription = "是否是查询条件")]
    public virtual bool IsQuery { get; set; }

    /// <summary>
    /// 查询方式
    /// </summary>
    [SugarColumn(ColumnDescription = "查询方式", Length = 16)]
    [MaxLength(16)]
    public virtual string? QueryType { get; set; }

    /// <summary>
    /// 列表显示
    /// </summary>
    [SugarColumn(ColumnDescription = "列表显示")]
    public virtual bool IsTable { get; set; }

    /// <summary>
    /// 内容复制
    /// </summary>
    [SugarColumn(ColumnDescription = "内容复制")]
    public virtual bool IsCopy { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    [SugarColumn(ColumnDescription = "默认值", Length = 256)]
    [MaxLength(256)]
    public virtual string? DefaultValue { get; set; }

    /// <summary>
    /// 字段验证规则
    /// </summary>
    [SugarColumn(ColumnDescription = "字段验证规则")]
    public virtual CodeGenFromRuleValidEnum? FromValid { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [SugarColumn(ColumnDescription = "排序", DefaultValue = "100")]
    public virtual int OrderNo { get; set; } = 100;
}