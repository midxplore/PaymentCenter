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
    /// 账号信息（卡号 / 账号 / 收款码文本）。与 <see cref="QrImageUrl"/> 至少填一个。
    /// </summary>
    [MaxLength(512, ErrorMessage = "账号信息长度不能超过512")]
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片路径（由上传接口返回）。与 <see cref="AccountInfo"/> 至少填一个。
    /// </summary>
    [MaxLength(512, ErrorMessage = "收款码图片地址长度不能超过512")]
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 总额度
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "总额度必须大于0")]
    public decimal TotalQuota { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(256, ErrorMessage = "备注长度不能超过256")]
    public string Remark { get; set; }
}

/// <summary>
/// 编辑收款账号（账号文本、收款码图片、备注、状态）
/// </summary>
public class UpdatePayAccountInput : BaseIdInput
{
    /// <summary>
    /// 账号信息。null 不修改，空字符串清除；与 <see cref="QrImageUrl"/> 至少填一个。
    /// </summary>
    [MaxLength(512, ErrorMessage = "账号信息长度不能超过512")]
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片路径。null 不修改，空字符串清除。
    /// </summary>
    [MaxLength(512, ErrorMessage = "收款码图片地址长度不能超过512")]
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [MaxLength(256, ErrorMessage = "备注长度不能超过256")]
    public string Remark { get; set; }

    /// <summary>
    /// 状态（仅允许 启用 / 停用）
    /// </summary>
    public PayAccountStatusEnum Status { get; set; } = PayAccountStatusEnum.Enabled;
}

/// <summary>
/// 追加额度输入（F1.2）
/// </summary>
public class AddQuotaInput : BaseIdInput
{
    /// <summary>
    /// 本次追加的额度
    /// </summary>
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "追加额度必须大于0")]
    public decimal Quota { get; set; }
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
    /// <summary>
    /// 主键Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 收款类型标签
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 账号信息（前后台均不脱敏，见设计决策）
    /// </summary>
    public string AccountInfo { get; set; }

    /// <summary>
    /// 收款码图片的根相对路径。无图时为空字符串。
    /// </summary>
    public string QrImageUrl { get; set; }

    /// <summary>
    /// 总额度
    /// </summary>
    public decimal TotalQuota { get; set; }

    /// <summary>
    /// 已用额度
    /// </summary>
    public decimal UsedQuota { get; set; }

    /// <summary>
    /// 锁定中额度
    /// </summary>
    public decimal LockedQuota { get; set; }

    /// <summary>
    /// 剩余可用额度（计算得出）
    /// </summary>
    public decimal RemainingQuota { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public PayAccountStatusEnum Status { get; set; }

    /// <summary>
    /// 状态名称
    /// </summary>
    public string StatusText { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 创建者姓名
    /// </summary>
    public string CreateUserName { get; set; }
}
