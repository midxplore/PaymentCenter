// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 业务操作审计动作枚举（F7.3，审计流水只增不改）
/// </summary>
[Description("业务操作审计动作枚举")]
public enum PayAuditActionEnum
{
    /// <summary>
    /// 新增收款账号
    /// </summary>
    [Description("新增收款账号")]
    AccountAdd = 1,

    /// <summary>
    /// 编辑收款账号
    /// </summary>
    [Description("编辑收款账号")]
    AccountUpdate = 2,

    /// <summary>
    /// 追加额度（F1.2）
    /// </summary>
    [Description("追加额度")]
    QuotaAdd = 3,

    /// <summary>
    /// 变更账号状态（启用 / 停用，F1.4）
    /// </summary>
    [Description("变更账号状态")]
    StatusChange = 4,

    /// <summary>
    /// 删除收款账号
    /// </summary>
    [Description("删除收款账号")]
    AccountDelete = 5,

    /// <summary>
    /// 异常到账人工处理（关联订单 / 确认无需处理，F5.2 / F5.3）
    /// </summary>
    [Description("异常到账处理")]
    AbnormalHandle = 6,

    /// <summary>
    /// 数据导出（F7.5）
    /// </summary>
    /// <remarks>
    /// 导出本身也是敏感动作（批量带走账号/金额数据），必须留痕：谁、什么时候、导了哪个区间、多少条。
    /// </remarks>
    [Description("数据导出")]
    Export = 7,

    /// <summary>
    /// 开放接口调用（资金相关动作，F6/F7.3）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 由**接入方**通过签名鉴权发起的、会改变资金状态的调用：查询匹配（<c>/api/pay/allocate</c>）、
    /// 到账通知（<c>/api/pay/notify</c>）。
    /// </para>
    /// <para>
    /// <b>为什么不复用订单事件流水（<c>pay_order_event</c>）</b>：那张表回答「订单经历了什么」，
    /// 本动作回答「**是谁**让它经历的」。同一笔订单可能先由 allocate 创建、再由 notify 累加，
    /// 两个调用方（甚至两个不同的 accessKey）各算一笔审计，订单流水里看不出这个区分。
    /// </para>
    /// <para>
    /// <b>为什么不只靠框架的 <c>SysLogOp</c></b>：<c>SysLogOp</c> 会被
    /// <c>CleanSysLogJob</c> 按保留期清理，而资金类调用的审计要求「永不清删」。
    /// </para>
    /// <para>
    /// 只读调用（如订单状态查询）**不写本动作**：它们不改变资金状态，
    /// 由框架 <c>SysLogOp</c> 的请求级日志覆盖即可，避免审计表被轮询流量灌满。
    /// </para>
    /// </remarks>
    [Description("开放接口调用")]
    ApiCall = 8,

    /// <summary>
    /// 开放接口认证失败
    /// </summary>
    /// <remarks>
    /// <para>
    /// 签名校验失败、accessKey 不存在、accessKey 已停用、scope 越权等**全部**未通过鉴权的尝试。
    /// </para>
    /// <para>
    /// 这类事件发生在 MVC 之前，框架的 <c>LoggingMonitor</c> / <c>SysLogOp</c> **采不到**
    /// （见设计文档 §8.4 实测），所以必须由签名鉴权链路上的审计钩子单独落库。
    /// </para>
    /// <para>
    /// 用途：密钥爆破探测、接入方上线前自查签名串、以及「某接入方突然大量 401」的定位。
    /// 因此 <c>ClientKey</c> 存的是**请求头里的原始值**，即使该 key 在库里不存在。
    /// </para>
    /// </remarks>
    [Description("开放接口认证失败")]
    ApiAuthFailure = 9
}
