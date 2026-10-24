// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;

namespace Admin.NET.Test.PayCenter;

/// <summary>运营汇总口径纯函数</summary>
public class PayOpsMetricsTest
{
    [Fact]
    public void 过期释放_请求减已到账()
    {
        Assert.Equal(80m, PayOpsMetrics.ExpireReleased(100m, 20m));
        Assert.Equal(100m, PayOpsMetrics.ExpireReleased(100m, 0m));
        Assert.Equal(0m, PayOpsMetrics.ExpireReleased(100m, 100m));
        Assert.Equal(0m, PayOpsMetrics.ExpireReleased(50m, 80m));
    }

    [Fact]
    public void 投放差额_QuotaAdd增减()
    {
        var before = """{"TotalQuota":100.00}""";
        var afterUp = """{"TotalQuota":150.00}""";
        var afterDown = """{"TotalQuota":70.00}""";
        Assert.Equal(50m, PayOpsMetrics.TryParseTotalQuotaDelta(before, afterUp));
        Assert.Equal(-30m, PayOpsMetrics.TryParseTotalQuotaDelta(before, afterDown));
    }

    [Fact]
    public void 投放差额_AccountAdd无before视为0()
    {
        var after = """{"Type":"wxpay","TotalQuota":200.00,"MinAmount":0,"Status":1,"HasQrImage":true}""";
        Assert.Equal(200m, PayOpsMetrics.TryParseTotalQuotaDelta(null, after));
        Assert.Equal(200m, PayOpsMetrics.TryParseTotalQuotaDelta("", after));
    }

    [Fact]
    public void 投放差额_兼容camelCase()
    {
        Assert.Equal(10m, PayOpsMetrics.TryParseTotalQuotaDelta(
            """{"totalQuota":5}""", """{"totalQuota":15}"""));
    }
}
