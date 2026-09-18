// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 收款账号的额度预占、最佳适配与状态同步（F2 / F1.4）
/// </summary>
/// <remarks>
/// <para>
/// 走真实数据库（由 <see cref="TestProgram"/> 的 <c>Serve.RunNative()</c> 启动应用后，
/// 连接 <c>ConfigurationLocal</c> 指定的库）。用例为**串行**调用，
/// 因此这里验证的是「单次调用的判定与记账是否正确」；
/// 真实并发下的不超发/不重复释放由 HTTP 并发用例覆盖（见设计文档 §13 的回归说明）。
/// </para>
/// <para>所有造数使用独立的 Type 前缀，并在构造与析构时各清理一次，避免污染开发库。</para>
/// </remarks>
public class PayQuotaTest : IDisposable
{
    /// <summary>本类专用收款类型，避免与真实数据及其他用例互相干扰</summary>
    private const string TestType = "unittest_payquota";

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;
    private readonly PayAccountService _accountService;

    public PayQuotaTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _allocateService = App.GetRequiredService<PayAllocateService>();
        _accountService = App.GetRequiredService<PayAccountService>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数（绕过软删除过滤器，保证不残留）</summary>
    private void Cleanup()
    {
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long SeedAccount(string info, decimal total, decimal used = 0m, decimal locked = 0m,
        PayAccountStatusEnum status = PayAccountStatusEnum.Enabled)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = info,
            TotalQuota = total,
            UsedQuota = used,
            LockedQuota = locked,
            Status = status,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private PayAccount GetAccount(long id) => _db.Queryable<PayAccount>().First(u => u.Id == id);

    private decimal GetLocked(long id) => GetAccount(id).LockedQuota;

    #region 最佳适配（F2.2）

    [Fact]
    public async Task 最佳适配_应选中剩余额度最小且够用的账号()
    {
        var big = SeedAccount("BIG-1000", 1000m);
        var small = SeedAccount("SMALL-50", 50m);
        var mid = SeedAccount("MID-200", 200m);

        var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 40m);

        Assert.Equal(small, lockedId);
        Assert.Equal(40m, GetLocked(small));
        Assert.Equal(0m, GetLocked(big));
        Assert.Equal(0m, GetLocked(mid));
    }

    [Fact]
    public async Task 最佳适配_剩余额度不足的账号应被跳过()
    {
        var small = SeedAccount("SMALL-50", 50m);
        var mid = SeedAccount("MID-200", 200m);

        var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 60m);

        Assert.Equal(mid, lockedId);
        Assert.Equal(0m, GetLocked(small));
    }

    [Fact]
    public async Task 无可用账号_应返回空且不改动任何账号()
    {
        var small = SeedAccount("SMALL-50", 50m);

        var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 5000m);

        Assert.Null(lockedId);
        Assert.Equal(0m, GetLocked(small));
    }

    [Fact]
    public async Task 未启用的账号_不应参与匹配()
    {
        var disabled = SeedAccount("DISABLED-1000", 1000m, status: PayAccountStatusEnum.Disabled);

        var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 10m);

        Assert.Null(lockedId);
        Assert.Equal(0m, GetLocked(disabled));
    }

    #endregion 最佳适配（F2.2）

    #region 额度不超发（F2.3）

    [Fact]
    public async Task 额度不足时_后续请求应拿不到账号而非超发()
    {
        // 额度 100，每笔 30 → 只应有 3 笔拿到账号（锁定 90），第 4 笔必须失败
        var accountId = SeedAccount("EXACT-100", 100m);

        for (var i = 0; i < 3; i++)
        {
            var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 30m);
            Assert.Equal(accountId, lockedId);
        }

        var overflow = await _allocateService.TryLockQuotaAsync(TestType, 30m);

        Assert.Null(overflow);
        Assert.Equal(90m, GetLocked(accountId));
        Assert.Equal(100m, GetAccount(accountId).TotalQuota);
    }

    [Fact]
    public async Task 恰好用尽额度_应能锁定()
    {
        var accountId = SeedAccount("EXACT-100", 100m);

        var lockedId = await _allocateService.TryLockQuotaAsync(TestType, 100m);

        Assert.Equal(accountId, lockedId);
        Assert.Equal(100m, GetLocked(accountId));
    }

    #endregion 额度不超发（F2.3）

    #region 额度释放与状态同步（F1.4）

    [Fact]
    public async Task 释放额度_应减少锁定额且不出现负数()
    {
        var accountId = SeedAccount("RELEASE-100", 100m);
        await _allocateService.TryLockQuotaAsync(TestType, 100m);
        Assert.Equal(100m, GetLocked(accountId));

        await _allocateService.ReleaseQuotaAsync(accountId, 100m);

        Assert.Equal(0m, GetLocked(accountId));
    }

    [Fact]
    public async Task 释放额度_超过已锁定额时应钳制为不减()
    {
        var accountId = SeedAccount("RELEASE-100", 100m);
        await _allocateService.TryLockQuotaAsync(TestType, 30m);

        // 释放比实际锁定更多的金额（异常路径防护），LockedQuota 不得变负
        await _allocateService.ReleaseQuotaAsync(accountId, 999m);

        Assert.Equal(30m, GetLocked(accountId));
    }

    [Fact]
    public async Task 剩余额度归零_账号应置为已用完()
    {
        var accountId = SeedAccount("EXHAUST-100", 100m);

        await _allocateService.TryLockQuotaAsync(TestType, 100m);

        Assert.Equal(PayAccountStatusEnum.Exhausted, GetAccount(accountId).Status);
        // 已用完的账号不再参与匹配
        Assert.Null(await _allocateService.TryLockQuotaAsync(TestType, 1m));
    }

    [Fact]
    public async Task 释放额度后恢复可用_账号状态应回到启用()
    {
        var accountId = SeedAccount("EXHAUST-100", 100m);
        await _allocateService.TryLockQuotaAsync(TestType, 100m);
        Assert.Equal(PayAccountStatusEnum.Exhausted, GetAccount(accountId).Status);

        await _allocateService.ReleaseQuotaAsync(accountId, 100m);

        Assert.Equal(PayAccountStatusEnum.Enabled, GetAccount(accountId).Status);
        Assert.NotNull(await _allocateService.TryLockQuotaAsync(TestType, 1m));
    }

    [Fact]
    public void 剩余可用额度_等于总额度减已用减锁定()
    {
        var account = new PayAccount
        {
            TotalQuota = 1000m, UsedQuota = 300m, LockedQuota = 200m
        };

        Assert.Equal(500m, PayAccountService.GetRemainingQuota(account));
    }

    #endregion 额度释放与状态同步（F1.4）
}
