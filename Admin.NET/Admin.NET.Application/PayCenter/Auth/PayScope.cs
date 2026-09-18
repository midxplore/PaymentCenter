// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 声明开放接口所需的权限范围（F6.2）
/// </summary>
/// <remarks>
/// 与接口放在一起声明，而不是在别处维护一张「路径 → scope」映射表：
/// 新增/调整开放接口时不会忘记同步权限规则。
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public class PayScopeAttribute : Attribute
{
    /// <summary>
    /// 所需权限范围
    /// </summary>
    public string Scope { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="scope">所需权限范围，取值见 <see cref="PayConst"/></param>
    public PayScopeAttribute(string scope) => Scope = scope;
}

/// <summary>
/// 收款中心开放接口的权限范围解析器（F6.2）
/// </summary>
/// <remarks>
/// <para>
/// 实现骨架留出的 <see cref="IOpenAccessScopeResolver"/> 钩子：骨架在签名校验通过后回调本类，
/// 拿到「当前请求需要什么 scope」，再去比对 <c>SysOpenAccess.Scopes</c>。
/// </para>
/// <para>本类只做「声明规则」，不重复实现签名/时间戳/nonce 校验——那些骨架已经做完了。</para>
/// </remarks>
public class PayScopeResolver : IOpenAccessScopeResolver, ISingleton
{
    /// <summary>
    /// 解析当前请求所需的 scope
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <returns>所需 scope；非收款中心开放接口返回 null（不限制）</returns>
    public string ResolveRequiredScope(HttpContext context)
    {
        // 首选接口上的 [PayScope] 声明 —— 这是**主机制**：权限规则与接口定义放在一起，
        // 新增/调整接口时不会忘记同步。
        var declared = context.GetEndpoint()?.Metadata.GetMetadata<PayScopeAttribute>()?.Scope;
        if (!string.IsNullOrWhiteSpace(declared)) return declared;

        // 兜底：按路径判定。鉴权在管道里晚于路由解析，正常情况下端点元数据一定拿得到，
        // 这里只防一种最坏情况——元数据取不到导致 scope 检查被静默跳过（等于接口敞开）。
        // 因此兜底的方向是「宁可多校验」，而不是「取不到就放行」。
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/pay", StringComparison.OrdinalIgnoreCase)) return null;

        if (path.Contains("/notify", StringComparison.OrdinalIgnoreCase)) return PayConst.ScopeNotify;
        if (path.Contains("/allocate", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/status", StringComparison.OrdinalIgnoreCase)) return PayConst.ScopeAllocate;

        // ★ 落在 /api/pay* 下但没声明、也匹配不上已知模式 → **fail-closed**。
        //
        // 原先这里返回 null（= 不限制），等于「新加一个开放接口、忘了写 [PayScope]」
        // 就会**默认敞开**给任何持有效密钥的接入方 —— 而这是资金系统里最不该出现的默认值。
        // 现在返回一个默认无人持有的 scope，默认拒绝，并在 401 消息里写明需要哪个 scope。
        //
        // 注意：不能因为「兜底匹配不上」就返回 null —— 那正是 fail-open 的写法。
        return PayConst.ScopeUnclassified;
    }
}
