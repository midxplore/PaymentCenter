// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成表
/// </summary>
[SysTable]
[SugarTable(null, "代码生成表")]
[SugarIndex("index_{table}_b", nameof(BusName), OrderByType.Asc)]
public partial class SysCodeGen : EntityBase
{
    /// <summary>
    /// 作者姓名
    /// </summary>
    [MaxLength(32)]
    [SugarColumn(ColumnDescription = "作者姓名", Length = 32)]
    public virtual string? AuthorName { get; set; }

    /// <summary>
    /// 作者邮箱
    /// </summary>
    [MaxLength(32)]
    [SugarColumn(ColumnDescription = "作者邮箱", Length = 32)]
    public virtual string? Email { get; set; }

    /// <summary>
    /// 生成方式
    /// </summary>
    [SugarColumn(ColumnDescription = "生成方式")]
    public virtual CodeGenMethodEnum GenerateMethod { get; set; }

    /// <summary>
    /// 生成场景
    /// </summary>
    [SugarColumn(ColumnDescription = "生成场景")]
    public virtual CodeGenSceneEnum Scene { get; set; }

    /// <summary>
    /// 树组件配置
    /// </summary>
    [SugarColumn(ColumnDescription = "树组件配置", IsJson = true, ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public virtual TreeWithTableConfigInput? TreeConfig { get; set; }

    /// <summary>
    /// 命名空间
    /// </summary>
    [MaxLength(128)]
    [SugarColumn(ColumnDescription = "命名空间", Length = 128)]
    public virtual string NameSpace { get; set; }

    /// <summary>
    /// 业务名
    /// </summary>
    [MaxLength(128)]
    [SugarColumn(ColumnDescription = "业务名", Length = 128)]
    public virtual string BusName { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [MaxLength(32)]
    [SugarColumn(ColumnDescription = "模块名称", Length = 32)]
    public virtual string ModuleName { get; set; }

    /// <summary>
    /// 是否水平布局
    /// </summary>
    [SugarColumn(ColumnDescription = "是否水平布局")]
    public virtual bool IsHorizontal { get; set; }

    /// <summary>
    /// 是否生成菜单
    /// </summary>
    [SugarColumn(ColumnDescription = "是否生成菜单")]
    public virtual bool GenerateMenu { get; set; } = true;

    /// <summary>
    /// 菜单图标
    /// </summary>
    [SugarColumn(ColumnDescription = "菜单图标", Length = 32)]
    public virtual string MenuIcon { get; set; } = "ele-Menu";

    /// <summary>
    /// 菜单编码
    /// </summary>
    [SugarColumn(ColumnDescription = "菜单编码")]
    public virtual long MenuPid { get; set; }

    /// <summary>
    /// 页面目录
    /// </summary>
    [SugarColumn(ColumnDescription = "页面目录", Length = 32)]
    public virtual string PagePath { get; set; }

    /// <summary>
    /// 支持打印类型
    /// </summary>
    [SugarColumn(ColumnDescription = "支持打印类型")]
    public virtual CodeGenPrintTypeEnum PrintType { get; set; }

    /// <summary>
    /// 打印模版名称
    /// </summary>
    [MaxLength(32)]
    [SugarColumn(ColumnDescription = "打印模版名称", Length = 32)]
    public virtual string? PrintName { get; set; }

    /// <summary>
    /// 是否使用 Api Service
    /// </summary>
    [SugarColumn(ColumnDescription = "是否使用 Api Service")]
    public virtual bool IsApiService { get; set; } = false;

    /// <summary>
    /// 关联表
    /// </summary>
    [Navigate(NavigateType.OneToMany, nameof(SysCodeGenTable.CodeGenId))]
    public List<SysCodeGenTable> TableList { get; set; }
}