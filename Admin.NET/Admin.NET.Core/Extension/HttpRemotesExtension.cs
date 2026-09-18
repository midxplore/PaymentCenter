// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// Http远程服务扩展
/// </summary>
public static class HttpRemoteExtension
{
    private static readonly HttpRequestOptionsKey<string> HttpNameKey = new("__HTTP_CLIENT_NAME__");
    private static readonly HttpRequestOptionsKey<HttpRemoteApiAttribute> AttrKey = new(nameof(HttpRemoteApiAttribute));
    private static readonly HttpRequestOptionsKey<string> ReqPlaintextKey = new(nameof(SysLogHttp.RequestBodyPlaintext));
    private static readonly HttpRequestOptionsKey<Func<HttpResponseMessage, Task<string>>> RespPlaintextFunc = new(nameof(SysLogHttp.ResponseBodyPlaintext) + "Func");

    /// <summary>
    /// 添加Http远程服务
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddHttpRemoteClientService(this IServiceCollection services)
    {
        var options = App.Configuration.GetSection("HttpRemote").GetChildren().Select(u => u.Get<HttpRemoteItem>());
        foreach (var opt in options)
        {
            services.AddHttpClient(opt.HttpName, client =>
                {
                    client.BaseAddress = new Uri(opt.BaseAddress);
                    client.Timeout = TimeSpan.FromSeconds(opt.Timeout);
                    foreach (var kv in opt.Headers) client.DefaultRequestHeaders.Add(kv.Key, kv.Value);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseCookies = opt.UseCookies
                })
                .AddHttpMessageHandler<HttpLoggingHandler>();
        }
        return services;
    }

    /// <summary>
    /// 设置请求接口相关属性
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="attr"></param>
    /// <param name="plaintext">请求明文</param>
    /// <param name="decryption">获取响应体明文的代理方法(解密)</param>
    /// <returns></returns>
    public static HttpRequestBuilder SetReqRemoteApiAttr(this HttpRequestBuilder builder, HttpRemoteApiAttribute attr, string plaintext = null, Func<HttpResponseMessage, Task<string>> decryption = null)
    {
        builder.SetOnPreSendRequest(conf =>
        {
            conf.Options.Set(AttrKey, attr);
            conf.Options.Set(ReqPlaintextKey, plaintext);
            conf.Options.Set(RespPlaintextFunc, decryption);
        });
        return builder;
    }

    /// <summary>
    /// 获取客户端名称
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static string GetHttpClientName(this HttpRequestMessage request)
    {
        return request.Options.TryGetValue(HttpNameKey, out var name) ? name : null;
    }

    /// <summary>
    /// 获取Http远程接口属性
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static HttpRemoteApiAttribute GetHttpRemoteApiAttr(this HttpRequestMessage request)
    {
        return request.Options.TryGetValue(AttrKey, out var attr) ? attr : null;
    }

    /// <summary>
    /// 获取请求明文
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static string GetRequestBodyPlaintext(this HttpRequestMessage request)
    {
        return request.Options.TryGetValue(ReqPlaintextKey, out var plaintext) ? plaintext : null;
    }

    /// <summary>
    /// 获取响应明文
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static async Task<string> GetResponseBodyPlaintext(this HttpResponseMessage response)
    {
        try
        {
            if (response.RequestMessage != null && response.RequestMessage.Options.TryGetValue(RespPlaintextFunc, out var decryptionFunc))
            {
                return await decryptionFunc.Invoke(response);
            }
        }
        catch (Exception e)
        {
            Log.Error("Http远程服务响应体解密失败", e);
            throw;
        }
        return null;
    }
}

/// <summary>
/// 远程请求配置项
/// </summary>
public sealed class HttpRemoteItem
{
    /// <summary>
    /// 是否启用日志
    /// </summary>
    public bool EnabledLog { get; set; }

    /// <summary>
    /// 是否启用代理
    /// </summary>
    public bool EnabledProxy { get; set; }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string HttpName { get; set; }

    /// <summary>
    /// 服务地址
    /// </summary>
    public string BaseAddress { get; set; }

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// 是否自动处理Cookie
    /// </summary>
    public bool UseCookies { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; }
}