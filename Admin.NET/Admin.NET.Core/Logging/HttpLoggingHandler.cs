// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// http日志处理
/// </summary>
public class HttpLoggingHandler : DelegatingHandler, ITransient
{
    private readonly Dictionary<string, HttpRemoteItem> _optionMap;
    private readonly SysConfigService _sysConfigService;
    private readonly IEventPublisher _eventPublisher;

    public HttpLoggingHandler(IEventPublisher eventPublisher, SysConfigService sysConfigService)
    {
        _eventPublisher = eventPublisher;
        _sysConfigService = sysConfigService;
        _optionMap = App.Configuration.GetSection("HttpRemote").GetChildren()
            .Select(u => u.Get<HttpRemoteItem>())
            .ToDictionary(opt => opt.HttpName, opt => opt);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // 判断全局Http日志开关
        var enabledLog = await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysLogHttp);
        if (!enabledLog) return await base.SendAsync(request, cancellationToken);

        // 判断当前配置日志开关
        var httpClientName = request.GetHttpClientName();
        var attr = request.GetHttpRemoteApiAttr();
        if (!string.IsNullOrWhiteSpace(httpClientName)) enabledLog = _optionMap.GetOrDefault(httpClientName).EnabledLog;
        if (!enabledLog || attr?.IgnoreLog == true) return await base.SendAsync(request, cancellationToken);

        var stopWatch = Stopwatch.StartNew();
        var userManger = App.GetService<UserManager>();
        var urlList = request.RequestUri?.LocalPath.Split("/") ?? [];
        var sysLogHttp = new SysLogHttp
        {
            HttpApiDesc = attr?.Desc,
            HttpClientName = httpClientName,
            HttpMethod = request.Method.Method,
            ActionName = urlList.Length >= 2 ? $"{urlList[^2]}/{urlList[^1]}" : urlList.Length >= 1 ? urlList[^1] : null,
            RequestUrl = request.RequestUri?.ToString(),
            RequestHeaders = request.Headers.ToDictionary(u => u.Key, u => u.Value.Join(";")).ToJson(),
            RequestBodyPlaintext = request.GetRequestBodyPlaintext(),
            TenantId = userManger?.TenantId,
            CreateUserId = userManger?.UserId,
            CreateUserName = userManger?.RealName,
        };
        if (request.Content != null) sysLogHttp.RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        sysLogHttp.StartTime = DateTime.Now;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            stopWatch.Stop();
            sysLogHttp.EndTime = DateTime.Now;
            sysLogHttp.StatusCode = response.StatusCode;
            sysLogHttp.ResponseBodyPlaintext = await response.GetResponseBodyPlaintext();
            sysLogHttp.ResponseHeaders = response.Headers.ToDictionary(u => u.Key, u => u.Value.Join(";")).ToJson();
            sysLogHttp.IsSuccessStatusCode = response.IsSuccessStatusCode ? YesNoEnum.Y : YesNoEnum.N;
            sysLogHttp.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            stopWatch.Stop();
            sysLogHttp.EndTime = DateTime.Now;
            sysLogHttp.IsSuccessStatusCode = YesNoEnum.N;
            sysLogHttp.Exception = JSON.Serialize(SerializableException.FromException(ex));
            throw;
        }
        finally
        {
            sysLogHttp.Elapsed = stopWatch.ElapsedMilliseconds;
            await _eventPublisher.PublishAsync(nameof(AppEventSubscriber.CreateHttpLog), sysLogHttp, cancellationToken);
        }
    }
}