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
    // 金额字段用 decimal（服务端从 JSON 原文精确解析，无浮点损失）；
    // 对外**写出**一律是十进制字符串，见 AllocateOutput.RequestAmount。
    /// <summary>
    /// 收款类型（字典 pay_account_type 的 Value）
    /// </summary>
    [Required(ErrorMessage = "收款类型不能为空")]
    [MaxLength(32, ErrorMessage = "收款类型长度不能超过32")]
    public string Type { get; set; }

    /// <summary>
    /// 收款金额（必须大于 0）
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "收款金额必须大于0")]
    public decimal Amount { get; set; }

    /// <summary>
    /// 外部业务单号（可选，作为幂等键）
    /// </summary>
    /// <remarks>
    /// 传入后：同一 ExternalNo 重复请求将直接返回首次生成的订单，不再重新匹配（F2.6）。
    /// </remarks>
    [MaxLength(64, ErrorMessage = "外部业务单号长度不能超过64")]
    public string ExternalNo { get; set; }
}

/// <summary>
/// 查询匹配输出（F2）
/// </summary>
public class AllocateOutput
{
    /// <summary>
    /// 系统订单号
    /// </summary>
    public string OrderNo { get; set; }

    /// <summary>
    /// 收款类型
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 收款账号 / 收款码文本。只上传了图片时为空字符串。
    /// </summary>
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片的根相对路径。无图时为空字符串。接入方用站点根地址拼接后展示。
    /// </summary>
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 请求金额，写出为两位小数字符串
    /// </summary>
    [JsonConverter(typeof(AmountStringConverter))]
    public decimal RequestAmount { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>
    /// 是否命中幂等键（true 表示该 ExternalNo 已有订单，本次未重新匹配）
    /// </summary>
    public bool IdempotentHit { get; set; }
}
