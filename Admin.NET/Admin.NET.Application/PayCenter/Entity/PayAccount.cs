// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号/收款码表（F1）
/// </summary>
/// <remarks>
/// 额度语义（见设计文档 §4.1）：
/// 剩余可用额度 = TotalQuota − UsedQuota − LockedQuota，计算得出、不落库。
/// </remarks>
[SugarTable("pay_account", "收款账号表")]
[SysTable]
[IncreTable]
[SugarIndex("i_{table}_tsc", nameof(Type), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
public class PayAccount : EntityBase
{
    /// <summary>
    /// 收款类型标签（如 wxpay / alipay，取值由字典 pay_account_type 维护）
    /// </summary>
    [SugarColumn(ColumnDescription = "收款类型", Length = 32)]
    [Required, MaxLength(32)]
    public virtual string Type { get; set; }

    /// <summary>
    /// 账号信息（兼容旧数据；新建以 <see cref="AccountNo"/> 为准，可同步写入本列）
    /// </summary>
    [SugarColumn(ColumnDescription = "账号信息", Length = 512)]
    [Required, MaxLength(512)]
    public virtual string AccountInfo { get; set; }

    /// <summary>
    /// 卖家姓名（收银台展示）
    /// </summary>
    [SugarColumn(ColumnDescription = "卖家姓名", Length = PayConst.PayeeNameLength, IsNullable = true)]
    [MaxLength(PayConst.PayeeNameLength)]
    public virtual string PayeeName { get; set; }

    /// <summary>
    /// 收款账号号（卡号 / 支付宝号等）
    /// </summary>
    [SugarColumn(ColumnDescription = "收款账号", Length = PayConst.AccountNoLength, IsNullable = true)]
    [MaxLength(PayConst.AccountNoLength)]
    public virtual string AccountNo { get; set; }

    /// <summary>
    /// 开户银行（银行卡类型必填）
    /// </summary>
    [SugarColumn(ColumnDescription = "开户银行", Length = PayConst.BankNameLength, IsNullable = true)]
    [MaxLength(PayConst.BankNameLength)]
    public virtual string BankName { get; set; }

    /// <summary>
    /// 支行信息（银行卡类型必填）
    /// </summary>
    [SugarColumn(ColumnDescription = "支行信息", Length = PayConst.BankNameLength, IsNullable = true)]
    [MaxLength(PayConst.BankNameLength)]
    public virtual string BranchName { get; set; }

    /// <summary>
    /// 收款码图片的根相对路径，例如 <c>/upload/pay-qr/{id}.png</c>。无图时为空。
    /// </summary>
    [SugarColumn(ColumnDescription = "收款码图片", Length = 512, IsNullable = true)]
    [MaxLength(512)]
    public virtual string QrImageUrl { get; set; }

    /// <summary>
    /// 总额度
    /// </summary>
    [SugarColumn(ColumnDescription = "总额度", ColumnDataType = "decimal(18,2)")]
    public virtual decimal TotalQuota { get; set; }

    /// <summary>
    /// 已用额度（累计确认到账）
    /// </summary>
    [SugarColumn(ColumnDescription = "已用额度", ColumnDataType = "decimal(18,2)")]
    public virtual decimal UsedQuota { get; set; }

    /// <summary>
    /// 锁定中额度（待到账/部分到账订单的预占）
    /// </summary>
    [SugarColumn(ColumnDescription = "锁定中额度", ColumnDataType = "decimal(18,2)")]
    public virtual decimal LockedQuota { get; set; }

    /// <summary>
    /// 状态：启用 / 停用 / 已用完 / 占用中（占用中仅历史兼容，匹配不再依赖）
    /// </summary>
    [SugarColumn(ColumnDescription = "状态")]
    public virtual PayAccountStatusEnum Status { get; set; } = PayAccountStatusEnum.Enabled;

    /// <summary>
    /// 账号分层：普通（非过期单≤2）/ 大额（不限次数）
    /// </summary>
    [SugarColumn(ColumnDescription = "账号分层")]
    public virtual PayAccountTierEnum AccountTier { get; set; } = PayAccountTierEnum.Normal;

    /// <summary>
    /// 匹配最小金额；0 表示不限制。&gt;0 时请求金额须 ≥ 本值才可匹配，并优先于低门槛账号。
    /// </summary>
    [SugarColumn(ColumnDescription = "匹配最小金额", ColumnDataType = "decimal(18,2)", DefaultValue = "0")]
    public virtual decimal MinAmount { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnDescription = "备注（收银台 Notice，可含受限 HTML）", Length = PayConst.AccountRemarkLength, IsNullable = true)]
    [MaxLength(PayConst.AccountRemarkLength)]
    public virtual string Remark { get; set; }

    /// <summary>
    /// 最近一次收款成功（订单 Completed）时间；从未成功则为空
    /// </summary>
    [SugarColumn(ColumnDescription = "最近收款成功时间", IsNullable = true)]
    public virtual DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// 空过期次数：关联订单 Expired 且到账为 0 的累计次数（匹配时降级）
    /// </summary>
    [SugarColumn(ColumnDescription = "空过期次数")]
    public virtual int EmptyExpireCount { get; set; }
}
