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
    public const int DefaultOrderExpireMinutes = 120;

    /// <summary>
    /// 普通账号允许的非过期订单数上限（含完成单；过期单不占名额）
    /// </summary>
    public const int NormalAccountMaxNonExpiredOrders = 2;

    /// <summary>
    /// 到账提示音配置（空=不播放）；系统参数 Code，由订单页上传音频写入根相对路径
    /// </summary>
    public const string OrderAlertSoundUrl = "pay_order_alert_sound_url";

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
    /// 权限范围：到账通知（历史保留；对外开放已下线，勿再给新凭证授权）
    /// </summary>
    public const string ScopeNotify = "notify";

    /// <summary>
    /// Webhook 地址列宽（与 SysOpenAccess.WebhookUrl / schema 联动）
    /// </summary>
    public const int WebhookUrlLength = 512;

    /// <summary>
    /// 水单路径列宽（与 PayOrder.VoucherUrl / schema 联动）
    /// </summary>
    public const int VoucherUrlLength = 512;

    /// <summary>
    /// 收银台 token 列宽（URL-safe 高熵串）
    /// </summary>
    public const int CashierTokenLength = 43;

    /// <summary>
    /// 多凭证 JSON 列宽（最多 3 条路径）
    /// </summary>
    public const int VoucherUrlsLength = 2048;

    /// <summary>
    /// 收银台返回地址（第三方 returnUrl）列宽
    /// </summary>
    public const int ReturnUrlLength = 512;

    /// <summary>
    /// 收银台「返回」按钮文案列宽
    /// </summary>
    public const int ReturnLabelLength = 16;

    /// <summary>
    /// 收银台返回按钮默认文案
    /// </summary>
    public const string DefaultReturnLabel = "返回";

    /// <summary>
    /// 卖家姓名列宽
    /// </summary>
    public const int PayeeNameLength = 64;

    /// <summary>
    /// 收款账号号列宽
    /// </summary>
    public const int AccountNoLength = 128;

    /// <summary>
    /// 开户银行 / 支行列宽
    /// </summary>
    public const int BankNameLength = 128;

    /// <summary>
    /// 收款账号备注（收银台 Notice，可含受限 HTML）列宽
    /// </summary>
    public const int AccountRemarkLength = 2000;

    /// <summary>
    /// 收银台单笔凭证最大字节数（3MB）
    /// </summary>
    public const int CashierVoucherMaxBytes = 3 * 1024 * 1024;

    /// <summary>
    /// 收银台单笔订单最多凭证张数
    /// </summary>
    public const int CashierVoucherMaxCount = 3;

    /// <summary>
    /// 收款码上传最大字节数（5MB；前端会先压，后端再限边转 JPEG）
    /// </summary>
    public const int QrImageMaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// 收款码落库最长边（像素）；手机扫屏显足够，避免超大图拖慢收银台
    /// </summary>
    public const int QrImageMaxSide = 1200;

    /// <summary>
    /// 标准收款类型：银行卡
    /// </summary>
    public const string TypeBank = "bank";

    /// <summary>
    /// Webhook outbox 派发作业标识
    /// </summary>
    public const string WebhookJobId = "pay_webhook_dispatch_job";

    /// <summary>
    /// Webhook outbox 派发触发器标识
    /// </summary>
    public const string WebhookJobTriggerId = "pay_webhook_dispatch_trigger";

    /// <summary>
    /// 运营汇总日终快照作业标识
    /// </summary>
    public const string OpsDailySnapshotJobId = "pay_ops_daily_snapshot_job";

    /// <summary>
    /// 运营汇总日终快照触发器标识（每日午夜）
    /// </summary>
    public const string OpsDailySnapshotTriggerId = "pay_ops_daily_snapshot_trigger";

    /// <summary>
    /// Webhook 单次 HTTP 超时（秒）
    /// </summary>
    public const int WebhookHttpTimeoutSeconds = 10;

    /// <summary>
    /// Webhook 派发单批次条数
    /// </summary>
    public const int WebhookDispatchBatchSize = 50;

    /// <summary>
    /// 权限范围：未归类接口（fail-closed 兜底）
    /// </summary>
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
