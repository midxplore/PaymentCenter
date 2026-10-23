using System.Text;
using Admin.NET.Application;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 收款码落库前限边 + JPEG 重编码
/// </summary>
public class PayQrImageOptimizeTest
{
    [Fact]
    public async Task ToJpegFormFile_Resizes_Oversize_Png()
    {
        await using var png = await MakePngAsync(2400, 1800);
        var form = new FormFile(png, 0, png.Length, "file", "big-qr.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var outFile = await PayQrImageOptimize.ToJpegFormFileAsync(form);
        Assert.Equal("image/jpeg", outFile.ContentType);
        Assert.EndsWith(".jpg", outFile.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(outFile.Length > 0);
        Assert.True(outFile.Length < png.Length);

        await using var outStream = outFile.OpenReadStream();
        using var image = await Image.LoadAsync(outStream);
        Assert.True(image.Width <= PayConst.QrImageMaxSide);
        Assert.True(image.Height <= PayConst.QrImageMaxSide);
    }

    [Fact]
    public async Task ToJpegFormFile_Rejects_NonImage()
    {
        var bytes = Encoding.UTF8.GetBytes("<html>nope</html>");
        await using var ms = new MemoryStream(bytes);
        var form = new FormFile(ms, 0, ms.Length, "file", "fake.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await Assert.ThrowsAnyAsync<Exception>(() => PayQrImageOptimize.ToJpegFormFileAsync(form));
    }

    private static async Task<MemoryStream> MakePngAsync(int w, int h)
    {
        using var image = new Image<Rgba32>(w, h);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            image[x, y] = (x + y) % 2 == 0 ? Color.Black : Color.White;

        var ms = new MemoryStream();
        await image.SaveAsync(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }
}
