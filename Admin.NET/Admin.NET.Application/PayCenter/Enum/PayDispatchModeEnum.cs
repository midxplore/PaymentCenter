// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号匹配调度方式（与账号分层正交）
/// </summary>
[Description("收款账号调度方式")]
public enum PayDispatchModeEnum
{
    /// <summary>权重优选（现网默认：空过期升序、最近成功优先、最佳适配）</summary>
    [Description("权重优选")]
    Weighted = 1,

    /// <summary>轮询（匹配成功后排到队尾；人头码建议用此方式）</summary>
    [Description("轮询")]
    RoundRobin = 2
}
