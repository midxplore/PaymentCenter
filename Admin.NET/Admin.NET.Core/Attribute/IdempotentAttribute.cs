// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Newtonsoft.Json;
using System.Security.Claims;

namespace Admin.NET.Core;

/// <summary>
/// 防止重复请求过滤器特性(使用分布式锁，需确保系统支持分布式锁)
/// </summary>
[SuppressSniffer]
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
public class IdempotentAttribute(int intervalTime = 5, bool throwBah = true, string message = "您的操作过于频繁，请稍后再试！") : Attribute, IAsyncActionFilter
{
    private static readonly Lazy<SysCacheService> SysCacheService = new(() => App.GetService<SysCacheService>());

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var path = httpContext.Request.Path.Value;
        var userId = httpContext.User.FindFirstValue(ClaimConst.UserId);
        var parameters = JsonConvert.SerializeObject(context.ActionArguments, Formatting.None, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include
        });

        // 分布式锁
        var md5Key = MD5Encryption.Encrypt($"{path}{userId}{parameters}");
        using var distributedLock = SysCacheService.Value.BeginCacheLock(CacheConst.KeyIdempotent + md5Key);
        if (distributedLock == null)
        {
            if (throwBah) throw Oops.Oh(message);
            return;
        }

        // 判断是否存在重复请求
        var cacheKey = CacheConst.KeyIdempotent + "cache:" + md5Key;
        var isExist = SysCacheService.Value.ExistKey(cacheKey);
        if (isExist)
        {
            if (throwBah) throw Oops.Oh(message);
            return;
        }

        // 标记请求
        SysCacheService.Value.Set(cacheKey, 1, TimeSpan.FromSeconds(intervalTime));
        await next();
    }
}