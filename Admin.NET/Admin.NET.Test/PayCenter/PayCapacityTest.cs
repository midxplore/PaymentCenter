// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 开放 capacity：可匹配池可用额度合计 / 单码最大 / allocatable 探测
/// </summary>
public class PayCapacityTest : IDisposable
{
    private const string TestType = "unittest_capacity";

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;

    public PayCapacityTest()
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

    private long Seed(string info, decimal total, decimal used = 0m, decimal locked = 0m,
        PayAccountStatusEnum status = PayAccountStatusEnum.Enabled,
        PayAccountTierEnum tier = PayAccountTierEnum.Large,
        decimal minAmount = 0m)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = info,
            TotalQuota = total,
            UsedQuota = used,
            LockedQuota = locked,
            Status = status,
            AccountTier = tier,
            MinAmount = minAmount,
            DispatchMode = PayDispatchModeEnum.Weighted,
            Remark = "单元测试 capacity"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private void SeedPendingOrders(long accountId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _db.Insertable(new PayOrder
            {
                OrderNo = $"CAP{accountId}{i}{DateTime.Now:HHmmssfff}{Random.Shared.Next(1000, 9999)}",
                ExternalNo = $"cap_ext_{accountId}_{i}_{Guid.NewGuid():N}"[..32],
                AccountId = accountId,
                RequestAmount = 10m,
                ReceivedAmount = 0m,
                Status = PayOrderStatusEnum.Pending,
                ExpireTime = DateTime.Now.AddHours(1),
                ClientId = null
            }).ExecuteCommand();
        }
    }

    [Fact]
    public async Task 合计与单码最大_应扣预占并忽略禁用码()
    {
        Seed("A", 100m, locked: 20m);          // remain 80
        Seed("B", 50m);                         // remain 50
        Seed("OFF", 999m, status: PayAccountStatusEnum.Disabled);

        var cap = await _allocateService.GetCapacity(TestType);

        Assert.Equal(TestType, cap.Type);
        Assert.Equal(130m, cap.AvailableAmount);
        Assert.Equal(80m, cap.MaxSingleAmount);
        Assert.Null(cap.Allocatable);
        Assert.True(cap.AsOf <= DateTime.Now.AddSeconds(2));
    }

    [Fact]
    public async Task 带金额_可分配与不可分配()
    {
        Seed("SMALL", 30m, minAmount: 0m);
        Seed("HIGH_MIN", 500m, minAmount: 200m);

        var ok = await _allocateService.GetCapacity(TestType, "25.00");
        Assert.True(ok.Allocatable);

        var tooBig = await _allocateService.GetCapacity(TestType, "40.00");
        Assert.False(tooBig.Allocatable); // 仅 SMALL 可接但剩余 30；HIGH_MIN 门槛 200

        var highOk = await _allocateService.GetCapacity(TestType, "200.00");
        Assert.True(highOk.Allocatable);
    }

    [Fact]
    public async Task 普通账号槽位占满_不计入可用()
    {
        var full = Seed("FULL", 1000m, tier: PayAccountTierEnum.Normal);
        SeedPendingOrders(full, PayConst.NormalAccountMaxNonExpiredOrders);
        Seed("FREE", 100m, tier: PayAccountTierEnum.Large);

        var cap = await _allocateService.GetCapacity(TestType);
        Assert.Equal(100m, cap.AvailableAmount);
        Assert.Equal(100m, cap.MaxSingleAmount);

        var probe = await _allocateService.GetCapacity(TestType, "50.00");
        Assert.True(probe.Allocatable);
    }

    [Fact]
    public async Task 空池_返回零且allocatable为false()
    {
        var cap = await _allocateService.GetCapacity(TestType, "10.00");
        Assert.Equal(0m, cap.AvailableAmount);
        Assert.Equal(0m, cap.MaxSingleAmount);
        Assert.False(cap.Allocatable);
    }

    [Fact]
    public async Task 非法金额_应报错()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.GetCapacity(TestType, "0"));
        await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.GetCapacity(TestType, "1.234"));
    }
}
