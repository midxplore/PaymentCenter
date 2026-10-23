// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号分页查询输入（F1.5）
/// </summary>
public class PagePayAccountInput : BasePageInput
{
    /// <summary>
    /// 收款类型
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public PayAccountStatusEnum? Status { get; set; }
}

/// <summary>
/// 新增收款账号输入（F1.1）
/// </summary>
public class AddPayAccountInput
{
    /// <summary>
    /// 收款类型标签
    /// </summary>
    [Required(ErrorMessage = "收款类型不能为空")]
    [MaxLength(32, ErrorMessage = "收款类型长度不能超过32")]
    public string Type { get; set; }

    /// <summary>
    /// 卖家姓名
    /// </summary>
    [MaxLength(PayConst.PayeeNameLength, ErrorMessage = "卖家姓名过长")]
    public string PayeeName { get; set; }

    /// <summary>
    /// 收款账号号（卡号 / 支付宝号等）
    /// </summary>
    [MaxLength(PayConst.AccountNoLength, ErrorMessage = "收款账号过长")]
    public string AccountNo { get; set; }

    /// <summary>
    /// 开户银行（银行卡必填）
    /// </summary>
    [MaxLength(PayConst.BankNameLength, ErrorMessage = "开户银行过长")]
    public string BankName { get; set; }

    /// <summary>
    /// 支行（银行卡必填）
    /// </summary>
    [MaxLength(PayConst.BankNameLength, ErrorMessage = "支行过长")]
    public string BranchName { get; set; }

    /// <summary>
    /// 账号信息（兼容旧字段；未填 AccountNo 时回落本列）
    /// </summary>
    [MaxLength(512, ErrorMessage = "账号信息长度不能超过512")]
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片路径（由上传接口返回）
    /// </summary>
    [MaxLength(512, ErrorMessage = "收款码图片地址长度不能超过512")]
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 总额度
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "总额度必须大于0")]
    public decimal TotalQuota { get; set; }

    /// <summary>
    /// 账号分层（默认普通）
    /// </summary>
    public PayAccountTierEnum AccountTier { get; set; } = PayAccountTierEnum.Normal;

    /// <summary>
    /// 匹配最小金额（0=不限制）
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "最小金额不能为负")]
    public decimal MinAmount { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(PayConst.AccountRemarkLength, ErrorMessage = "备注过长")]
    public string Remark { get; set; }
}

/// <summary>
/// 编辑收款账号（结构化字段、收款码、备注、状态）
/// </summary>
public class UpdatePayAccountInput : BaseIdInput
{
    /// <summary>
    /// 卖家姓名
    /// </summary>
    [MaxLength(PayConst.PayeeNameLength, ErrorMessage = "卖家姓名过长")]
    public string PayeeName { get; set; }

    /// <summary>
    /// 收款账号号
    /// </summary>
    [MaxLength(PayConst.AccountNoLength, ErrorMessage = "收款账号过长")]
    public string AccountNo { get; set; }

    /// <summary>
    /// 开户银行
    /// </summary>
    [MaxLength(PayConst.BankNameLength, ErrorMessage = "开户银行过长")]
    public string BankName { get; set; }

    /// <summary>
    /// 支行
    /// </summary>
    [MaxLength(PayConst.BankNameLength, ErrorMessage = "支行过长")]
    public string BranchName { get; set; }

    /// <summary>
    /// 账号信息。null 不修改；与 AccountNo 同步时写入。
    /// </summary>
    [MaxLength(512, ErrorMessage = "账号信息长度不能超过512")]
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片路径。null 不修改，空字符串清除。
    /// </summary>
    [MaxLength(512, ErrorMessage = "收款码图片地址长度不能超过512")]
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 备注（收银台 Notice，可含受限 HTML）
    /// </summary>
    [MaxLength(PayConst.AccountRemarkLength, ErrorMessage = "备注过长")]
    public string Remark { get; set; }

    /// <summary>
    /// 状态（仅允许 启用 / 停用）
    /// </summary>
    public PayAccountStatusEnum Status { get; set; } = PayAccountStatusEnum.Enabled;

    /// <summary>
    /// 账号分层
    /// </summary>
    public PayAccountTierEnum? AccountTier { get; set; }

    /// <summary>
    /// 匹配最小金额（0=不限制；null 不修改）
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "最小金额不能为负")]
    public decimal? MinAmount { get; set; }
}

/// <summary>
/// 调整总额度输入（增加 / 减少）
/// </summary>
public class AddQuotaInput : BaseIdInput
{
    /// <summary>
    /// 调整金额（绝对值，须 &gt; 0）
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "调整金额必须大于0")]
    public decimal Quota { get; set; }

    /// <summary>
    /// true=增加总额度；false=减少总额度
    /// </summary>
    /// <remarks>
    /// 减少时：存在预占（LockedQuota&gt;0）或历史占用中状态则拒绝；
    /// 调减后总额度不得低于已用额度，也不得低于匹配最小金额。
    /// </remarks>
    public bool Increase { get; set; } = true;
}

/// <summary>
/// 启用/停用输入（F1.3）
/// </summary>
public class SetPayAccountStatusInput : BaseIdInput
{
    /// <summary>
    /// 状态（仅允许 启用 / 停用）
    /// </summary>
    public PayAccountStatusEnum Status { get; set; } = PayAccountStatusEnum.Enabled;
}

/// <summary>
/// 收款账号输出
/// </summary>
public class PayAccountOutput
{
    public long Id { get; set; }

    public string Type { get; set; }

    public string PayeeName { get; set; }

    public string AccountNo { get; set; }

    public string BankName { get; set; }

    public string BranchName { get; set; }

    /// <summary>
    /// 账号信息（兼容旧数据；前后台均不脱敏）
    /// </summary>
    public string AccountInfo { get; set; }

    public string QrImageUrl { get; set; }

    public decimal TotalQuota { get; set; }

    public decimal UsedQuota { get; set; }

    public decimal LockedQuota { get; set; }

    public decimal RemainingQuota { get; set; }

    public PayAccountStatusEnum Status { get; set; }

    public string StatusText { get; set; }

    public PayAccountTierEnum AccountTier { get; set; }

    public string AccountTierText { get; set; }

    /// <summary>
    /// 匹配最小金额（0=不限制）
    /// </summary>
    public decimal MinAmount { get; set; }

    public string Remark { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? UpdateTime { get; set; }

    public string CreateUserName { get; set; }
}
