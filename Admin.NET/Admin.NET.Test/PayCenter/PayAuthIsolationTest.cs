// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Admin.NET.Core.Service;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 鉴权加固的回归守卫（2026-09-17）：**调用方隔离**、**归属校验**、**密钥掩码**。
/// </summary>
/// <remarks>
/// <para>
/// 本类只覆盖「不报错但会静默出错」的那几类缺陷 —— 它们的共同特征是：
/// 出错时三方拿到的是**看起来正常**的响应（别人的订单、被静默去重的通知、明文密钥），
/// 而不是一个显式的错误。所以必须有测试盯着。
/// </para>
/// <para>
/// 与 <c>scripts/pay_schema_guard.py</c> 的分工：守卫脚本做**静态**断言（源码里有没有这个写法），
/// 本类做**行为**断言（跑起来是不是真的隔离了）。两者缺一不可 ——
/// 静态断言能防「改回去」，行为断言能防「写了但没生效」。
/// </para>
/// </remarks>
public class PayAuthIsolationTest : IDisposable
{
    /// <summary>本类专用收款类型</summary>
    private const string TestType = "unittest_authiso";

    /// <summary>本类专用外部单号前缀（清理依据）</summary>
    private const string ExternalNoPrefix = "unittest_authiso_";

    /// <summary>调用方 A 的 Id</summary>
    private const long ClientA = 9990000000101L;

    /// <summary>调用方 B 的 Id</summary>
    private const long ClientB = 9990000000102L;

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;

    public PayAuthIsolationTest()
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

    /// <summary>硬删除本类造数（审计 → 事件 → 订单 → 账号，顺序不能颠倒）</summary>
    private void Cleanup()
    {
        // ★ 审计行也要清：pay_audit_log 只增不改，但测试造数必须可重复运行，
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

    private long SeedAccount(decimal total)
    {
        var account = new PayAccount
        {
            Type = TestType,
            AccountInfo = "unittest-authiso-account",
            TotalQuota = total,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = PayAccountStatusEnum.Enabled,
            Remark = "单元测试数据"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    private static AllocateInput BuildInput(decimal amount, string externalNo) => new()
    {
        Type = TestType,
        Amount = amount,
        ExternalNo = externalNo
    };

    // ───────────────────────── 幂等键按调用方隔离 ─────────────────────────

    /// <summary>
    /// ★ P1-1 回归：A 与 B 用**同一个** externalNo 下单，必须各得各的订单。
    /// </summary>
    /// <remarks>
    /// 若唯一索引退回单列 `externalno`、或幂等查询漏了 `ClientId`，B 会命中 A 的订单 ——
    /// 表现为 B 拿到**别人的** OrderNo 与**完整收款账号**，而且双方都看不出异常。
    /// 这是本系统最危险的一类缺陷（跨调用方串单 + 信息泄漏），所以单独盯一条。
    /// </remarks>
    [Fact]
    public async Task 幂等键隔离_两个调用方用同一外部单号应各得各的订单()
    {
        SeedAccount(500m);
        var sharedExternalNo = ExternalNoPrefix + "shared";

        AllocateOutput a;
        using (PayTestCaller.Begin(ClientA, "unittest-key-a"))
        {
            a = await _allocateService.Allocate(BuildInput(30m, sharedExternalNo));
        }

        AllocateOutput b;
        using (PayTestCaller.Begin(ClientB, "unittest-key-b"))
        {
            b = await _allocateService.Allocate(BuildInput(40m, sharedExternalNo));
        }

        // 两笔独立订单，不是同一笔
        Assert.NotEqual(a.OrderNo, b.OrderNo);
        Assert.False(b.IdempotentHit); // B 不应命中 A 的订单

        // 落库各自归属正确
        var orderA = _db.Queryable<PayOrder>().First(u => u.OrderNo == a.OrderNo);
        var orderB = _db.Queryable<PayOrder>().First(u => u.OrderNo == b.OrderNo);
        Assert.Equal(ClientA, orderA.ClientId);
        Assert.Equal(ClientB, orderB.ClientId);
        Assert.Equal(30m, orderA.RequestAmount);
        Assert.Equal(40m, orderB.RequestAmount);

        // 同一调用方重复请求仍然幂等（隔离不能把幂等本身弄丢）
        using (PayTestCaller.Begin(ClientA, "unittest-key-a"))
        {
            var again = await _allocateService.Allocate(BuildInput(30m, sharedExternalNo));
            Assert.Equal(a.OrderNo, again.OrderNo);
            Assert.True(again.IdempotentHit);
        }

        // 共 2 笔，不是 3 笔
        Assert.Equal(2, _db.Queryable<PayOrder>().Count(u => u.ExternalNo == sharedExternalNo));
    }

    // ───────────────────────── 订单状态查询归属校验 ─────────────────────────

    /// <summary>
    /// ★ P1-2 回归：调用方不能查到别人的订单。
    /// </summary>
    /// <remarks>
    /// 关键不只是「抛错」，还有**抛的错必须与「订单不存在」不可区分** ——
    /// 否则该接口会退化成订单号的存在性探针（拿一串订单号去试，就能知道哪些存在）。
    /// </remarks>
    [Fact]
    public async Task 订单状态查询_调用方查别人的订单应与查不存在的订单报同样的错()
    {
        SeedAccount(200m);

        AllocateOutput a;
        using (PayTestCaller.Begin(ClientA, "unittest-key-a"))
        {
            a = await _allocateService.Allocate(BuildInput(20m, ExternalNoPrefix + "own"));
        }

        // ① 查自己的 → 正常返回
        using (PayTestCaller.Begin(ClientA, "unittest-key-a"))
        {
            var own = await _allocateService.GetStatus(a.OrderNo);
            Assert.Equal(a.OrderNo, own.OrderNo);
        }

        // ② B 查 A 的订单 → 抛错
        Exception crossCaller = null;
        using (PayTestCaller.Begin(ClientB, "unittest-key-b"))
        {
            crossCaller = await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.GetStatus(a.OrderNo));
        }

        // ③ A 查一个不存在的订单号 → 抛错
        Exception notFound = null;
        using (PayTestCaller.Begin(ClientA, "unittest-key-a"))
        {
            notFound = await Assert.ThrowsAnyAsync<Exception>(() => _allocateService.GetStatus("9999999999999999999999999999"));
        }

        // 两者报错必须一致（同一个错误码），否则越权查询就成了存在性探针
        Assert.NotNull(crossCaller);
        Assert.NotNull(notFound);
        Assert.Equal(notFound.Message, crossCaller.Message);
    }

    // ───────────────────────── 「无调用方」必须报错 ─────────────────────────

    /// <summary>
    /// ★ 防止 `?? 0` 兜底被改回来。
    /// </summary>
    /// <remarks>
    /// 退化成 0 号调用方**不会报错**，只会让所有接入方共享同一个去重命名空间 ——
    /// A 用过的凭证号会把 B 的到账通知静默去重掉（钱被吞、双方都看不到报错）。
    /// 所以这里断言的是「没有调用方身份时必须抛错」，而不是「能跑」。
    /// </remarks>
    [Fact]
    public async Task 无调用方身份_分配接口应抛错而不是退化成0号调用方()
    {
        SeedAccount(100m);

        // 刻意**不** Begin：模拟「接口没被签名鉴权保护」这一配置错误
        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => _allocateService.Allocate(BuildInput(10m, ExternalNoPrefix + "nocaller")));

        Assert.Contains("调用方", ex.Message);
        // 并且没有落任何订单（不能出现「先写了库再发现没身份」）
        Assert.Equal(0, _db.Queryable<PayOrder>().Count(u => u.ExternalNo == ExternalNoPrefix + "nocaller"));
    }

    // ───────────────────────── 密钥掩码语义 ─────────────────────────

    /// <summary>掩码保留首尾各 4 位，中间用 `****`（`*` 不在 Base64 字符集，无歧义）</summary>
    [Fact]
    public void 密钥掩码_应保留首尾各四位()
    {
        var secret = "abcdefghijklmnop"; // 16 位
        var masked = OpenAccessSecretMask.Mask(secret);

        Assert.Equal("abcd****mnop", masked);
        // 掩码本身必须能被识别为掩码（这是「掩码=不改」成立的前提）
        Assert.True(OpenAccessSecretMask.LooksLikeMask(masked));
        // 且掩码不能等于原文
        Assert.NotEqual(secret, masked);
    }

    /// <summary>太短的密钥直接返回裸标记，不泄漏任何字符</summary>
    [Fact]
    public void 密钥掩码_过短时只返回标记不泄漏字符()
    {
        Assert.Equal("****", OpenAccessSecretMask.Mask("abc"));
        Assert.Equal("****", OpenAccessSecretMask.Mask("12345678")); // 恰好 2×4
    }

    /// <summary>
    /// 「掩码 = 不改」：留空或回传**自身**掩码都视为不修改。
    /// </summary>
    /// <remarks>
    /// 这是编辑弹窗能安全工作的关键：列表接口只给掩码，用户不动它直接提交时，
    /// 后端必须把掩码理解成「保持原值」，而不是把 `abcd****mnop` 当成新密钥存进去。
    /// </remarks>
    [Fact]
    public void 密钥掩码_留空或回传自身掩码都表示不修改()
    {
        var existing = "abcdefghijklmnop";
        var mask = OpenAccessSecretMask.Mask(existing);

        Assert.True(OpenAccessSecretMask.MeansUnchanged(null, existing));
        Assert.True(OpenAccessSecretMask.MeansUnchanged("", existing));
        Assert.True(OpenAccessSecretMask.MeansUnchanged("   ", existing));
        Assert.True(OpenAccessSecretMask.MeansUnchanged(mask, existing));

        // 真正的新密钥 → 不是「不修改」
        Assert.False(OpenAccessSecretMask.MeansUnchanged("brandnewsecret", existing));
    }

    /// <summary>
    /// 含 `*` 但不是自身掩码的值必须被识别为「掩码形态」，由调用方拒绝。
    /// </summary>
    /// <remarks>
    /// 防的是「把别处的掩码当密钥存进来」：若静默存下 `****`，
    /// 该凭证的签名会用一个几乎无熵的密钥，且**不报任何错** —— 等于凭空造出一个弱密钥。
    /// </remarks>
    [Fact]
    public void 密钥掩码_外来掩码形态应被识别并拒绝()
    {
        var existing = "abcdefghijklmnop";

        // 别的凭证的掩码（或任何含 * 的值）
        Assert.True(OpenAccessSecretMask.LooksLikeMask("wxyz****1234"));
        Assert.True(OpenAccessSecretMask.LooksLikeMask("****"));

        // 它不等于本行的掩码 → 不该被当成「不修改」
        Assert.False(OpenAccessSecretMask.MeansUnchanged("wxyz****1234", existing));

        // 正常 Base64 密钥（生成器的字符集）不含 * → 不会被误判
        Assert.False(OpenAccessSecretMask.LooksLikeMask("AbCdEf12+/GhIjKlMnOpQr"));
    }
}
