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
/// 订单号生成（F2.3，方案 B）：时间戳前缀 + 雪花尾段，自包含
/// </summary>
/// <remarks>
/// <para>
/// 本类要钉住三件事：
/// <list type="number">
/// <item><b>格式契约</b>——纯数字、日期前置（三方按日期检索 / 流水匹配都靠它）；</item>
/// <item><b>唯一性</b>——串行与并发都不能出重号，且能直接落库（列宽够用）；</item>
/// <item><b>自包含</b>——<c>sysserial</c> 里<b>没有</b> <c>pay_order</c> 配置也照样能取号，
/// 这正是方案 B 相对骨架流水号的核心收益（模块可整体拆出独立部署）。</item>
/// </list>
/// </para>
/// <para>
/// 造数用独立订单号前缀 <c>UTNO</c>，构造与析构各清理一次（绕过软删除过滤器）。
/// </para>
/// </remarks>
public class PayOrderNoTest : IDisposable
{
    /// <summary>本类专用订单号前缀</summary>
    private const string OrderNoPrefix = "UTNO";

    /// <summary>订单号格式：17 位时间戳（yyyyMMddHHmmssfff）+ 15~16 位雪花尾段</summary>
    private static readonly Regex OrderNoPattern = new(@"^\d{32,40}$");

    private readonly ISqlSugarClient _db;
    private readonly PayOrderNoGenerator _generator;

    public PayOrderNoTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _generator = App.GetRequiredService<PayOrderNoGenerator>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    /// <summary>硬删除本类造数</summary>
    /// <remarks>
    /// 注意按 <c>externalno</c> 而不是 <c>orderno</c> 清理：订单号现在由雪花生成，
    /// 是纯数字、无法带测试前缀，测试标识只能挂在外部单号上。
    /// </remarks>
    private void Cleanup()
    {
        _db.Ado.ExecuteCommand("delete from pay_order where externalno like @prefix",
            new SugarParameter("@prefix", OrderNoPrefix + "%"));
    }

    [Fact]
    public void 取号_应为纯数字且日期前置()
    {
        var before = DateTime.Now;
        var orderNo = _generator.Next();
        var after = DateTime.Now;

        Assert.Matches(OrderNoPattern, orderNo);

        // 前 17 位就是生成时刻的 yyyyMMddHHmmssfff。
        // 允许跨秒：取号瞬间可能正好跨过一秒边界，故在 [before, after] 之间取或。
        var prefix = orderNo[..17];
        var candidates = new[]
        {
            before.ToString("yyyyMMddHHmmssfff"),
            after.ToString("yyyyMMddHHmmssfff")
        };
        Assert.Contains(prefix, candidates);

        // 严格日期前置：前 8 位必须是 yyyyMMdd（三方按日期检索的依据）。
        // 同样容忍跨零点这一种边界。
        Assert.Contains(orderNo[..8], new[] { before.ToString("yyyyMMdd"), after.ToString("yyyyMMdd") });
    }

    [Fact]
    public void 连续取号_应无重号()
    {
        const int count = 5000;
        var numbers = new List<string>(count);
        for (var i = 0; i < count; i++) numbers.Add(_generator.Next());

        Assert.Equal(count, numbers.Distinct().Count());
    }

    [Fact]
    public void 并发取号_不应产生重复号()
    {
        // 取号是纯内存操作（无锁、无 IO），这里用多线程真并发压一遍，
        // 确保雪花尾段在高并发下不会因为同一毫秒内的序列耗尽而出重号。
        var bag = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 2000, _ => bag.Add(_generator.Next()));

        var numbers = bag.ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void 取号_不应依赖sysserial配置()
    {
        // 方案 B 的核心断言：把该类型在 sysserial 里的配置删干净（本来种子也已移除），
        // 取号仍必须成功——因为订单号根本不读这张表。
        _db.Ado.ExecuteCommand("delete from sysserial where type = @type",
            new SugarParameter("@type", "pay_order"));

        var exists = _db.Ado.SqlQuery<long>(
            "select id from sysserial where type = @type",
            new SugarParameter("@type", "pay_order"));
        Assert.Empty(exists);

        var orderNo = _generator.Next();
        Assert.Matches(OrderNoPattern, orderNo);
    }

    [Fact]
    public void 订单号_应能直接落库且不超列宽()
    {
        var orderNo = _generator.Next();
        Assert.True(orderNo.Length <= PayConst.OrderNoLength,
            $"订单号长度 {orderNo.Length} 超过列宽 {PayConst.OrderNoLength}");

        var order = new PayOrder
        {
            OrderNo = orderNo,
            ExternalNo = OrderNoPrefix + "-" + Guid.NewGuid().ToString("N")[..12],
            RequestAmount = 1m,
            AccountId = 0,
            ReceivedAmount = 0m,
            Status = PayOrderStatusEnum.Pending,
            ExpireTime = DateTime.Now.AddMinutes(30)
        };
        _db.Insertable(order).ExecuteCommand();

        var saved = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(orderNo, saved.OrderNo);
    }

    [Fact]
    public void 订单号_应按时间单调递增()
    {
        // 雪花尾段在同一进程内单调，加上毫秒时间戳前缀 → 整体字符串序 == 时间序。
        // 三方按订单号排序时不需要额外解析，这一点必须成立。
        var numbers = Enumerable.Range(0, 500).Select(_ => _generator.Next()).ToList();

        Assert.Equal(numbers.OrderBy(u => u, StringComparer.Ordinal).ToList(), numbers);
    }

    [Fact]
    public void 订单号长度_必须为雪花进位留出余量()
    {
        // ★ 这条不是「当下能不能过」，而是「进位后还能不能过」。
        //
        // 订单号 = 17 位时间戳 + 雪花尾段，雪花位数随年份增长（约 2027-11 起 15 → 16 位）。
        // 如果列宽/校验上限刚好等于当前长度（历史上出现过 32 位卡 32 位），
        // 进位当天就会全面失败。所以要求**至少留 1 位余量**。
        var orderNo = _generator.Next();

        Assert.True(orderNo.Length < PayConst.OrderNoLength,
            $"订单号当前 {orderNo.Length} 位，列宽 {PayConst.OrderNoLength} 位：已无进位余量，" +
            "雪花位数增长后写库与参数校验都会失败");
    }
}
