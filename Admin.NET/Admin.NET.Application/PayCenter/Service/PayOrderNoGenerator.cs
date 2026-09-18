// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Yitter.IdGenerator;

namespace Admin.NET.Application;

/// <summary>
/// 订单号生成器（F2.3，方案 B）：时间戳前缀 + 雪花尾段，完全自包含
/// </summary>
/// <remarks>
/// <para>
/// <b>格式</b>：<c>{yyyyMMddHHmmssfff}{雪花Id}</c>，纯数字、日期前置，
/// 形如 <c>20260917002812345849000000000000</c>。
/// </para>
/// <list type="bullet">
/// <item><b>日期前置（前 17 位）</b>：三方（银行 / 支付渠道）拿到订单号即可直接按日期检索、
/// 在流水文件里做前缀匹配，不需要先解析雪花时间戳。</item>
/// <item><b>纯数字、无业务前缀</b>：三方流水文件里直接做字符串匹配，不涉及前缀剥离。</item>
/// <item><b>雪花尾段</b>：由框架已注册的 <see cref="YitIdHelper"/> 产出（<c>SqlSugarSetup.AddSqlSugar</c>
/// 里 <c>services.AddYitIdHelper(...)</c> + <c>StaticConfig.CustomSnowFlakeFunc</c>），
/// 时间戳 + WorkId + 序列号三段保证<b>跨进程、跨重启全局唯一</b>。</item>
/// </list>
/// <para>
/// <b>为什么走方案 B（自包含）而不是骨架 <c>SysSerial</c> 流水号</b>：
/// 见设计文档 §12 与 §5.1——本模块要能整体拆出去独立部署，
/// 不希望在 <c>sysserial</c> 表上再挂一份配置与租户上下文依赖。
/// 这里只用框架的<b>库</b>（<c>Yitter.IdGenerator</c>，实体主键本来就在用），
/// 不依赖框架的<b>表与后台配置</b>。
/// </para>
/// <para>
/// <b>为什么不用 <c>SysSerialService.NextSeqNo</c></b>：它每次取号都要抢一把分布式锁
/// （<c>SysCacheService.BeginCacheLock</c>，1 秒超时），实测 30 并发起大量请求会因锁等待超时
/// 抛「服务正忙，请稍后再试！」——这是<b>可用性缺陷</b>（取不到号的订单直接失败），不是性能问题。
/// 雪花号在进程内生成，取号路径上没有任何锁竞争。
/// </para>
/// <para>
/// <b>长度与列宽</b>：<c>DateTime.Now</c> 17 位 + 雪花 15~16 位 ≈ 32~33 位
/// （雪花位数随年份增长，约 2027-11 起变 16 位）。因此
/// <c>pay_order.orderno</c> 等相关列按 <see cref="PayConst.OrderNoLength"/>（64）建表，
/// 留足余量，避免几年后因位数增长而写库失败。
/// </para>
/// <para>无状态、可注册为单例；<see cref="Next"/> 不碰数据库，可在事务外调用。</para>
/// </remarks>
public class PayOrderNoGenerator : ISingleton
{
    /// <summary>
    /// 取下一个订单号
    /// </summary>
    /// <returns>可直接落库的订单号（纯数字、日期前置）</returns>
    public string Next() => $"{DateTime.Now:yyyyMMddHHmmssfff}{YitIdHelper.NextId()}";
}
