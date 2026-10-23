// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// TOTP 绑定二维码输出
/// </summary>
public class TotpSetupOutput
{
    /// <summary>二维码 data-url（PNG base64）</summary>
    public string QrCodeImage { get; set; }

    /// <summary>手动输入的 Base32 密钥</summary>
    public string ManualKey { get; set; }

    /// <summary>账号标签</summary>
    public string Account { get; set; }

    /// <summary>发行方</summary>
    public string Issuer { get; set; } = SysTotpService.Issuer;
}

/// <summary>
/// TOTP 持票请求
/// </summary>
public class TotpTicketInput
{
    /// <summary>登录中间票</summary>
    [Required(ErrorMessage = "登录凭证不能为空")]
    public string TotpTicket { get; set; }
}

/// <summary>
/// TOTP 验码 / 确认绑定
/// </summary>
public class TotpCodeInput : TotpTicketInput
{
    /// <summary>6 位动态码</summary>
    [Required(ErrorMessage = "动态码不能为空")]
    [MinLength(6, ErrorMessage = "动态码须为6位")]
    [MaxLength(6, ErrorMessage = "动态码须为6位")]
    public string Code { get; set; }
}
