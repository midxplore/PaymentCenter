// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号分配系统常量
/// </summary>
public class PayConst
{
    /// <summary>
    /// 订单全局过期时长（分钟）（F3.1），落点为系统配置项
    /// </summary>
    public const string OrderExpireMinutes = "pay_order_expire_minutes";

    /// <summary>
    /// 订单号长度上限（建表列宽）
    /// </summary>
    /// <remarks>
    /// 订单号格式为 <c>{yyyyMMddHHmmssfff}{雪花Id}</c>（见 <see cref="PayOrderNoGenerator"/>），
    /// 当前约 32~33 位，且雪花位数会随年份增长（约 2027-11 起由 15 位变 16 位）。
    /// 这里一次留足余量，避免几年后因位数增长而写库失败。
    /// </remarks>
    public const int OrderNoLength = 64;

    /// <summary>
    /// 金额小数位上限（与列精度 <c>numeric(18,2)</c> 一致）
    /// </summary>
    /// <remarks>
    /// ★ <b>必须与 <c>scripts/paycenter-schema.sql</c> 里金额列的 scale 保持一致。</b>
    /// 本常量用于在**入参侧**拒绝超出精度的金额，而不是让数据库默默四舍五入。
    /// </remarks>
    public const int AmountScale = 2;

    /// <summary>
    /// 金额是否超出允许的小数位
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么要在入参侧拦，而不是交给数据库四舍五入</b>：金额列是 <c>numeric(18,2)</c>，
    /// 提交 <c>1.005</c> 会被 PostgreSQL 存成 <c>1.01</c>（银行家舍入），
    /// 但接口响应回显的是未舍入的原值 —— 实测提交 <c>"1.005"</c> 时
    /// 响应 <c>requestAmount="1.005"</c> 而库里是 <c>1.01</c>。
    /// 于是接入方按响应记账、平台按库内金额结算，两边静默对不上：
    /// 既不报错，也没有任何一条日志能指出是哪一笔出的问题。
    /// 所以在入口直接拒绝，让调用方自己决定如何取整。
    /// </para>
    /// <para>
    /// 实现方式：把小数部分放大到 <c>AmountScale</c> 位后比较是否仍为整数，
    /// 全程用 <see cref="decimal"/>（不使用 double），避免浮点误差造成误判。
    /// </para>
    /// </remarks>
    /// <param name="amount">待校验金额</param>
    /// <returns>超出精度返回 true</returns>
    public static bool HasExcessScale(decimal amount)
    {
        var factor = 1m;
        for (var i = 0; i < AmountScale; i++) factor *= 10m;
        return decimal.Truncate(amount * factor) != amount * factor;
    }

    /// <summary>
    /// 收款类型字典编码（SysDictType.Code）
    /// </summary>
    public const string AccountTypeDictCode = "pay_account_type";

    /// <summary>
    /// 缓存 key 前缀
    /// </summary>
    public const string CacheKeyPrefix = "paycenter_";

    /// <summary>
    /// 订单全局过期时长默认值（分钟）
    /// </summary>
    public const int DefaultOrderExpireMinutes = 30;

    /// <summary>
    /// 过期扫描单批次处理条数
    /// </summary>
    /// <remarks>
    /// 分批是为了避免一次扫描把大表全捞出来、并让长事务变短（F3.2）。
    /// 每分钟跑一轮，一轮最多处理这么多单；积压时会连续多轮消化。
    /// </remarks>
    public const int ExpireScanBatchSize = 200;

    /// <summary>
    /// 订单过期扫描作业标识（F3.2，对应 <c>[JobDetail]</c>）
    /// </summary>
    public const string ExpireJobId = "pay_order_expire_job";

    /// <summary>
    /// 订单过期扫描触发器标识（F3.2，对应 <c>[Minutely]</c>）
    /// </summary>
    public const string ExpireJobTriggerId = "pay_order_expire_trigger";

    /// <summary>
    /// 导出时间区间上限（天）（F7.5）
    /// </summary>
    /// <remarks>
    /// 导出的查询条件是时间区间且**不分页**，区间开太大就是一次全表扫描 + 全量载入内存。
    /// 这里给一个显式上限并在超限时报错提示缩小范围，比让服务悄悄 OOM 好。
    /// </remarks>
    public const int ExportMaxRangeDays = 366;

    /// <summary>
    /// 单次导出最大条数（F7.5）
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ExportMaxRangeDays"/> 是两道不同的闸：区间合规但数据密度极高时仍可能超量。
    /// </remarks>
    public const int ExportMaxRows = 50000;

    /// <summary>
    /// 权限范围：查询匹配（F6.2）
    /// </summary>
    public const string ScopeAllocate = "allocate";

    /// <summary>
    /// 权限范围：到账通知（F6.2）
    /// </summary>
    public const string ScopeNotify = "notify";

    /// <summary>
    /// 权限范围：未归类接口（fail-closed 兜底）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于「路径落在 <c>/api/pay*</c> 下、但既没有 <c>[PayScope]</c> 声明、
    /// 也匹配不上已知路径模式」的接口 —— 即**新加了开放接口却忘了声明权限范围**。
    /// </para>
    /// <para>
    /// 这种情况下解析器返回本值而不是 null。差别是决定性的：
    /// <list type="bullet">
    /// <item>返回 null = <b>不限制</b>（fail-open）→ 任何持有效密钥的接入方都能调，等于接口默认敞开；</item>
    /// <item>返回本值 = <b>要求一个默认无人持有的 scope</b>（fail-closed）→ 默认拒绝，
    /// 错误信息里直接写出 <c>需要 scope=unclassified</c>，开发者在联调第一分钟就能发现漏声明。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 真要让某个未归类接口可用，必须**显式**把它加进某把密钥的 Scopes 里 ——
    /// 也就是必须有人做一次有意识的授权决定，而不是靠「忘了配置」获得放行。
    /// </para>
    /// </remarks>
    public const string ScopeUnclassified = "unclassified";

    /// <summary>
    /// 到账通知结果：正常受理并累加（F4.6）
    /// </summary>
    public const string NotifyResultAccepted = "accepted";

    /// <summary>
    /// 到账通知结果：凭证号重复，未累加（F4.5）
    /// </summary>
    public const string NotifyResultDuplicated = "duplicated";

    /// <summary>
    /// 到账通知结果：异常（订单不存在/已过期/已完成），已入异常台账（F5.1）
    /// </summary>
    public const string NotifyResultAbnormal = "abnormal";

    // ───────────────────────── 审计相关（F7.3） ─────────────────────────

    /// <summary>
    /// 审计记录中「操作人」在开放接口场景下的显示名
    /// </summary>
    /// <remarks>
    /// 开放接口没有登录用户，<c>OperatorId</c> 记 0、<c>OperatorName</c> 记本值，
    /// 真实身份由 <c>ClientId</c> / <c>ClientKey</c> 两个字段表达。
    /// 这样后台审计列表上能一眼区分「某个人在后台改的」与「某接入方调接口改的」。
    /// </remarks>
    public const string OpenApiOperatorName = "开放接口";

    /// <summary>
    /// 审计目标类型：开放接口凭证（认证失败记录用）
    /// </summary>
    /// <remarks>
    /// 认证失败时 accessKey 可能根本不存在，拿不到凭证 Id，所以
    /// <c>TargetId</c> 记 0、<c>TargetNo</c> 记 accessKey 原文，
    /// 靠 <c>ClientKey</c> 关联 —— 详见 <see cref="PayAuditActionEnum.ApiAuthFailure"/>。
    /// </remarks>
    public const string AuditTargetTypeOpenAccess = "OpenAccess";

    /// <summary>
    /// 审计目标类型：收款订单
    /// </summary>
    public const string AuditTargetTypeOrder = "Order";
}
