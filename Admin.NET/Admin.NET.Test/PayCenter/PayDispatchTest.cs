// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 匹配调度：轮询触达队尾、混用优先轮询、权重粘性不作用于轮询池
/// </summary>
public class PayDispatchTest : IDisposable
{
    private const string TestType = "unittest_dispatch";

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;

    public PayDispatchTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _allocateService = App.GetRequiredService<PayAllocateService>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        _db.Ado.ExecuteCommand(
            "delete from pay_order_event where orderno in (select orderno from pay_order where accountid in (select id from pay_account where type = @type))",
            new SugarParameter("@type", TestType));
        _db.Ado.ExecuteCommand(
            "delete from pay_order where accountid in (select id from pay_account where type = @type)",
            new SugarParameter("@type", TestType));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long Seed(string info, decimal total, PayDispatchModeEnum mode,
        DateTime? lastSuccess = null, DateTime? lastMatched = null, PayAccountTierEnum tier = PayAccountTierEnum.Large)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = info,
            TotalQuota = total,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = PayAccountStatusEnum.Enabled,
            AccountTier = tier,
            DispatchMode = mode,
            LastSuccessTime = lastSuccess,
            LastMatchedAt = lastMatched,
            EmptyExpireCount = 0,
            Remark = "单元测试调度"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private PayAccount Get(long id) => _db.Queryable<PayAccount>().First(u => u.Id == id);

    [Fact]
    public async Task 轮询_连续匹配应轮转且触达LastMatchedAt()
    {
        var a = Seed("RR-A", 1000m, PayDispatchModeEnum.RoundRobin);
        var b = Seed("RR-B", 1000m, PayDispatchModeEnum.RoundRobin);
        var c = Seed("RR-C", 1000m, PayDispatchModeEnum.RoundRobin);

        var first = await _allocateService.TryLockQuotaAsync(TestType, 10m);
        var second = await _allocateService.TryLockQuotaAsync(TestType, 10m);
        var third = await _allocateService.TryLockQuotaAsync(TestType, 10m);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.Equal(new HashSet<long> { a, b, c }, new HashSet<long> { first!.Value, second!.Value, third!.Value });

        Assert.NotNull(Get(first.Value).LastMatchedAt);
        Assert.NotNull(Get(second.Value).LastMatchedAt);
        Assert.NotNull(Get(third.Value).LastMatchedAt);
    }

    [Fact]
    public async Task 轮询_不因最近成功而粘在同一账号()
    {
        var sticky = Seed("RR-STICKY", 1000m, PayDispatchModeEnum.RoundRobin,
            lastSuccess: DateTime.Now, lastMatched: DateTime.Now.AddMinutes(-1));
        var fresh = Seed("RR-FRESH", 1000m, PayDispatchModeEnum.RoundRobin,
            lastSuccess: null, lastMatched: null);

        var locked = await _allocateService.TryLockQuotaAsync(TestType, 10m);

        Assert.Equal(fresh, locked);
        Assert.Equal(0m, Get(sticky).LockedQuota);
    }

    [Fact]
    public async Task 混用_有可用轮询时优先于权重优选()
    {
        var weighted = Seed("WT", 1000m, PayDispatchModeEnum.Weighted,
            lastSuccess: DateTime.Now);
        var rr = Seed("RR", 1000m, PayDispatchModeEnum.RoundRobin);

        var locked = await _allocateService.TryLockQuotaAsync(TestType, 10m);

        Assert.Equal(rr, locked);
        Assert.Equal(0m, Get(weighted).LockedQuota);
    }

    [Fact]
    public async Task 混用_轮询额度不足时回落到权重优选()
    {
        Seed("RR-SMALL", 5m, PayDispatchModeEnum.RoundRobin);
        var weighted = Seed("WT-OK", 1000m, PayDispatchModeEnum.Weighted);

        var locked = await _allocateService.TryLockQuotaAsync(TestType, 10m);

        Assert.Equal(weighted, locked);
    }

    [Fact]
    public async Task 权重优选_路径仍按最佳适配()
    {
        Seed("WT-BIG", 1000m, PayDispatchModeEnum.Weighted);
        var small = Seed("WT-SMALL", 50m, PayDispatchModeEnum.Weighted);
        Seed("WT-MID", 200m, PayDispatchModeEnum.Weighted);

        var locked = await _allocateService.TryLockQuotaAsync(TestType, 40m);

        Assert.Equal(small, locked);
    }
}
