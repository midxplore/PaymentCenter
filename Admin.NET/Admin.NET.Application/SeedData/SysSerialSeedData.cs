// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 流水号种子数据
/// </summary>
/// <remarks>
/// <para>
/// 这里只放「业务流水号」的类型定义；取号逻辑见 <see cref="SysSerialService"/>。
/// 类型字段来自 <see cref="SerialTypeProvider"/>（后台「系统流水号」页面的下拉项由它扫描生成）。
/// </para>
/// <para>
/// <b>收款订单号</b>（<c>pay_order</c>）：纯数字、日期前置、<b>按日重置</b>，
/// 形如 <c>202609160000001</c>（<c>{SEQ}</c> 按 <c>Max</c> 位数补零 → 7 位）。
/// 刻意不加业务前缀：三方（银行 / 支付渠道）拿到订单号即可按日期检索、按序号排序，
/// 纯数字也便于在其流水文件里直接做字符串匹配。该值只是种子默认值，后台可改。
/// </para>
/// <para>
/// ⚠️ 不要把它改回 <c>ResetInterval = Never</c>：格式里带了 <c>{yyyy}{MM}{dd}</c>，
/// 语义上就是「按日编号」，配成不重置会让序号无限增长并最终撞上 <c>Max</c> 上限。
/// </para>
/// </remarks>
[SeedData(500)]
[IncreSeed]
public class SysSerialSeedData : ISqlSugarEntitySeedData<SysSerial>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysSerial> HasData()
    {
        return
        [
            new(){ Id=1300000000101, Type=SerialTypeProvider.Test, ResetInterval=ResetIntervalEnum.Day, Formater="T{yyyy}{MM}{SEQ}", Seq=0, Min=1, Max=9999999, Expy=DateTime.Parse("2025-01-01 12:00:00"), TenantId=SqlSugarConst.DefaultTenantId, OrderNo=100 },
            // 注：收款订单号**不再**走骨架流水号（F2.3 方案 B）。
            // 订单号由 PayOrderNoGenerator 以「时间戳 + 雪花尾段」自包含生成，
            // 不在 sysserial 里占配置，模块可整体拆出独立部署。
        ];
    }
}
