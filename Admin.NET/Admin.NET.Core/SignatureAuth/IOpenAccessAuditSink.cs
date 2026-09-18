// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 开放接口认证审计接收器（框架留出的扩展点）
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要这个钩子</b>：签名校验失败时，请求根本进不到控制器，
/// 所以 <c>LoggingMonitor</c> / <c>SysLogOp</c> 这类「入站请求日志」是**采不到**的
/// （详见本项目技术设计方案 §8.4）。骨架目前只在 <c>OnChallenge</c> 里打一条
/// <c>ILogger.LogWarning</c> —— 那只是**文本日志**，无法被后台检索、无法关联到具体接入方、
/// 也会随日志轮转消失。
/// </para>
/// <para>
/// 对于资金类开放接口，「谁在什么时候用哪个 accessKey 试图调哪个接口、被拒的原因是什么」
/// 属于必须可检索、可长期留存的审计事实。本接口让业务模块把这条事实落到**自己的审计表**里
/// （本项目为 <c>pay_audit_log</c>），而不是依赖日志文本。
/// </para>
/// <para>
/// <b>分层理由</b>：<c>Admin.NET.Core</c> 不能反向依赖业务程序集，所以由 Core 定义钩子、
/// 由业务实现落库（与 <see cref="IOpenAccessScopeResolver"/> 同一思路：
/// 框架留钩子 + 业务填增量）。
/// </para>
/// <para>
/// <b>实现约束</b>：
/// <list type="number">
/// <item>实现类**必须自行吞掉所有异常**。该钩子在认证失败路径上被调用，
/// 一旦抛出会把「本该返回 401 的请求」变成 500，等于用审计的可用性去换认证的可用性 —— 方向反了。
/// 正确做法是 catch 后记一条 Error 日志，让 401 照常返回。</item>
/// <item>应尽量快。它在请求线程上同步执行，落库失败/超时不应拖慢 401 响应。</item>
/// <item>不要在这里改 <c>HttpContext</c> 的认证结果 —— 本钩子只负责「记录」，不负责「裁决」。</item>
/// </list>
/// </para>
/// </remarks>
public interface IOpenAccessAuditSink
{
    /// <summary>
    /// 开放接口认证失败时回调
    /// </summary>
    /// <param name="context">失败上下文（含 accessKey、失败原因、请求方法与路径、来源 IP）</param>
    Task OnAuthenticationFailedAsync(OpenAccessAuthFailureContext context);
}

/// <summary>
/// 开放接口认证失败上下文
/// </summary>
public class OpenAccessAuthFailureContext
{
    /// <summary>
    /// 当前请求上下文
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// 请求头里携带的 accessKey（可能为空串或不存在）
    /// </summary>
    /// <remarks>
    /// 刻意保留**原始值**而不是查库后的身份：失败场景下 accessKey 很可能压根不存在
    /// （密钥被爆破、接入方配错环境），此时「对方到底用了哪个 key」才是最有价值的线索。
    /// </remarks>
    public string AccessKey { get; init; }

    /// <summary>
    /// 失败原因（框架给出的可读消息，如「sign 无效的签名」「accessKey 无权访问该接口…」）
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string Method { get; init; }

    /// <summary>
    /// 请求路径（不含查询串）
    /// </summary>
    public string Path { get; init; }

    /// <summary>
    /// 来源 IP
    /// </summary>
    public string RemoteIp { get; init; }
}
