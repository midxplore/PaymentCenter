// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Core;

namespace Admin.NET.Test.PayCenter;

public class FileContentProbeTest
{
    [Fact]
    public void Jpeg魔数_应通过()
    {
        using var ms = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);
        FileContentProbe.EnsureSafeAndMatches(ms, ".jpg");
    }

    [Fact]
    public void Php伪装成Jpg_应拒绝()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("<?php echo 1;");
        using var ms = new MemoryStream(bytes);
        Assert.ThrowsAny<Exception>(() => FileContentProbe.EnsureSafeAndMatches(ms, ".jpg"));
    }

    [Fact]
    public void Html伪装成Png_应拒绝()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("<html><script>alert(1)</script>");
        using var ms = new MemoryStream(bytes);
        Assert.ThrowsAny<Exception>(() => FileContentProbe.EnsureSafeAndMatches(ms, ".png"));
    }

    [Fact]
    public void Pdf魔数_应通过()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake");
        using var ms = new MemoryStream(bytes);
        FileContentProbe.EnsureSafeAndMatches(ms, ".pdf");
    }
}
