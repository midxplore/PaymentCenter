// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成查询类型枚举
/// </summary>
[Description("代码生成查询类型枚举")]
public enum CodeGenQueryTypeEnum
{
    /// <summary>
    /// 等于
    /// </summary>
    [Description("等于")]
    Equal = 100,

    /// <summary>
    /// 模糊匹配
    /// </summary>
    [Description("模糊")]
    Like = 101,

    /// <summary>
    /// 大于
    /// </summary>
    [Description("大于")]
    GreaterThan = 102,

    /// <summary>
    /// 小于
    /// </summary>
    [Description("小于")]
    LessThan = 103,

    /// <summary>
    /// 不等于
    /// </summary>
    [Description("不等于")]
    NotEqual = 104,

    /// <summary>
    /// 大于等于
    /// </summary>
    [Description("大于等于")]
    GreaterThanOrEqual = 105,

    /// <summary>
    /// 小于等于
    /// </summary>
    [Description("小于等于")]
    LessThanOrEqual = 106,

    /// <summary>
    /// 不为空
    /// </summary>
    [Description("不为空")]
    IsNotNull = 107,

    /// <summary>
    /// 时间范围
    /// </summary>
    [Description("时间范围")]
    TimeRange = 108
}