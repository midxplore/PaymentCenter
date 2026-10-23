// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相应法律法规和许可证的要求。

using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Admin.NET.Application;

/// <summary>
/// 收款码落库前限边 + JPEG 重编码，减小反代静态体积。
/// </summary>
public static class PayQrImageOptimize
{
    /// <summary>
    /// 将上传图限制最长边并转为 JPEG；失败时抛业务异常。
    /// 调用方负责 Dispose 返回的 FormFile。
    /// </summary>
    public static async Task<IFormFile> ToJpegFormFileAsync(IFormFile file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length <= 0) throw Oops.Bah("请选择收款码图片");
        if (file.Length > PayConst.QrImageMaxBytes)
            throw Oops.Bah($"收款码图片不能超过 {PayConst.QrImageMaxBytes / (1024 * 1024)}MB");

        await using var input = file.OpenReadStream();
        var suffix = Path.GetExtension(file.FileName ?? "");
        FileContentProbe.EnsureSafeAndMatches(input, suffix);

        using var image = await Image.LoadAsync(input, ct);
        var maxSide = PayConst.QrImageMaxSide;
        if (image.Width > maxSide || image.Height > maxSide)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxSide, maxSide)
            }));
        }

        var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 85 }, ct);
        ms.Position = 0;

        var name = Path.GetFileNameWithoutExtension(file.FileName ?? "qr");
        if (string.IsNullOrWhiteSpace(name)) name = "qr";
        return new FormFile(ms, 0, ms.Length, file.Name, $"{name}.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }
}
