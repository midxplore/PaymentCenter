// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款订单表（F2.3）
/// </summary>
[SugarTable("pay_order", "收款订单表")]
[SysTable]
[SugarIndex("u_{table}_no", nameof(OrderNo), OrderByType.Asc, IsUnique = true)]
// ★ 幂等键是 (ClientId, ExternalNo) 组合，**不是** ExternalNo 单列。
//   详见 ExternalNo 属性上的说明。
[SugarIndex("u_{table}_ce", nameof(ClientId), OrderByType.Asc, nameof(ExternalNo), OrderByType.Asc, IsUnique = true)]
[SugarIndex("i_{table}_aid", nameof(AccountId), OrderByType.Asc)]
[SugarIndex("i_{table}_se", nameof(Status), OrderByType.Asc, nameof(ExpireTime), OrderByType.Asc)]
public class PayOrder : EntityBase
{
    /// <summary>
    /// 系统订单号（唯一，由 PayOrderNoGenerator 自包含生成）
    /// </summary>
    [SugarColumn(ColumnDescription = "系统订单号", Length = PayConst.OrderNoLength)]
    [MaxLength(PayConst.OrderNoLength)]
    public virtual string OrderNo { get; set; }

    /// <summary>
    /// 外部业务单号（调用方幂等键的一部分，允许为空；F2.6）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>唯一性是 <c>(ClientId, ExternalNo)</c> 组合，不是本列单独唯一。</b>
    /// </para>
    /// <para>
    /// <b>为什么必须组合</b>：<c>ExternalNo</c> 是**调用方自己的业务单号**，
    /// 只在他自己的命名空间里有意义。若把它当全局唯一键，就会出现：
    /// 接入方 B 用了他业务里正常的单号（如 <c>20260917001</c>），
    /// 恰好与接入方 A 的单号相同 → B 的请求命中 A 的订单 →
    /// **B 拿到 A 的订单号与 A 的收款账号明文**，而 B 自己的收款请求根本没被分配。
    /// 后果是「钱可能进错账户」+「跨调用方数据泄漏」，且双方都不会收到任何报错。
    /// </para>
    /// <para>
    /// 组合之后语义才正确：<b>同一调用方的同一业务单号 → 幂等返回同一订单；
    /// 不同调用方的相同单号 → 互不影响</b>。
    /// </para>
    /// <para>
    /// <b>NULL 的处理</b>：<c>ExternalNo</c> 可为空（调用方不要求幂等时）。
    /// PostgreSQL 的组合唯一索引中 NULL 之间互不冲突（默认 <c>NULLS DISTINCT</c>），
    /// 所以不传 ExternalNo 的订单可以有多条，无需特殊处理。
    /// </para>
    /// <para>
    /// ⚠️ <b>索引名变更必须同步删旧索引</b>：SqlSugar CodeFirst 只会**新建**
    /// <c>u_pay_order_ce</c>，**不会**删掉历史上的 <c>u_pay_order_en</c>（单列唯一）。
    /// 旧索引残留会让「跨调用方同单号」继续被数据库拒绝（表现为 <c>D1000</c> 之类的写库异常），
    /// 必须由 <c>scripts/paycenter-schema.sql</c> 显式 <c>DROP INDEX</c>，
    /// 并由 <c>scripts/pay_schema_guard.py</c> 断言旧索引已消失。
    /// </para>
    /// </remarks>
    [SugarColumn(ColumnDescription = "外部业务单号", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string ExternalNo { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    [SugarColumn(ColumnDescription = "请求金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal RequestAmount { get; set; }

    /// <summary>
    /// 匹配到的收款账号Id
    /// </summary>
    [SugarColumn(ColumnDescription = "收款账号Id")]
    public virtual long AccountId { get; set; }

    /// <summary>
    /// 累计到账金额
    /// </summary>
    [SugarColumn(ColumnDescription = "累计到账金额", ColumnDataType = "decimal(18,2)")]
    public virtual decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    [SugarColumn(ColumnDescription = "订单状态")]
    public virtual PayOrderStatusEnum Status { get; set; } = PayOrderStatusEnum.Pending;

    /// <summary>
    /// 过期时间（创建时按全局配置算出）
    /// </summary>
    [SugarColumn(ColumnDescription = "过期时间")]
    public virtual DateTime ExpireTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [SugarColumn(ColumnDescription = "完成时间", IsNullable = true)]
    public virtual DateTime? CompleteTime { get; set; }

    /// <summary>
    /// 超额到账备注（F4.4）
    /// </summary>
    [SugarColumn(ColumnDescription = "超额到账备注", Length = 256, IsNullable = true)]
    [MaxLength(256)]
    public virtual string OverpayRemark { get; set; }

    /// <summary>
    /// 调用方标识（SysOpenAccess.Id），审计用
    /// </summary>
    [SugarColumn(ColumnDescription = "调用方Id", IsNullable = true)]
    public virtual long? ClientId { get; set; }
}
