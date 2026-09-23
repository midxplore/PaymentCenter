// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 商户 Webhook 派发 outbox（只增改状态，不删）
/// </summary>
[SugarTable("pay_webhook_outbox", "Webhook派发表")]
[SysTable]
[IncreTable]
[SugarIndex("u_{table}_eid", nameof(EventId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("i_{table}_sn", nameof(Status), OrderByType.Asc, nameof(NextRetryTime), OrderByType.Asc)]
public class PayWebhookOutbox : EntityBase
{
    /// <summary>
    /// 幂等事件 Id（如 order:{orderId}:result:Completed）
    /// </summary>
    [SugarColumn(ColumnDescription = "事件Id", Length = 128)]
    [Required, MaxLength(128)]
    public virtual string EventId { get; set; }

    /// <summary>
    /// 事件类型（本期固定 OrderResult）
    /// </summary>
    [SugarColumn(ColumnDescription = "事件类型", Length = 64)]
    [Required, MaxLength(64)]
    public virtual string EventType { get; set; }

    /// <summary>
    /// 关联订单 Id
    /// </summary>
    [SugarColumn(ColumnDescription = "订单Id")]
    public virtual long OrderId { get; set; }

    /// <summary>
    /// 系统订单号
    /// </summary>
    [SugarColumn(ColumnDescription = "订单号", Length = PayConst.OrderNoLength)]
    [MaxLength(PayConst.OrderNoLength)]
    public virtual string OrderNo { get; set; }

    /// <summary>
    /// 调用方（SysOpenAccess.Id）
    /// </summary>
    [SugarColumn(ColumnDescription = "调用方Id")]
    public virtual long ClientId { get; set; }

    /// <summary>
    /// 派发目标 URL（入队时快照，避免事后改凭证影响在途消息）
    /// </summary>
    [SugarColumn(ColumnDescription = "Webhook地址", Length = PayConst.WebhookUrlLength)]
    [MaxLength(PayConst.WebhookUrlLength)]
    public virtual string WebhookUrl { get; set; }

    /// <summary>
    /// JSON 报文正文
    /// </summary>
    [SugarColumn(ColumnDescription = "报文", ColumnDataType = "text")]
    public virtual string Payload { get; set; }

    /// <summary>
    /// 派发状态
    /// </summary>
    [SugarColumn(ColumnDescription = "状态")]
    public virtual PayWebhookStatusEnum Status { get; set; } = PayWebhookStatusEnum.Pending;

    /// <summary>
    /// 已尝试次数
    /// </summary>
    [SugarColumn(ColumnDescription = "尝试次数")]
    public virtual int AttemptCount { get; set; }

    /// <summary>
    /// 下次重试时间（Pending 时有效）
    /// </summary>
    [SugarColumn(ColumnDescription = "下次重试时间", IsNullable = true)]
    public virtual DateTime? NextRetryTime { get; set; }

    /// <summary>
    /// 最近一次错误摘要
    /// </summary>
    [SugarColumn(ColumnDescription = "最后错误", Length = 512, IsNullable = true)]
    [MaxLength(512)]
    public virtual string LastError { get; set; }
}
