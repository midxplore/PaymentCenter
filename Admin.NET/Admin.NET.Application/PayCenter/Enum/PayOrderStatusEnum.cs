// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款订单状态枚举
/// </summary>
/// <remarks>
/// 状态机：待到账 → 部分到账 → 已完成；待到账/部分到账 → 已过期。
/// 「已完成」「已过期」为终态，不可逆。
/// </remarks>
[Description("收款订单状态枚举")]
public enum PayOrderStatusEnum
{
    /// <summary>
    /// 待到账（已匹配账号并预占额度）
    /// </summary>
    [Description("待到账")]
    Pending = 1,

    /// <summary>
    /// 部分到账（累计到账小于请求金额，额度继续锁定）
    /// </summary>
    [Description("部分到账")]
    Partial = 2,

    /// <summary>
    /// 已完成（累计到账达标，终态）
    /// </summary>
    [Description("已完成")]
    Completed = 3,

    /// <summary>
    /// 已过期（超过过期时长仍未达标，终态）
    /// </summary>
    [Description("已过期")]
    Expired = 4
}
