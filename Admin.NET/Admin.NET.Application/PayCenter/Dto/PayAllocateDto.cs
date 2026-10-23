// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Newtonsoft.Json;

namespace Admin.NET.Application;

/// <summary>
/// 查询匹配输入（F2）
/// </summary>
public class AllocateInput
{
    /// <summary>
    /// 收款类型（标准类型码，见对外文档）
    /// </summary>
    [Required(ErrorMessage = "收款类型不能为空")]
    [MaxLength(32, ErrorMessage = "收款类型长度不能超过32")]
    public string Type { get; set; }

    /// <summary>
    /// 收款金额（必须大于 0；超精度拒绝）。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 商家订单号（必填，作为幂等键的一部分）
    /// </summary>
    [Required(ErrorMessage = "商家订单号不能为空")]
    [MaxLength(64, ErrorMessage = "商家订单号长度不能超过64")]
    public string ExternalNo { get; set; }

    /// <summary>
    /// 收银台「返回」跳转地址（可选；须为 http/https 绝对地址；空则付款人点返回时关页/通知父窗）
    /// </summary>
    [MaxLength(PayConst.ReturnUrlLength, ErrorMessage = "返回地址长度不能超过512")]
    public string ReturnUrl { get; set; }

    /// <summary>
    /// 收银台「返回」按钮文案（可选；不传默认「返回」；最多 16 字）
    /// </summary>
    [MaxLength(PayConst.ReturnLabelLength, ErrorMessage = "返回按钮文案长度不能超过16")]
    public string ReturnLabel { get; set; }
}

/// <summary>
/// 查询匹配输出：仅订单号 + 收银台链接
/// </summary>
public class AllocateOutput
{
    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 收银台页面 URL（付款人打开此链接完成付款与凭证上传）
    /// </summary>
    public string CashierUrl { get; set; }
}

/// <summary>
/// 收银台展示输出（匿名 token 门禁）
/// </summary>
public class CashierOrderOutput
{
    public string OrderNo { get; set; }

    public string Type { get; set; }

    /// <summary>进度：1 确认付款 / 2 等待确认 / 3 确认收款</summary>
    public int Step { get; set; }

    [JsonConverter(typeof(EnumNameConverter))]
    public PayOrderStatusEnum Status { get; set; }

    public string StatusText { get; set; }

    [JsonConverter(typeof(AmountStringConverter))]
    public decimal RequestAmount { get; set; }

    public DateTime ExpireTime { get; set; }

    /// <summary>剩余秒数，已过期为 0</summary>
    public int RemainSeconds { get; set; }

    /// <summary>可发起「我已付款」主流程（待到账 / 未到账）</summary>
    public bool CanPay { get; set; }

    /// <summary>可取消订单（同 CanPay）</summary>
    public bool CanCancel { get; set; }

    /// <summary>可上传/重传凭证（待到账 / 未到账 / 等待确认）</summary>
    public bool CanUpload { get; set; }

    public string PayeeName { get; set; }

    public string AccountNo { get; set; }

    public string BankName { get; set; }

    public string BranchName { get; set; }

    public string QrImageUrl { get; set; }

    public List<string> VoucherUrls { get; set; } = [];

    /// <summary>付款提示（来自收款账号 Remark；空则前端不展示）</summary>
    public string Notice { get; set; }

    /// <summary>返回跳转地址（空则前端关页/通知父窗）</summary>
    public string ReturnUrl { get; set; }

    /// <summary>返回按钮文案（空则前端用「返回」）</summary>
    public string ReturnLabel { get; set; }
}

/// <summary>
/// 收银台确认提交。传 VoucherUrls 时在提交瞬间替换已落库凭证（重传防误操作）。
/// </summary>
public class CashierSubmitInput
{
    /// <summary>可选；1～3 条本单草稿上传 URL，提交时整体替换</summary>
    public List<string> VoucherUrls { get; set; }
}

/// <summary>
/// 收银台提交结果（凭证提交即到账；过期重传失败则进异常台账）
/// </summary>
public class CashierSubmitOutput
{
    /// <summary>Completed / Expired（异常待人工）</summary>
    public string Status { get; set; }

    public string StatusText { get; set; }

    /// <summary>是否已自动到账并通知商户</summary>
    public bool Paid { get; set; }

    public string Message { get; set; }
}
