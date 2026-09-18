// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 订单事件类型枚举（F7.1 / F7.4，流水只增不改）
/// </summary>
[Description("订单事件类型枚举")]
public enum PayEventTypeEnum
{
    /// <summary>
    /// 订单创建（匹配成功并预占额度）
    /// </summary>
    [Description("订单创建")]
    Created = 1,

    /// <summary>
    /// 部分到账
    /// </summary>
    [Description("部分到账")]
    PartialReceived = 2,

    /// <summary>
    /// 订单完成（累计到账达标）
    /// </summary>
    [Description("订单完成")]
    Completed = 3,

    /// <summary>
    /// 订单过期（释放未达成部分的预占额度）
    /// </summary>
    [Description("订单过期")]
    Expired = 4,

    /// <summary>
    /// 人工关联（异常到账补录到订单）
    /// </summary>
    [Description("人工关联")]
    ManualLinked = 5
}
