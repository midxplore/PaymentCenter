// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Text.Encodings.Web;
using OtpNet;
using QRCoder;

namespace Admin.NET.Core.Service;

/// <summary>
/// Google Authenticator TOTP 工具（票 / 密钥 / 二维码 / 校验）
/// </summary>
public class SysTotpService : ITransient
{
    /// <summary>otpauth issuer / 应用名</summary>
    public const string Issuer = "PulsePay";

    /// <summary>中间票有效期</summary>
    public static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(10);

    private readonly SysCacheService _sysCacheService;

    public SysTotpService(SysCacheService sysCacheService)
    {
        _sysCacheService = sysCacheService;
    }

    /// <summary>签发登录中间票（仅缓存 userId）</summary>
    public string IssueTicket(long userId)
    {
        var ticket = Convert.ToBase64String(KeyGeneration.GenerateRandomKey(24))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _sysCacheService.Set($"{CacheConst.KeyTotpTicket}{ticket}", userId, TicketTtl);
        return ticket;
    }

    /// <summary>解析票 → userId；无效/过期抛错</summary>
    public long RequireUserId(string ticket)
    {
        ticket = (ticket ?? "").Trim();
        if (ticket.Length < 16)
            throw Oops.Bah("登录凭证已失效，请重新登录");
        var userId = _sysCacheService.Get<long>($"{CacheConst.KeyTotpTicket}{ticket}");
        if (userId <= 0)
            throw Oops.Bah("登录凭证已失效，请重新登录");
        return userId;
    }

    public void RemoveTicket(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket)) return;
        _sysCacheService.Remove($"{CacheConst.KeyTotpTicket}{ticket}");
        _sysCacheService.Remove($"{CacheConst.KeyTotpSetupSecret}{ticket}");
    }

    /// <summary>生成或复用绑定态明文 Base32 secret</summary>
    public string GetOrCreateSetupSecret(string ticket)
    {
        var key = $"{CacheConst.KeyTotpSetupSecret}{ticket}";
        var existing = _sysCacheService.Get<string>(key);
        if (!string.IsNullOrWhiteSpace(existing)) return existing;

        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        _sysCacheService.Set(key, secret, TicketTtl);
        return secret;
    }

    public string BuildOtpAuthUrl(string account, string base32Secret)
    {
        var label = UrlEncoder.Default.Encode($"{Issuer}:{account}");
        var issuer = UrlEncoder.Default.Encode(Issuer);
        var secret = Uri.EscapeDataString(base32Secret);
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&digits=6&period=30";
    }

    public string BuildQrPngBase64(string otpAuthUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(6);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    public bool VerifyCode(string base32Secret, string code)
    {
        code = (code ?? "").Trim();
        if (code.Length != 6 || !code.All(char.IsDigit)) return false;
        if (string.IsNullOrWhiteSpace(base32Secret)) return false;

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
            return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    public string EncryptSecret(string base32Secret) => CryptogramHelper.Encrypt(base32Secret);

    public string DecryptSecret(string encrypted) => CryptogramHelper.Decrypt(encrypted);

    /// <summary>TOTP 错误次数（按 ticket）；超限抛锁定</summary>
    public void EnsureNotLocked(string ticket, int maxTimes)
    {
        if (maxTimes < 1) maxTimes = 5;
        var times = _sysCacheService.Get<int>($"{CacheConst.KeyTotpErrorTimes}{ticket}");
        if (times > maxTimes)
            throw Oops.Bah("动态码错误次数过多，请重新登录后再试");
    }

    public void RecordError(string ticket)
    {
        var key = $"{CacheConst.KeyTotpErrorTimes}{ticket}";
        var times = _sysCacheService.Get<int>(key);
        _sysCacheService.Set(key, times + 1, TicketTtl);
    }

    public void ClearError(string ticket)
    {
        _sysCacheService.Remove($"{CacheConst.KeyTotpErrorTimes}{ticket}");
    }

    /// <summary>
    /// 校验当前用户 TOTP；未绑定或码错误则抛错。
    /// </summary>
    public void EnsureTotp(SysUser user, string code)
    {
        if (user == null || !user.TotpEnabled || string.IsNullOrWhiteSpace(user.TotpSecret))
            throw Oops.Bah("请先绑定身份验证器后再操作");

        string plain;
        try
        {
            plain = DecryptSecret(user.TotpSecret);
        }
        catch
        {
            throw Oops.Bah("动态口令数据异常，请联系管理员重置");
        }

        if (!VerifyCode(plain, code))
            throw Oops.Bah("动态码不正确");
    }
}
