// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 到账通知输入（F4，§7.2）
/// </summary>
public class NotifyInput
{
    /// <summary>
    /// 系统订单号
    /// </summary>
    /// <remarks>
    /// ⚠️ 上限必须跟 <see cref="PayConst.OrderNoLength"/>（= 列宽）一致，不要写死一个「看起来够用」的数。
    /// 订单号是 <c>{yyyyMMddHHmmssfff}{雪花尾段}</c>，**长度会随年份增长**：
    /// 2026 年 32 位，约 2027-11 起 33 位。历史上这里写过 32，正好卡在当年长度上，
    /// 一旦进位就会把**所有**到账通知挡在参数校验外（而不是报个业务错）。
    /// </remarks>
    [Required(ErrorMessage = "订单号不能为空")]
    [MaxLength(PayConst.OrderNoLength, ErrorMessage = "订单号长度超出上限")]
    public string OrderNo { get; set; }

    /// <summary>
    /// 本次到账金额（必须大于 0）
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "到账金额必须大于0")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 到账时间（通知方传入；为空时取服务端当前时间）
    /// </summary>
    public DateTime? NotifyTime { get; set; }

    /// <summary>
    /// 凭证号（去重键，F4.5）
    /// </summary>
    /// <remarks>同一通知方的同一凭证号只会被累加一次，重复上报将被忽略。</remarks>
    [Required(ErrorMessage = "凭证号不能为空")]
    [MaxLength(64, ErrorMessage = "凭证号长度不能超过64")]
    public string VoucherNo { get; set; }
}

/// <summary>
/// 到账通知输出（F4.6，§7.2）
/// </summary>
/// <remarks>
/// <c>duplicated</c> 与 <c>abnormal</c> 都属于「业务上已受理」，
/// 统一返回业务成功码，避免通知方无脑重试。
/// </remarks>
public class NotifyOutput
{
    /// <summary>
    /// 处理结果：accepted / duplicated / abnormal
    /// </summary>
    public string Result { get; set; }

    /// <summary>
    /// 处理结果中文说明
    /// </summary>
    public string ResultText { get; set; }

    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 当前订单状态中文描述
    /// </summary>
    public string OrderStatus { get; set; }

    /// <summary>
    /// 累计到账金额
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    public decimal RequestAmount { get; set; }
}

/// <summary>
/// 到账累加结果（内部使用）
/// </summary>
/// <remarks>
/// 由 <see cref="PayNotifyService.ApplyReceiptCoreAsync"/> 返回，
/// 同时供到账通知（§5.2）与人工关联（§5.4）复用。
/// </remarks>
public class ReceiptApplyResult
{
    /// <summary>
    /// 订单Id
    /// </summary>
    public long OrderId { get; set; }

    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 累加后的订单状态
    /// </summary>
    public PayOrderStatusEnum Status { get; set; }

    /// <summary>
    /// 累加后的累计到账金额
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    /// <summary>
    /// 请求金额
    /// </summary>
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 超额到账金额（未超额为 0，F4.4）
    /// </summary>
    public decimal Overpay { get; set; }
}
