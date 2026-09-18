// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.Extensions.Logging;

namespace Admin.NET.Application;

/// <summary>
/// 开放接口认证失败 → 审计落库（F7.3）
/// </summary>
/// <remarks>
/// <para>
/// 实现骨架留出的 <see cref="IOpenAccessAuditSink"/> 钩子：签名鉴权失败时，
/// 由本类把这条事实写进模块自己的审计表 <c>pay_audit_log</c>。
/// </para>
/// <para>
/// <b>为什么不只靠框架的 Warning 日志</b>（见设计文档 §8.4 实测）：
/// 401 发生在 MVC 之前，<c>LoggingMonitor</c> / <c>SysLogOp</c> 完全采不到；
/// 而文本日志会轮转、无法按接入方检索、也无法和订单/审计表关联。
/// 对资金类接口而言，「哪个 accessKey 在什么时候试图调哪个接口、为什么被拒」
/// 属于必须可检索、可长期留存的审计事实。
/// </para>
/// <para>
/// <b>写入策略（重要）</b>：只记录**带 accessKey** 的失败。
/// 理由：没有任何 accessKey 的请求（裸探测、健康检查打错地址）无法归属到任何主体，
/// 写进审计表既没有检索价值，又给了攻击者一个「零成本灌表」的放大面 ——
/// 每个匿名请求都产生一行永久留存的数据。这类失败由 Warning 文本日志 + 限流覆盖。
/// 反之，**带 accessKey 的失败一律记录**：那正是密钥爆破、接入方配错环境、
/// 以及「某接入方突然大量 401」这些真正需要被发现的场景。
/// </para>
/// <para>
/// <b>异常处理</b>：本类自行吞掉所有异常。审计写不进去要能被发现（记 Error 日志），
/// 但**绝不能**因此把本该返回的 401 变成 500 —— 那等于让所有接入方陪着一个
/// 写不进去的审计表一起故障，方向反了。
/// </para>
/// </remarks>
public class PayOpenAccessAuditSink : IOpenAccessAuditSink, ITransient
{
    /// <summary>
    /// <c>pay_audit_log.targetno</c> 的列宽
    /// </summary>
    /// <remarks>
    /// 与 <c>PayAuditLog.TargetNo</c> 的 <c>[MaxLength(64)]</c> 一致。
    /// accessKey 列宽是 128，比本列宽，所以必须截断，否则写库报错、
    /// 而错误又会被下面的 catch 吞掉 —— 表现为「认证失败静默没有审计」，最难查。
    /// </remarks>
    private const int TargetNoLength = 64;

    /// <summary>
    /// <c>pay_audit_log.remark</c> 的列宽
    /// </summary>
    private const int RemarkLength = 512;

    /// <summary>
    /// <c>pay_audit_log.clientkey</c> 的列宽
    /// </summary>
    private const int ClientKeyLength = 128;

    private readonly SqlSugarRepository<PayAuditLog> _payAuditLogRep;
    private readonly ILogger<PayOpenAccessAuditSink> _logger;

    public PayOpenAccessAuditSink(SqlSugarRepository<PayAuditLog> payAuditLogRep,
        ILogger<PayOpenAccessAuditSink> logger)
    {
        _payAuditLogRep = payAuditLogRep;
        _logger = logger;
    }

    /// <summary>
    /// 认证失败回调：落一条审计
    /// </summary>
    /// <param name="context">失败上下文</param>
    /// <returns></returns>
    public async Task OnAuthenticationFailedAsync(OpenAccessAuthFailureContext context)
    {
        try
        {
            var accessKey = context.AccessKey?.Trim();

            // 无 accessKey 的裸探测不入库（见类注释的「写入策略」）
            if (string.IsNullOrWhiteSpace(accessKey)) return;

            await _payAuditLogRep.InsertAsync(new PayAuditLog
            {
                Action = PayAuditActionEnum.ApiAuthFailure,
                TargetType = PayConst.AuditTargetTypeOpenAccess,
                // 认证失败时该 accessKey 很可能压根不在 sysopenaccess 里，拿不到凭证 Id。
                // 所以 TargetId 记 0、靠 ClientKey 关联 —— 这也是 ClientKey 存原文而非外键的原因。
                TargetId = 0,
                TargetNo = Truncate(accessKey, TargetNoLength),
                Remark = Truncate($"签名鉴权失败：{context.Reason}｜{context.Method} {context.Path}", RemarkLength),
                ClientId = null,
                ClientKey = Truncate(accessKey, ClientKeyLength),
                // 没有登录用户：操作人记 0（系统），真实身份在 ClientKey
                OperatorId = 0,
                OperatorName = PayConst.OpenApiOperatorName,
                OperatorIp = Truncate(context.RemoteIp, 64)
            });
        }
        catch (Exception ex)
        {
            // 只记日志，不向外抛：审计失败不能影响 401 的正常返回
            _logger.LogError(ex,
                "开放接口认证失败审计写入异常：accessKey={AccessKey}｜{Method} {Path}",
                context.AccessKey, context.Method, context.Path);
        }
    }

    /// <summary>
    /// 按列宽截断（避免「值超长 → 写库报错 → 被 catch 吞掉 → 审计静默丢失」）
    /// </summary>
    /// <param name="value">原值</param>
    /// <param name="maxLength">列宽</param>
    /// <returns></returns>
    private static string Truncate(string value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
