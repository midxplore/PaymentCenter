// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Furion;
using Xunit.Abstractions;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 测试宿主自检：确认 <see cref="TestProgram"/> 的 <c>Serve.RunNative()</c> 真的把应用启动起来了，
/// 并且**连的是配置里那个库**
/// </summary>
/// <remarks>
/// <para>
/// 走数据库的用例都依赖宿主已注册 SqlSugar 等服务。若本类失败，先修宿主启动，再看其它用例的失败。
/// </para>
/// <para>
/// ★★ 本类的断言刻意「重」。只断言「服务非空」是**不够**的：宿主即便连到一个**空库**，
/// <c>App.GetService&lt;ISqlSugarClient&gt;()</c> 依然**非空**，于是「断言非空」会安静地通过 ——
/// 假绿。真实缺陷因此被掩盖了整整一轮。
/// </para>
/// <para>
/// 所以这里改为断言**可观察事实**：库类型不是 Sqlite、库里确实有表、且 <c>pay_*</c> 业务表存在。
/// 这三条只要宿主连错库或 CodeFirst 没跑，就必然失败。
/// </para>
/// </remarks>
public class TestHostSanityTest
{
    private readonly ITestOutputHelper _output;

    public TestHostSanityTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void 宿主已启动_应能解析出应用配置()
    {
        Assert.NotNull(App.Configuration);

        _output.WriteLine($"宿主环境：{App.HostEnvironment?.EnvironmentName ?? "<未读到>"}");
        _output.WriteLine($"ASPNETCORE_ENVIRONMENT：{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "<未设置>"}");

        // 宿主真的起来了吗？启动失败时这项是 False。
        Assert.NotNull(App.RootServices);

        // ★ ConfigurationScanDirectories 是**数组**：.NET 配置把它存成
        //   `ConfigurationScanDirectories:0` / `:1`，用父键名索引（App.Configuration["ConfigurationScanDirectories"]）
        //   恒为 null。曾因此把「读法不对」误判成「没读到」，白查了一轮。必须按节读子项。
        var scanDirs = App.Configuration.GetSection("ConfigurationScanDirectories")
            .GetChildren()
            .Select(u => u.Value)
            .Where(u => u != null)
            .ToList();

        _output.WriteLine($"ConfigurationScanDirectories：[{string.Join(", ", scanDirs.Select(d => d.Length == 0 ? "<空串=内容根>" : d))}]");

        Assert.NotEmpty(scanDirs);
        Assert.Contains("Configuration", scanDirs);

        var dbType = App.Configuration["DbConnection:ConnectionConfigs:0:DbType"];
        _output.WriteLine($"配置中的 DbType：{dbType ?? "<未读到>"}");

        Assert.False(string.IsNullOrWhiteSpace(dbType), "未从配置读到 DbConnection:ConnectionConfigs:0:DbType");
    }

    /// <summary>
    /// 核心断言：宿主解析出的 SqlSugar 客户端必须**真的能读到一个建好的库**。
    /// </summary>
    /// <remarks>
    /// 这里刻意不断言「服务非空」——那正是上一轮的假绿来源。改为连库读表：
    /// 连错库、CodeFirst 没跑、业务表没建，都会在此失败。
    /// </remarks>
    [Fact]
    public void 宿主已启动_数据库必须真实可用()
    {
        var db = App.GetService<ISqlSugarClient>();
        Assert.NotNull(db);

        var dbType = db.CurrentConnectionConfig.DbType;
        _output.WriteLine($"ISqlSugarClient.DbType：{dbType}");

        // 连接串可能含口令，打印前必须脱敏（沿用项目约定：密钥不出现在日志里）。
        var conn = db.CurrentConnectionConfig.ConnectionString ?? string.Empty;
        var masked = System.Text.RegularExpressions.Regex.Replace(
            conn, @"(?i)(PASSWORD|PWD)\s*=\s*[^;]*", "$1=***");
        _output.WriteLine($"连接串（脱敏）：{masked}");

        // ★ 本项目只有 PG：DbType 不是 PG 说明连的不是本项目的库。
        Assert.NotEqual(DbType.Sqlite, dbType);

        // ★★ 真断言：连上库并列出表。空库 / 连错库 / 表没建都会在这里炸。
        var tables = db.DbMaintenance.GetTableInfoList(false);
        _output.WriteLine($"库中表数量：{tables.Count}");

        Assert.True(tables.Count > 0, $"已连上 {dbType}，但库里一张表都没有 —— CodeFirst 没跑，或连错了库。");

        var names = tables
            .Select(u => u.Name?.ToLowerInvariant() ?? string.Empty)
            .ToList();

        var payTables = names.Where(u => u.StartsWith("pay_")).ToList();
        _output.WriteLine($"pay_* 表数量：{payTables.Count}");

        // pay_account 是收款账号分配系统的核心表；它不存在说明连的根本不是本项目的库。
        Assert.Contains("pay_account", names);
    }

    [Fact]
    public void 宿主自检_AppStartup是否被发现()
    {
        var types = App.EffectiveTypes?.ToList() ?? [];
        _output.WriteLine($"App.EffectiveTypes 数量：{types.Count}");

        var startups = types.Where(u => typeof(AppStartup).IsAssignableFrom(u) && u.IsClass && !u.IsAbstract).ToList();
        _output.WriteLine($"发现 AppStartup 子类：{startups.Count}");
        foreach (var s in startups) _output.WriteLine($"  - {s.FullName}");

        var webCoreStartup = Type.GetType("Admin.NET.Web.Core.Startup, Admin.NET.Web.Core");
        _output.WriteLine($"Admin.NET.Web.Core.Startup 可解析：{webCoreStartup != null}");
        _output.WriteLine($"  已包含在 EffectiveTypes：{webCoreStartup != null && types.Contains(webCoreStartup)}");

        // 早期注册的服务（AddProjectOptions）是否可用，用于判断 ConfigureServices 跑到哪一步
        var options = App.GetService<Microsoft.Extensions.Options.IOptions<DbConnectionOptions>>();
        _output.WriteLine($"IOptions<DbConnectionOptions>：{(options == null ? "<未注册>" : "已注册")}");

        Assert.NotEmpty(startups);
    }

    /// <summary>
    /// 回归守卫：进程内只能有「一套」Furion 运行时，且必须是与应用同源的那套。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 背景：Furion 官方同时发布 <c>Furion</c> 与 <c>Furion.Pure</c> 两个包，
    /// 它们版本号、公钥完全相同但<b>程序集标识不同</b>，因此是两套互不兼容的静态
    /// <c>App</c> / <c>Serve</c> / <c>AppStartup</c>。
    /// </para>
    /// <para>
    /// 本解决方案（Admin.NET.Core / Admin.NET.Web.Core）编译期绑定 <c>Furion.Pure</c>。
    /// 一旦测试工程误引 <c>Furion.Xunit</c>（它依赖非 Pure 的 <c>Furion</c>），就会出现：
    /// 宿主扫描 <c>Furion.AppStartup</c> 子类 → 一个都扫不到（真实启动类派生自
    /// <c>Furion.Pure.AppStartup</c>）→ <c>Startup.ConfigureServices</c> 永不执行 →
    /// DI 里没有任何应用服务，但 Furion 宿主自己注册的 <c>IConfigurableOptions</c> 仍在，
    /// 于是故障表现为「看起来跑了一半」，极难定位。这条断言把该故障钉死在启动阶段。
    /// </para>
    /// </remarks>
    [Fact]
    public void 诊断_只能存在一套Furion运行时()
    {
        var startupType = Type.GetType("Admin.NET.Web.Core.Startup, Admin.NET.Web.Core");
        Assert.NotNull(startupType);

        _output.WriteLine($"typeof(AppStartup) = {typeof(AppStartup).Assembly.FullName}");
        _output.WriteLine($"Startup.BaseType  = {startupType.BaseType?.Assembly.FullName}");

        // 1) 编译期解析到的 AppStartup 必须就是启动类的基类（同一程序集）
        Assert.True(typeof(AppStartup).IsAssignableFrom(startupType),
            "Admin.NET.Web.Core.Startup 不是当前 AppStartup 的子类：进程里混入了两套 Furion 运行时。");

        // 2) 程序集名必须一致，且不得同时加载 Furion 与 Furion.Pure
        Assert.Equal(typeof(AppStartup).Assembly.GetName().Name, startupType.BaseType!.Assembly.GetName().Name);

        var furionAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name)
            .Where(n => n is "Furion" or "Furion.Pure")
            .ToList();

        _output.WriteLine($"已加载的 Furion 运行时：{string.Join(", ", furionAssemblies)}");

        Assert.DoesNotContain("Furion", furionAssemblies);
        Assert.Contains("Furion.Pure", furionAssemblies);
    }
}
