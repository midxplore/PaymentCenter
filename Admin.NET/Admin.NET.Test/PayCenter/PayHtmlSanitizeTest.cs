// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;

namespace Admin.NET.Test.PayCenter;

/// <summary>账号备注 / Notice HTML 消毒（无库）</summary>
public class PayHtmlSanitizeTest
{
    [Fact]
    public void 备注消毒_应去掉脚本与事件()
    {
        var dirty = "<p>请付款</p><script>alert(1)</script><b onclick=\"x()\">OK</b>";
        var clean = PayHtmlSanitize.Sanitize(dirty);
        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<b>", clean);
        Assert.Contains("请付款", clean);
    }

    [Fact]
    public void 空编辑器占位_视为空()
    {
        Assert.Equal("", PayHtmlSanitize.Sanitize("<p><br></p>"));
        Assert.Equal("纯文本", PayHtmlSanitize.Sanitize("纯文本"));
    }
}
