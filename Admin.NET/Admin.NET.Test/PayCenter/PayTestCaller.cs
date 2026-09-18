// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;
using Microsoft.AspNetCore.Http;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 为「直连调用服务」的单元测试**显式扮演一个调用方**。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么必须有这个助手</b>：<see cref="PayAllocateService"/> / <see cref="PayNotifyService"/>
/// 都通过 <c>PayCallerContext.RequireClientId()</c> 解析调用方，而该方法**刻意不提供 <c>?? 0</c> 兜底** ——
/// 退化成 0 会让所有接入方共享同一个「0 号调用方」的去重命名空间，
/// 于是 A 用过的凭证号会把 B 的到账通知**静默去重**掉（钱被吞、双方都看不到报错）。
/// </para>
/// <para>
/// 所以在测试里也必须**显式扮演一个调用方**，而不是让产品代码为「没有身份」留一条后门 ——
/// 一旦留了后门，生产环境里「这个接口没被签名鉴权保护」就会从**配置错误**退化成**静默放行**。
/// </para>
/// <para>
/// 这个助手就是那层「扮演」：往 <see cref="IHttpContextAccessor"/> 里塞一个带
/// <c>SignatureAuthenticationDefaults.OpenAccessItemKey</c> 的 <see cref="HttpContext"/>，
/// 与签名鉴权中间件在生产环境所做的事一致。
/// </para>
/// <para>
/// <b>安全性</b>：测试工程已声明
/// <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c>，
/// 用例串行执行，因此共享单例 <see cref="IHttpContextAccessor"/> 不会互相覆盖。
/// </para>
/// <para>
/// <b>用法</b>：<c>using var caller = PayTestCaller.Begin();</c> ——
/// 释放时恢复原上下文（通常是 <c>null</c>），保证「无调用方」的用例仍然可写。
/// </para>
/// </remarks>
internal static class PayTestCaller
{
    /// <summary>
    /// 测试专用调用方 Id。刻意选在 <c>9990000000001</c> 这个远离真实凭证的号段，
    /// 万一有残留也能一眼认出来是测试数据。
    /// </summary>
    public const long ClientId = 9990000000001L;

    /// <summary>测试专用身份标识（accessKey）</summary>
    public const string AccessKey = "unittest-access-key";

    /// <summary>
    /// 建立调用方上下文。返回的对象释放时恢复原上下文。
    /// </summary>
    /// <param name="clientId">调用方 Id（默认 <see cref="ClientId"/>）</param>
    /// <param name="accessKey">身份标识（默认 <see cref="AccessKey"/>）</param>
    public static IDisposable Begin(long clientId = ClientId, string accessKey = AccessKey)
    {
        var accessor = App.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;

        var httpContext = new DefaultHttpContext();
        httpContext.Items[SignatureAuthenticationDefaults.OpenAccessItemKey] = new SysOpenAccess
        {
            Id = clientId,
            AccessKey = accessKey,
        };
        accessor.HttpContext = httpContext;

        return new RestoreScope(accessor, previous);
    }

    /// <summary>恢复上下文（嵌套使用时按栈序还原）</summary>
    private sealed class RestoreScope(IHttpContextAccessor accessor, HttpContext previous) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = previous;
    }
}
