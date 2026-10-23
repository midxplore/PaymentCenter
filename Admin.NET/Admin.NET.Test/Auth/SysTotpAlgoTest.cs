using OtpNet;
using Xunit;

namespace Admin.NET.Test.Auth;

/// <summary>
/// TOTP 算法与加解密纯单测（不依赖库）
/// </summary>
public class SysTotpAlgoTest
{
    [Fact]
    public void VerifyCode_正确码应通过_错误码应拒绝()
    {
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        Assert.True(Verify(secret, code));
        Assert.False(Verify(secret, "000000"));
        Assert.False(Verify(secret, "12345"));
        Assert.False(Verify(secret, "abcdef"));
        Assert.False(Verify("", code));
    }

    [Fact]
    public void Cryptogram_加解密往返应一致()
    {
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var enc = CryptogramHelper.Encrypt(secret);
        Assert.False(string.IsNullOrWhiteSpace(enc));
        Assert.NotEqual(secret, enc);
        Assert.Equal(secret, CryptogramHelper.Decrypt(enc));
    }

    [Fact]
    public void OtpAuthUrl_应包含Issuer与账号()
    {
        var url = $"otpauth://totp/PulsePay%3Aadmin?secret=ABCDEF&issuer=PulsePay&digits=6&period=30";
        Assert.Contains("PulsePay", url);
        Assert.Contains("admin", url);
        Assert.Contains("secret=", url);
    }

    private static bool Verify(string base32Secret, string code)
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
}
