// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Newtonsoft.Json;

namespace Admin.NET.Application;

/// <summary>
/// 订单状态查询输出（§7.3）
/// </summary>
public class OrderQueryOutput
{
    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 外部业务单号
    /// </summary>
    public string ExternalNo { get; set; }

    /// <summary>
    /// 订单状态名称
    /// </summary>
    [JsonConverter(typeof(EnumNameConverter))]
    public PayOrderStatusEnum Status { get; set; }

    /// <summary>
    /// 订单状态中文描述
    /// </summary>
    public string StatusText { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    [JsonConverter(typeof(AmountStringConverter))]
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 累计到账金额
    /// </summary>
    [JsonConverter(typeof(AmountStringConverter))]
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>
    /// 完成时间（未完成时为空）
    /// </summary>
    public DateTime? CompleteTime { get; set; }
}

/// <summary>
/// 后台订单分页查询输入（F7.2）
/// </summary>
/// <remarks>
/// 同时承担两个审计视角：
/// <list type="bullet">
/// <item><b>订单维度</b>：按订单号 / 外部单号 / 状态 / 时间区间查；</item>
/// <item><b>账号维度</b>（F7.2）：传 <see cref="AccountId"/> 即可看该账号名下的订单明细。</item>
/// </list>
/// </remarks>
public class PagePayOrderInput : BasePageInput
{
    /// <summary>
    /// 系统订单号（模糊匹配）
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 外部业务单号（模糊匹配）
    /// </summary>
    public string ExternalNo { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public PayOrderStatusEnum? Status { get; set; }

    /// <summary>
    /// 收款账号Id（F7.2 账号维度审计）
    /// </summary>
    public long? AccountId { get; set; }

    /// <summary>
    /// 调用方标识（SysOpenAccess.Id）
    /// </summary>
    public long? ClientId { get; set; }

    /// <summary>
    /// 创建时间起
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 创建时间止
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 后台订单列表输出（F7.2）
/// </summary>
public class PayOrderOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 外部业务单号
    /// </summary>
    public string ExternalNo { get; set; }

    /// <summary>
    /// 收款账号Id
    /// </summary>
    public long AccountId { get; set; }

    /// <summary>
    /// 收款账号内容
    /// </summary>
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片的根相对路径。无图时为空。
    /// </summary>
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 收款类型
    /// </summary>
    public string AccountType { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 累计到账金额
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 未达成金额（请求 − 已到账，负数表示超额到账）
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>
    /// 订单状态
    /// </summary>
    public PayOrderStatusEnum Status { get; set; }

    /// <summary>
    /// 订单状态中文描述
    /// </summary>
    public string StatusText { get; set; }

    /// <summary>
    /// 调用方标识
    /// </summary>
    public long? ClientId { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompleteTime { get; set; }

    /// <summary>
    /// 超额到账备注（F4.4）
    /// </summary>
    public string OverpayRemark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 后台订单详情输出（F7.1 全生命周期）
/// </summary>
/// <remarks>
/// 订单当前状态 + 完整变更历史（事件流水）+ 每次通知明细，
/// 三张表一次给全，满足「一笔订单到底发生了什么」的追溯需求。
/// </remarks>
public class PayOrderDetailOutput
{
    /// <summary>
    /// 订单主体
    /// </summary>
    public PayOrderOutput Order { get; set; }

    /// <summary>
    /// 事件流水（按发生时间升序）
    /// </summary>
    public List<PayOrderEventOutput> Events { get; set; } = [];

    /// <summary>
    /// 到账通知明细（按到账时间升序）
    /// </summary>
    public List<PayNotifyRecordOutput> NotifyRecords { get; set; } = [];
}

/// <summary>
/// 订单事件流水输出（F7.1 / F7.4）
/// </summary>
public class PayOrderEventOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public PayEventTypeEnum EventType { get; set; }

    /// <summary>
    /// 事件类型中文描述
    /// </summary>
    public string EventTypeText { get; set; }

    /// <summary>
    /// 变更前状态
    /// </summary>
    public PayOrderStatusEnum? FromStatus { get; set; }

    /// <summary>
    /// 变更前状态中文描述
    /// </summary>
    public string FromStatusText { get; set; }

    /// <summary>
    /// 变更后状态
    /// </summary>
    public PayOrderStatusEnum? ToStatus { get; set; }

    /// <summary>
    /// 变更后状态中文描述
    /// </summary>
    public string ToStatusText { get; set; }

    /// <summary>
    /// 本次事件涉及金额
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 事件发生后的累计到账金额
    /// </summary>
    public decimal ReceivedTotal { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    public string Remark { get; set; }

    /// <summary>
    /// 操作人Id（系统事件为空）
    /// </summary>
    public long? OperatorId { get; set; }

    /// <summary>
    /// 操作人姓名（系统事件为空）
    /// </summary>
    public string OperatorName { get; set; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 到账通知明细输出（F7.1）
/// </summary>
public class PayNotifyRecordOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 本次到账金额
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 到账时间（通知方传入）
    /// </summary>
    public DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号
    /// </summary>
    public string VoucherNo { get; set; }

    /// <summary>
    /// 通知方标识（SysOpenAccess.Id）
    /// </summary>
    public long ClientId { get; set; }

    /// <summary>
    /// 是否已实际累加到订单（异常到账入台账时为 false）
    /// </summary>
    public bool Applied { get; set; }

    /// <summary>
    /// 原始请求报文（可追溯）
    /// </summary>
    public string RawBody { get; set; }

    /// <summary>
    /// 接收时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}
