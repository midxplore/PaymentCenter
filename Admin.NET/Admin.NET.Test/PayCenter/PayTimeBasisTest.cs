// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;
using Xunit.Abstractions;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 时间基准守卫的回归测试
/// <para>
/// 背景：<c>pay_*</c> 的全部时间列都是 PostgreSQL <c>timestamp without time zone</c>，
/// 语义是「+08 墙上时间」字面值。写入用 <c>DateTime.Now</c>、入参按本地归一化、输出不带偏移 ——
/// 这三者只在进程时区为 +08 时才自洽，而时区不对时**不会报错**，
/// 只会在对账时发现新旧数据相差 8 小时。
/// </para>
/// <para>
/// ★ 本类要防的不只是「判定写错」，更重要的是防「守卫根本没被跑到」：
/// 守卫挂在 <see cref="PayCenterStartup"/> 上，靠 Furion 扫描 <see cref="AppStartup"/> 子类触发。
/// 一旦扫描不到（例如误引了另一套 Furion 包），守卫会变成**静默空操作** ——
/// 那种情况下代码看起来完全正常。所以这里专门断言它确实被发现了。
/// </para>
/// </summary>
public class PayTimeBasisTest
{
    private readonly ITestOutputHelper _output;

    public PayTimeBasisTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void 时间基准判定_只接受正八区()
    {
        Assert.True(PayTimeBasis.IsExpectedOffset(TimeSpan.FromHours(8)));

        // 常见的错误时区：UTC、UTC+9（东京）、UTC-5、UTC+5:30（半时区）
        Assert.False(PayTimeBasis.IsExpectedOffset(TimeSpan.Zero));
        Assert.False(PayTimeBasis.IsExpectedOffset(TimeSpan.FromHours(9)));
        Assert.False(PayTimeBasis.IsExpectedOffset(TimeSpan.FromHours(-5)));
        Assert.False(PayTimeBasis.IsExpectedOffset(TimeSpan.FromMinutes(330)));
    }

    [Fact]
    public void 时间基准判定_冬夏不一致也要拒绝()
    {
        // 只有两个时点都是 +08 才算合格；否则某一天偏移会自己变掉，
        // 而列里存的仍是无时区字面值 —— 同样是静默错位。
        Assert.True(PayTimeBasis.IsYearRoundExpected(TimeSpan.FromHours(8), TimeSpan.FromHours(8)));
        Assert.False(PayTimeBasis.IsYearRoundExpected(TimeSpan.FromHours(8), TimeSpan.FromHours(9)));
        Assert.False(PayTimeBasis.IsYearRoundExpected(TimeSpan.FromHours(9), TimeSpan.FromHours(8)));
        Assert.False(PayTimeBasis.IsYearRoundExpected(TimeSpan.Zero, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void 偏移格式化_带符号且补零()
    {
        Assert.Equal("UTC+08:00", PayTimeBasis.FormatOffset(TimeSpan.FromHours(8)));
        Assert.Equal("UTC+00:00", PayTimeBasis.FormatOffset(TimeSpan.Zero));
        Assert.Equal("UTC-05:00", PayTimeBasis.FormatOffset(TimeSpan.FromHours(-5)));
        Assert.Equal("UTC+05:30", PayTimeBasis.FormatOffset(TimeSpan.FromMinutes(330)));
        Assert.Equal("UTC+09:00", PayTimeBasis.FormatOffset(TimeSpan.FromHours(9)));
    }

    [Fact]
    public void 报错文案_必须保留修复指引与不要只删守卫的说明()
    {
        // ★ 这条是防「后来者只看到一句时区不对，然后把守卫删掉」。
        // 文案被削弱成没有可操作信息时，这里会失败。
        var msg = PayTimeBasis.BuildMismatchMessage(
            TimeZoneInfo.Utc, 2026, TimeSpan.Zero, TimeSpan.Zero);

        _output.WriteLine(msg);

        Assert.Contains("UTC+08:00", msg);              // 要求是什么
        Assert.Contains("TZ=Asia/Shanghai", msg);       // 怎么修
        Assert.Contains("timestamp without time zone", msg); // 为什么是这个要求
        Assert.Contains("静默", msg);                    // 不修会怎样
        Assert.Contains("timestamptz", msg);            // 若要改约定该往哪走
    }

    [Fact]
    public void 当前运行环境_时间基准必须合格()
    {
        // 本系统的时间语义依赖「进程时区 = 固定 +08」，所以运行环境本身就是被测试对象。
        // 在 UTC 机器/容器里跑测试时本用例会失败 —— 这是**故意的**：
        // 与其让测试全绿而数据错位，不如在这里明确失败，并给出 TZ=Asia/Shanghai 的修复提示。
        var year = DateTime.Now.Year;
        var (winter, summer) = PayTimeBasis.GetYearRoundOffsets(year);
        _output.WriteLine($"时区: {TimeZoneInfo.Local.Id}  冬: {PayTimeBasis.FormatOffset(winter)}  夏: {PayTimeBasis.FormatOffset(summer)}");

        Assert.True(PayTimeBasis.IsYearRoundExpected(winter, summer),
            $"当前运行环境时区为 {TimeZoneInfo.Local.Id}（{PayTimeBasis.FormatOffset(winter)} / {PayTimeBasis.FormatOffset(summer)}），" +
            "不是固定 UTC+08:00。请设置 TZ=Asia/Shanghai 后重跑。");

        // 守卫本身在当前环境不应抛异常
        PayTimeBasis.EnsureLocalTimeBasis();
    }

    [Fact]
    public void 守卫必须真的被执行_否则是静默空操作()
    {
        // ★ 这是本类最关键的一条，也是第一版**写漏**的一条。
        //   第一版只断言了「PayCenterStartup 出现在 App.EffectiveTypes 里」，
        //   但类型被扫到 ≠ 方法被调用：实测 TZ=UTC 时宿主照常启动（守卫没生效），
        //   而那条断言依然通过 —— 典型的「假绿灯」。
        //   真正要断言的是「ConfigureServices 被执行过」。
        var startups = (App.EffectiveTypes ?? []).Where(u => typeof(AppStartup).IsAssignableFrom(u) && u.IsClass && !u.IsAbstract).ToList();
        _output.WriteLine($"发现 AppStartup 子类 {startups.Count} 个：");
        foreach (var s in startups) _output.WriteLine($"  - {s.FullName}");

        Assert.Contains(startups, u => u == typeof(PayCenterStartup));
        Assert.True(PayCenterStartup.ConfigureServicesInvoked,
            "PayCenterStartup.ConfigureServices 没有被执行 —— 时间基准守卫是静默空操作。" +
            "检查 AppStartup 基类的 ConfigureServices 是否为 virtual（子类漏 override 时只会隐藏基类方法）。");
    }
}
