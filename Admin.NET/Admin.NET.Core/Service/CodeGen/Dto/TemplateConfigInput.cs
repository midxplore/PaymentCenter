// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 单表带树组件模板配置输入参数
/// </summary>
public class TreeWithTableConfigInput : EffectTreeConfigInput
{
    /// <summary>
    /// 树标题
    /// </summary>
    [Required(ErrorMessage = "树标题不能为空")]
    public override string TreeTitle { get; set; }
}

/// <summary>
/// 多表模板配置输入参数
/// </summary>
public class MoreTableConfigInput
{
    /// <summary>
    /// 是否水平布局
    /// </summary>
    public bool IsHorizontal { get; set; }
}

/// <summary>
/// 对照表扩展配置输入参数
/// </summary>
public class TableRelationshipConfigInput : MoreTableConfigInput
{
    /// <summary>
    /// 目标表数据库配置id
    /// </summary>
    [Required(ErrorMessage = "目标表数据库配置id不能为空")]
    public string ConfigId { get; set; }

    /// <summary>
    /// 目标表数据库表名
    /// </summary>
    [Required(ErrorMessage = "目标表数据库表名不能为空")]
    public string TableName { get; set; }

    /// <summary>
    /// 对照表1链接字段名
    /// </summary>
    [Required(ErrorMessage = "对照表1链接字段名不能为空")]
    public string PropertyName1 { get; set; }

    /// <summary>
    /// 对照表2链接字段名
    /// </summary>
    [Required(ErrorMessage = "对照表2链接字段名不能为空")]
    public string PropertyName2 { get; set; }
}