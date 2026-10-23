// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Admin.NET.Application;

/// <summary>
/// 收银台 Notice / 账号备注：白名单消毒（防 stored XSS）。
/// </summary>
public static partial class PayHtmlSanitize
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "b", "strong", "i", "em", "u", "ul", "ol", "li", "span", "div", "a"
    };

    private static readonly HashSet<string> AllowedAttrs = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "target", "rel", "class"
    };

    /// <summary>空编辑器常见占位视为无内容。</summary>
    public static string NormalizeEmpty(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var t = html.Trim();
        if (t is "<p><br></p>" or "<p><br/></p>" or "<p></p>") return "";
        return t;
    }

    /// <summary>消毒后输出；无标签纯文本原样返回。</summary>
    public static string Sanitize(string html)
    {
        html = NormalizeEmpty(html);
        if (html.Length == 0) return "";
        if (html.Length > PayConst.AccountRemarkLength)
            html = html[..PayConst.AccountRemarkLength];

        if (!LooksLikeHtml().IsMatch(html))
            return html;

        html = ScriptStyleBlock().Replace(html, "");
        html = OnEventAttr().Replace(html, "");

        var sb = new StringBuilder(html.Length);
        var pos = 0;
        foreach (Match m in TagMatch().Matches(html))
        {
            sb.Append(html, pos, m.Index - pos);

            var raw = m.Value;
            if (raw.StartsWith("<!--", StringComparison.Ordinal))
            {
                pos = m.Index + m.Length;
                continue;
            }

            var isClose = raw.StartsWith("</", StringComparison.Ordinal);
            var nameMatch = TagName().Match(raw);
            if (!nameMatch.Success)
            {
                pos = m.Index + m.Length;
                continue;
            }

            var name = nameMatch.Groups[1].Value;
            if (!AllowedTags.Contains(name))
            {
                pos = m.Index + m.Length;
                continue;
            }

            if (isClose)
            {
                sb.Append("</").Append(name.ToLowerInvariant()).Append('>');
            }
            else if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("<br/>");
            }
            else
            {
                sb.Append('<').Append(name.ToLowerInvariant());
                foreach (Match a in AttrMatch().Matches(raw))
                {
                    var an = a.Groups[1].Value;
                    if (!AllowedAttrs.Contains(an)) continue;

                    var av = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Value;
                    if (name.Equals("a", StringComparison.OrdinalIgnoreCase) && an.Equals("href", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsSafeHref(av)) continue;
                        sb.Append(" href=\"").Append(WebUtility.HtmlEncode(av)).Append('"');
                        sb.Append(" rel=\"noopener noreferrer\"");
                        continue;
                    }
                    if (an.Equals("target", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(" target=\"_blank\"");
                        continue;
                    }
                    if (an.Equals("class", StringComparison.OrdinalIgnoreCase))
                    {
                        if (av.Length > 64 || !SafeClass().IsMatch(av)) continue;
                        sb.Append(" class=\"").Append(WebUtility.HtmlEncode(av)).Append('"');
                    }
                }
                sb.Append('>');
            }

            pos = m.Index + m.Length;
        }
        sb.Append(html, pos, html.Length - pos);
        return sb.ToString();
    }

    private static bool IsSafeHref(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        href = href.Trim();
        if (href.StartsWith('#') || href.StartsWith('/')) return true;
        return href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"<[a-zA-Z!/]", RegexOptions.CultureInvariant)]
    private static partial Regex LooksLikeHtml();

    [GeneratedRegex(@"<(script|style|iframe|object|embed)[\s\S]*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptStyleBlock();

    [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OnEventAttr();

    [GeneratedRegex(@"<!--[\s\S]*?-->|</?[a-zA-Z][^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagMatch();

    [GeneratedRegex(@"^</?\s*([a-zA-Z][a-zA-Z0-9]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TagName();

    [GeneratedRegex(@"([a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.CultureInvariant)]
    private static partial Regex AttrMatch();

    [GeneratedRegex(@"^[a-zA-Z0-9_\-\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeClass();
}
