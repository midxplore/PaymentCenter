// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成模板枚举
/// </summary>
[Description("代码生成模板枚举")]
public enum CodeGenSceneEnum
{
    /// <summary>
    /// 单表
    /// </summary>
    [Description("单表")]
    SingleTable = 1000,

    /// <summary>
    /// 单表树组件
    /// </summary>
    [Description("单表树组件")]
    TreeSingleTable = 1010,

    /// <summary>
    /// 主从表
    /// </summary>
    [Description("主从表")]
    MasterSlaveTables = 2000,

    /// <summary>
    /// 主从表树组件
    /// </summary>
    [Description("主从表树组件")]
    TreeMasterSlaveTables = 2010,

    /// <summary>
    /// 关系对照
    /// </summary>
    [Description("关系对照")]
    TableRelationship = 3000,

    /// <summary>
    /// 关系对照树组件
    /// </summary>
    [Description("关系对照树组件")]
    TreeTableRelationship = 3010,

    /// <summary>
    /// 表实体
    /// </summary>
    [Description("表实体")]
    TableEntity = -1000,

    /// <summary>
    /// 表种子数据
    /// </summary>
    [Description("表种子数据")]
    TableSeedData = -2000,
}