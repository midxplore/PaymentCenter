// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Text.RegularExpressions;
using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 查询匹配端到端（F2）：最佳适配 → 原子预占 → 落订单 → 事件流水 → 幂等
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="PayQuotaTest"/> 的分工：后者只验证额度预占与状态迁移（直接调 <c>TryLockQuotaAsync</c>），
/// 本类验证<b>完整调用链</b>——尤其是订单号是否真的由
/// <see cref="PayOrderNoGenerator"/> 的「时间戳 + 雪花尾段」产出（格式见该类注释）。
/// </para>
/// <para>造数用独立的收款类型与外部单号前缀，构造与析构各清理一次。</para>
/// </remarks>
public class PayAllocateFlowTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_alloc";

    /// <summary>本类专用外部单号前缀</summary>
    private const string ExternalNoPrefix = "unittest_alloc_";

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;
    private readonly IDisposable _caller;

    public PayAllocateFlowTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _allocateService = App.GetRequiredService<PayAllocateService>();
        // ★ 扮演一个调用方：Allocate 必须能解析出 ClientId，否则会抛
        //   「无法解析调用方身份」（见 PayTestCaller 的说明）。
        //   放在构造函数而不是逐个用例加 using，是为了避免「某个用例忘了加」——
        //   忘了加**不会报错**，只会让 Assert.ThrowsAnyAsync 因**别的原因**通过（假绿）：
        //   本类里 `无可用账号_应报错且不生成订单` 就是这么被掩盖的。
        _caller = PayTestCaller.Begin();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        _caller.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数（审计 → 订单事件 → 订单 → 账号，顺序不能颠倒）</summary>
    private void Cleanup()
    {
        // ★ 审计行也要清：pay_audit_log 是**只增不改**的业务表，但测试造数必须可重复运行 ——
        //   否则每跑一次就留一批 targetid 指向已删订单的孤儿审计行。
        //   必须**先于删订单**执行：子查询依赖 pay_order 还在。
        _db.Ado.ExecuteCommand(
            "delete from pay_audit_log where targettype = 'Order' and targetid in (select id from pay_order where externalno like @prefix)",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand(
            "delete from pay_order_event where orderno in (select orderno from pay_order where externalno like @prefix)",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where externalno like @prefix",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long SeedAccount(decimal total, PayAccountStatusEnum status = PayAccountStatusEnum.Enabled)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = "unittest-account-info",
            TotalQuota = total,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = status,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private static AllocateInput BuildInput(decimal amount, string externalNo = null) => new()
    {
        Type = TestType,
        Amount = amount,
        ExternalNo = externalNo
    };

    [Fact]
    public async Task 匹配成功_应生成订单与创建事件_订单号走雪花自包含生成()
    {
        var accountId = SeedAccount(100m);

        var output = await _allocateService.Allocate(BuildInput(40m, ExternalNoPrefix + "ok"));

        // 订单号格式来自 PayOrderNoGenerator = {yyyyMMddHHmmssfff}{雪花Id}
        // （纯数字、日期前置，便于三方查账），形如 20260917002812345849000000000000
        Assert.Matches(new Regex(@"^\d{32,40}$"), output.OrderNo);
        Assert.Equal(TestType, output.Type);
        Assert.Equal("unittest-account-info", output.AccountInfo);
        Assert.Equal(40m, output.RequestAmount);
        Assert.False(output.IdempotentHit);
        Assert.True(output.ExpireTime > DateTime.Now);

        // 落库核对
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        Assert.Equal(accountId, order.AccountId);
        Assert.Equal(PayOrderStatusEnum.Pending, order.Status);
        Assert.Equal(0m, order.ReceivedAmount);

        // 额度已预占（锁定 40），已用仍为 0
        var account = _db.Queryable<PayAccount>().First(u => u.Id == accountId);
        Assert.Equal(40m, account.LockedQuota);
        Assert.Equal(0m, account.UsedQuota);

        // 事件流水（只增不改）
        var events = _db.Queryable<PayOrderEvent>().Where(u => u.OrderId == order.Id).ToList();
        Assert.Single(events);
        Assert.Equal(PayEventTypeEnum.Created, events[0].EventType);
    }

    [Fact]
    public async Task 同一外部单号重复请求_应命中幂等且不重复占额度()
    {
        var accountId = SeedAccount(100m);
        var externalNo = ExternalNoPrefix + "idem";

        var first = await _allocateService.Allocate(BuildInput(30m, externalNo));
        var second = await _allocateService.Allocate(BuildInput(30m, externalNo));

        Assert.Equal(first.OrderNo, second.OrderNo);
        Assert.False(first.IdempotentHit);
        Assert.True(second.IdempotentHit);

        // 只落一笔订单、只占一次额度
        Assert.Equal(1, _db.Queryable<PayOrder>().Count(u => u.ExternalNo == externalNo));
        Assert.Equal(30m, _db.Queryable<PayAccount>().First(u => u.Id == accountId).LockedQuota);
    }

    [Fact]
    public async Task 无可用账号_应报错且不生成订单()
    {
        // 只造一个额度不足的账号
        SeedAccount(10m);

        await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.Allocate(BuildInput(500m, ExternalNoPrefix + "noacc")));

        Assert.Equal(0, _db.Queryable<PayOrder>().Count(u => u.ExternalNo == ExternalNoPrefix + "noacc"));
    }

    [Fact]
    public async Task 额度恰好用尽后_该账号应被自动下架且不再被匹配()
    {
        var accountId = SeedAccount(100m);

        var output = await _allocateService.Allocate(BuildInput(100m, ExternalNoPrefix + "exhaust"));
        Assert.NotNull(output.OrderNo);

        var account = _db.Queryable<PayAccount>().First(u => u.Id == accountId);
        Assert.Equal(PayAccountStatusEnum.Exhausted, account.Status);
        Assert.Equal(100m, account.LockedQuota);

        // 已用完的账号不再参与匹配
        await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.Allocate(BuildInput(1m, ExternalNoPrefix + "after")));
    }
}
