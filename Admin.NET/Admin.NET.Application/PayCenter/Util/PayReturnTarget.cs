// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收银台返回地址 / 按钮文案规范化
/// </summary>
public static class PayReturnTarget
{
    /// <summary>
    /// 规范化 returnUrl：空 → null；仅允许 http/https 绝对 URI。
    /// </summary>
    public static string NormalizeUrl(string raw)
    {
        var url = raw?.Trim();
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (url.Length > PayConst.ReturnUrlLength)
            throw Oops.Bah($"返回地址长度不能超过 {PayConst.ReturnUrlLength}");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw Oops.Bah("返回地址须为 http 或 https 绝对地址");
        return url;
    }

    /// <summary>
    /// 规范化按钮文案：空 → null（前端默认「返回」）；去空白、截断。
    /// </summary>
    public static string NormalizeLabel(string raw)
    {
        var label = raw?.Trim();
        if (string.IsNullOrWhiteSpace(label)) return null;
        if (label.Length > PayConst.ReturnLabelLength)
            label = label[..PayConst.ReturnLabelLength];
        return label;
    }
}
