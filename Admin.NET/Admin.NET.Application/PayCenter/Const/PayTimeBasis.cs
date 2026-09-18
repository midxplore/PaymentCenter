// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 时间基准守卫
/// <para>
/// ★ 本系统的时间基准是<b>固定 +08:00（北京时间）的墙上时间</b>，而不是 UTC、也不是带时区的时刻。
/// 这不是随口约定，而是由存储层类型决定的：
/// </para>
/// <list type="bullet">
///   <item>存储：<c>pay_*</c> 全部 17 个时间列都是 PostgreSQL <c>timestamp without time zone</c>（不带时区），
///     列里存的字面值<b>就是</b>显示值，数据库不知道它属于哪个时区。</item>
///   <item>写入：应用与框架 AOP（<c>SqlSugarSetup</c> 的 <c>CreateTime</c>）全部用 <c>DateTime.Now</c>（本地时间）。</item>
///   <item>读入：MVC 走 Newtonsoft，<c>DateTimeZoneHandling = Local</c> —— 入参带 <c>Z</c> 或 <c>+08:00</c>
///     会被归一化成本地时间，不带偏移的按本地解释。</item>
///   <item>输出：<c>yyyy-MM-dd HH:mm:ss</c>，不带偏移。</item>
/// </list>
/// <para>
/// 只要进程时区是 +08，整条链路自洽。但一旦进程时区不是 +08（最常见的是容器默认 UTC），
/// <c>DateTime.Now</c> 会变成 UTC 墙上时间、入参也会被归一化成 UTC，
/// 于是<b>同一列里会混入两种含义</b>：历史行是 +08，新行是 UTC，相差 8 小时，
/// 而且<b>不会有任何报错</b> —— 对账、超时判断、订单号日期前缀全部静默错位。
/// </para>
/// <para>
/// 所以这里把它变成<b>启动期硬失败</b>：宁可起不来，也不要带着错误的时间语义对外服务。
/// </para>
/// <para>
/// ★ 若将来决定改为 UTC + <c>timestamptz</c>（见设计文档的时间基准章节），
/// 需要同时改三处：本守卫、<c>scripts/paycenter-schema.sql</c> 的列类型迁移、以及前端时间展示。
/// </para>
/// </summary>
public static class PayTimeBasis
{
    /// <summary>
    /// 系统要求的时间基准偏移：固定 +08:00
    /// </summary>
    public static readonly TimeSpan RequiredOffset = TimeSpan.FromHours(8);

    /// <summary>
    /// 判断给定偏移是否符合约定
    /// </summary>
    public static bool IsExpectedOffset(TimeSpan offset) => offset == RequiredOffset;

    /// <summary>
    /// 判断「冬夏两个时点的偏移」是否都符合约定。
    /// <para>
    /// 抽成纯函数是为了可测：不用真的把机器时区改错，就能验证两条分支。
    /// </para>
    /// </summary>
    public static bool IsYearRoundExpected(TimeSpan winter, TimeSpan summer)
        => IsExpectedOffset(winter) && IsExpectedOffset(summer);

    /// <summary>
    /// 取进程当前时区在本年度「冬/夏两个时点」的偏移。
    /// <para>
    /// 取两个时点而不是只看当前，是为了识别<b>会随季节变化的时区</b>：
    /// 只有「两个时点都是 +08」才能保证全年都是固定 +08，
    /// 否则某一天偏移会自己变掉，而列里存的还是无时区字面值 —— 同样静默错位。
    /// （不用 <c>TimeZoneInfo.SupportsDaylightSavingTime</c>：它对 Asia/Shanghai 也会返回 true，
    /// 因为 tzdata 里含 1986–1991 年的历史夏令时记录，用它会误报。）
    /// </para>
    /// </summary>
    public static (TimeSpan Winter, TimeSpan Summer) GetYearRoundOffsets(int year)
    {
        // 取 1 月中与 7 月中：北半球覆盖冬/夏；对南半球也足以覆盖两次调整
        var winter = TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, 1, 15, 12, 0, 0, DateTimeKind.Unspecified));
        var summer = TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, 7, 15, 12, 0, 0, DateTimeKind.Unspecified));
        return (winter, summer);
    }

    /// <summary>
    /// 校验进程时区是否符合时间基准约定；不符合直接抛异常（启动期失败）。
    /// </summary>
    public static void EnsureLocalTimeBasis()
    {
        var year = DateTime.Now.Year;
        var (winter, summer) = GetYearRoundOffsets(year);

        if (IsYearRoundExpected(winter, summer)) return;

        throw new InvalidOperationException(
            BuildMismatchMessage(TimeZoneInfo.Local, year, winter, summer));
    }

    /// <summary>
    /// 构造时区不符的报错文案。
    /// <para>
    /// 单独抽出来是为了可测：文案里必须保留「怎么修」和「为什么不能只删守卫」，
    /// 否则后来的人只会看到一句「时区不对」，然后去把守卫删掉。
    /// </para>
    /// </summary>
    public static string BuildMismatchMessage(TimeZoneInfo tz, int year, TimeSpan winter, TimeSpan summer)
    {
        return $"""
             ✗ 时间基准不符合约定：本进程所在时区不是固定 UTC+08:00。

               当前时区      : {tz.Id}（DisplayName: {tz.DisplayName}）
               {year}-01 偏移 : {FormatOffset(winter)}
               {year}-07 偏移 : {FormatOffset(summer)}
               要求偏移      : UTC+08:00（冬夏一致）

             ★ 为什么必须硬失败：pay_* 的全部时间列都是 PostgreSQL `timestamp without time zone`，
               存的是「+08 墙上时间」字面值。写入用 DateTime.Now、入参按本地归一化、
               输出不带偏移 —— 三者只在进程时区为 +08 时才自洽。
               时区不对时，新数据会与历史数据在同一列里混入两种含义（相差 8 小时），
               且**不会有任何报错**：对账、订单超时判断、订单号日期前缀会全部静默错位。

             → 修复：设置环境变量 TZ=Asia/Shanghai 后重启（容器编排与 systemd 都要设）。
               验证：`date` 应显示 CST (+0800)；启动日志里本守卫不再抛出。
               若确实要改为 UTC + timestamptz，请勿只删本守卫 —— 需按设计文档
               「时间基准」章节同步迁移列类型与前端展示。
            """;
    }

    /// <summary>
    /// 把偏移格式化成 <c>UTC+08:00</c> 形式（不用格式字符串，避免转义歧义）
    /// </summary>
    public static string FormatOffset(TimeSpan offset)
    {
        var d = offset.Duration();
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"UTC{sign}{(int)d.TotalHours:D2}:{d.Minutes:D2}";
    }
}
