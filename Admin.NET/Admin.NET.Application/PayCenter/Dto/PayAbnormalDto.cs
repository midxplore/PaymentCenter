// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 异常到账台账分页查询输入（F5.3）
/// </summary>
public class PagePayAbnormalInput : BasePageInput
{
    /// <summary>
    /// 处理状态
    /// </summary>
    public PayHandleStatusEnum? HandleStatus { get; set; }

    /// <summary>
    /// 异常原因
    /// </summary>
    public PayAbnormalReasonEnum? Reason { get; set; }

    /// <summary>
    /// 通知方标识（SysOpenAccess.Id）
    /// </summary>
    public long? ClientId { get; set; }

    /// <summary>
    /// 凭证号（精确匹配）
    /// </summary>
    public string VoucherNo { get; set; }

    /// <summary>
    /// 上报订单号（模糊匹配）
    /// </summary>
    public string ReportOrderNo { get; set; }

    /// <summary>
    /// 到账时间起
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 到账时间止
    /// </summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 人工关联异常到账输入（F5.2）
/// </summary>
public class LinkAbnormalInput : BaseIdInput
{
    /// <summary>
    /// 目标订单号（人工判定该笔到账应归属的订单）
    /// </summary>
    /// <remarks>
    /// ⚠️ 上限跟 <see cref="PayConst.OrderNoLength"/>（列宽）对齐，别写死。
    /// 订单号长度随年份增长（2026 年 32 位，约 2027-11 起 33 位），写死 32 会在进位后
    /// 让「人工关联」直接参数校验失败。
    /// </remarks>
    [Required(ErrorMessage = "目标订单号不能为空")]
    [MaxLength(PayConst.OrderNoLength, ErrorMessage = "目标订单号长度超出上限")]
    public string TargetOrderNo { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    [MaxLength(256, ErrorMessage = "处理备注长度不能超过256")]
    public string HandleRemark { get; set; }
}

/// <summary>
/// 确认无需处理输入（F5.3）
/// </summary>
public class IgnoreAbnormalInput : BaseIdInput
{
    /// <summary>
    /// 处理备注（建议写明判定依据，便于事后复核）
    /// </summary>
    [MaxLength(256, ErrorMessage = "处理备注长度不能超过256")]
    public string HandleRemark { get; set; }
}

/// <summary>
/// 异常到账台账输出（F5.3）
/// </summary>
public class PayAbnormalOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 到账金额
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 到账时间
    /// </summary>
    public DateTime NotifyTime { get; set; }

    /// <summary>
    /// 凭证号
    /// </summary>
    public string VoucherNo { get; set; }

    /// <summary>
    /// 通知方标识
    /// </summary>
    public long ClientId { get; set; }

    /// <summary>
    /// 通知方上报的订单号
    /// </summary>
    public string ReportOrderNo { get; set; }

    /// <summary>
    /// 异常原因
    /// </summary>
    public PayAbnormalReasonEnum Reason { get; set; }

    /// <summary>
    /// 异常原因名称
    /// </summary>
    public string ReasonText { get; set; }

    /// <summary>
    /// 处理状态
    /// </summary>
    public PayHandleStatusEnum HandleStatus { get; set; }

    /// <summary>
    /// 处理状态名称
    /// </summary>
    public string HandleStatusText { get; set; }

    /// <summary>
    /// 人工关联后的订单Id
    /// </summary>
    public long? RelatedOrderId { get; set; }

    /// <summary>
    /// 人工关联后的订单号
    /// </summary>
    public string RelatedOrderNo { get; set; }

    /// <summary>
    /// 处理人Id
    /// </summary>
    public long? HandlerId { get; set; }

    /// <summary>
    /// 处理人姓名
    /// </summary>
    public string HandlerName { get; set; }

    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTime? HandleTime { get; set; }

    /// <summary>
    /// 处理备注
    /// </summary>
    public string HandleRemark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 人工关联结果输出（F5.2）
/// </summary>
public class LinkAbnormalOutput
{
    /// <summary>
    /// 关联到的订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 关联后订单状态
    /// </summary>
    public PayOrderStatusEnum OrderStatus { get; set; }

    /// <summary>
    /// 关联后订单状态名称
    /// </summary>
    public string OrderStatusText { get; set; }

    /// <summary>
    /// 关联后累计到账金额
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 本次关联金额
    /// </summary>
    public decimal Amount { get; set; }
}
