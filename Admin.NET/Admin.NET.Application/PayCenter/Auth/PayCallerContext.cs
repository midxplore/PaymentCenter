// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 开放接口调用方上下文 —— 「当前请求是谁在调」的**唯一解析入口**
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要有这个类</b>：调用方身份原先由 <see cref="PayAllocateService"/> 与
/// <see cref="PayNotifyService"/> 各自实现一份 <c>ResolveClientIdAsync</c>。
/// 两份实现意味着两处可能漂移 —— 而 <c>ClientId</c> 在本系统里同时承担
/// 「幂等键的隔离维度」「订单归属」「审计主体」三个职责，任何一处解析口径不一致，
/// 都会表现为「同一调用方在两个接口里被认成两个人」，排查成本极高。
/// </para>
/// <para>
/// <b>身份来源</b>：取签名鉴权<b>已经校验过</b>的身份 ——
/// 框架在 <c>OnValidated</c> 成功回调里写入
/// <c>HttpContext.Items[SignatureAuthenticationDefaults.OpenAccessItemKey]</c>。
/// 刻意**不**去读 <c>accessKey</c> 请求头再查一次库：那等于把已校验的身份重新推导一遍，
/// 既多一次查询，也可能与「实际生效的那把密钥」不一致（比如密钥刚被轮换）。
/// </para>
/// <para>
/// <b>为什么需要「必填」版本</b>：见 <see cref="RequireClientId"/> 的说明。
/// </para>
/// </remarks>
public static class PayCallerContext
{
    /// <summary>
    /// 解析已校验的调用方凭证对象
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <returns>凭证对象；未经签名鉴权或鉴权未通过时返回 null</returns>
    public static SysOpenAccess Resolve(HttpContext context)
        => context?.Items[SignatureAuthenticationDefaults.OpenAccessItemKey] as SysOpenAccess;

    /// <summary>
    /// 解析调用方Id（<c>SysOpenAccess.Id</c>）
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <returns></returns>
    public static long? ClientId(HttpContext context) => Resolve(context)?.Id;

    /// <summary>
    /// 解析调用方身份标识（AccessKey）
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <returns></returns>
    public static string AccessKey(HttpContext context) => Resolve(context)?.AccessKey;

    /// <summary>
    /// 解析调用方Id，解析不到则**抛错**
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于「没有调用方身份就不能正确执行」的场景，目前是
    /// <c>allocate</c>（幂等键的隔离维度）与 <c>status</c>（订单归属维度）。
    /// </para>
    /// <para>
    /// <b>为什么宁可抛错也不要退化成 null</b>：
    /// 若返回 null 继续执行，<c>ClientId</c> 会落成 NULL，
    /// 于是这些请求会共享「NULL 调用方」这一个命名空间 ——
    /// 不同接入方的幂等键会互相命中，等于把刚修掉的跨调用方串单缺口重新打开，
    /// 而且**不报任何错**，只在事后对账时才发现钱进了别人的账户。
    /// </para>
    /// <para>
    /// 正常情况下本方法不可能抛错：接口挂了 <c>[Authorize(Signature)]</c>，
    /// 认证成功必然经过 <c>OnValidated</c> 并写入 Items。
    /// 所以一旦抛错，说明鉴权链路被改坏了（例如误加了 <c>[AllowAnonymous]</c>、
    /// 或换了别的认证方案却忘了补 Items）—— 这类问题必须**立刻可见**，
    /// 而不是以「静默串单」的形式潜伏下去。
    /// </para>
    /// </remarks>
    /// <param name="context">当前请求上下文</param>
    /// <returns>调用方Id</returns>
    /// <exception cref="Exception">无法解析调用方身份时抛出</exception>
    public static long RequireClientId(HttpContext context)
    {
        var id = ClientId(context);
        if (id == null)
            throw Oops.Bah("无法解析调用方身份，已拒绝执行：签名鉴权上下文缺失。"
                + "该接口必须由签名鉴权保护（不应为匿名或其它认证方案）。");

        return id.Value;
    }
}
