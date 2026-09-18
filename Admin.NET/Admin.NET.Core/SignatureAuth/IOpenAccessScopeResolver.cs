// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 开放接口权限范围（scope）解析器（F6.2）
/// </summary>
/// <remarks>
/// <para>
/// 骨架内置的签名鉴权只回答「你是谁」（<see cref="SysOpenAccess"/> + HMAC 签名 + 时间戳 + nonce），
/// 不回答「你能调哪些接口」。本接口是留给业务模块的<b>扩展点</b>：
/// 业务声明「这个请求需要什么 scope」，骨架在签名校验通过后统一比对
/// <see cref="SysOpenAccess.Scopes"/>。
/// </para>
/// <para>
/// 为什么不直接在 <c>GetSignatureAuthenticationEventImpl</c> 里写业务规则：
/// 该方法位于 <c>Admin.NET.Core</c>，而 Core 不能反向依赖业务程序集。
/// 所以由 Core 定义钩子、由业务实现规则（框架留钩子 + 业务填增量），
/// 而不是让业务自己再写一套签名校验。
/// </para>
/// <para>实现类注册为单例即可；未注册任何实现时表示「所有开放接口都不做 scope 限制」。</para>
/// </remarks>
public interface IOpenAccessScopeResolver
{
    /// <summary>
    /// 解析当前请求所需的 scope
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <returns>所需 scope；返回 null 或空表示该请求不需要 scope 校验</returns>
    string ResolveRequiredScope(HttpContext context);
}

/// <summary>
/// 开放接口权限范围（scope）比对
/// </summary>
public static class OpenAccessScopeMatcher
{
    /// <summary>
    /// 判断已授权的 scope 集合是否覆盖所需 scope
    /// </summary>
    /// <param name="grantedScopes">已授权 scope（逗号分隔，来自 <see cref="SysOpenAccess.Scopes"/>）</param>
    /// <param name="requiredScope">所需 scope</param>
    /// <returns></returns>
    /// <remarks>
    /// <b>未配置 scope 视为「未授权任何接口」</b>，即失败关闭（fail-closed）而不是失败放行。
    /// 这是刻意的：这些接口直接关联资金，升级后宁可让接入方看到一条明确的
    /// 「未配置权限范围」报错，也不能因为配置遗漏而把接口默认敞开。
    /// 报错信息会写清需要哪个 scope，排查成本很低。
    /// </remarks>
    public static bool IsGranted(string grantedScopes, string requiredScope)
    {
        if (string.IsNullOrWhiteSpace(requiredScope)) return true;
        if (string.IsNullOrWhiteSpace(grantedScopes)) return false;

        foreach (var item in grantedScopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(item, requiredScope, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
