// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 上传内容探测：按文件头校验真实类型，防止改扩展名上传脚本
/// </summary>
public static class FileContentProbe
{
    /// <summary>必须做内容校验的后缀（声明为图片/PDF 时）</summary>
    private static readonly HashSet<string> StrictSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".gif", ".bmp", ".webp", ".pdf"
    };

    /// <summary>
    /// 校验扩展名与文件头一致；并拒绝脚本/HTML 伪装。
    /// </summary>
    /// <param name="stream">可读可定位流；方法结束后复位到开头</param>
    /// <param name="suffix">含点的扩展名，如 .jpg</param>
    public static void EnsureSafeAndMatches(Stream stream, string suffix)
    {
        if (stream == null || !stream.CanRead) throw Oops.Oh(ErrorCodeEnum.D8000);

        suffix = (suffix ?? "").Trim().ToLowerInvariant();
        if (suffix is ".jpeg" or ".jpe") suffix = ".jpg";

        var header = new byte[16];
        var read = stream.Read(header, 0, header.Length);
        if (stream.CanSeek) stream.Position = 0;
        if (read < 2) throw Oops.Oh(ErrorCodeEnum.D8001, "空或过短");

        // 脚本 / 可执行伪装（无论扩展名）
        if (LooksLikeScriptOrHtml(header.AsSpan(0, read)))
            throw Oops.Oh(ErrorCodeEnum.D8001, "疑似脚本或网页内容");

        if (!StrictSuffixes.Contains(suffix) && suffix != ".jpg")
            return; // 非严控类型：仅拦脚本头；后缀白名单仍由 AllowSuffix 负责

        if (!MatchesDeclared(header.AsSpan(0, read), suffix))
            throw Oops.Oh(ErrorCodeEnum.D8001, $"内容与扩展名 {suffix} 不符");
    }

    private static bool MatchesDeclared(ReadOnlySpan<byte> h, string suffix) => suffix switch
    {
        ".jpg" => h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h.Length >= 8
                  && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                  && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
        ".gif" => h.Length >= 6
                  && h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'8'
                  && (h[4] == (byte)'7' || h[4] == (byte)'9') && h[5] == (byte)'a',
        ".bmp" => h.Length >= 2 && h[0] == (byte)'B' && h[1] == (byte)'M',
        ".webp" => h.Length >= 12
                   && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
                   && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P',
        ".pdf" => h.Length >= 5
                  && h[0] == (byte)'%' && h[1] == (byte)'P' && h[2] == (byte)'D' && h[3] == (byte)'F' && h[4] == (byte)'-',
        _ => false
    };

    private static bool LooksLikeScriptOrHtml(ReadOnlySpan<byte> h)
    {
        // UTF-8 BOM + <
        var i = 0;
        if (h.Length >= 3 && h[0] == 0xEF && h[1] == 0xBB && h[2] == 0xBF) i = 3;
        while (i < h.Length && (h[i] == (byte)' ' || h[i] == (byte)'\t' || h[i] == (byte)'\r' || h[i] == (byte)'\n'))
            i++;
        if (i >= h.Length) return false;

        // <?php / <?= / <script / <html / <!DOCTYPE
        if (h[i] == (byte)'<')
        {
            var rest = h[i..];
            if (StartsWithIgnoreCaseAscii(rest, "<?")
                || StartsWithIgnoreCaseAscii(rest, "<script")
                || StartsWithIgnoreCaseAscii(rest, "<html")
                || StartsWithIgnoreCaseAscii(rest, "<!doctype")
                || StartsWithIgnoreCaseAscii(rest, "<svg"))
                return true;
        }

        // Shebang
        if (h.Length >= 2 && h[0] == (byte)'#' && h[1] == (byte)'!')
            return true;

        // PE / ELF
        if (h.Length >= 2 && h[0] == (byte)'M' && h[1] == (byte)'Z') return true;
        if (h.Length >= 4 && h[0] == 0x7F && h[1] == (byte)'E' && h[2] == (byte)'L' && h[3] == (byte)'F')
            return true;

        return false;
    }

    private static bool StartsWithIgnoreCaseAscii(ReadOnlySpan<byte> data, string ascii)
    {
        if (data.Length < ascii.Length) return false;
        for (var i = 0; i < ascii.Length; i++)
        {
            var c = (char)data[i];
            var e = ascii[i];
            if (char.ToLowerInvariant(c) != char.ToLowerInvariant(e)) return false;
        }
        return true;
    }
}
