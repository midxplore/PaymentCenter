// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 数据库日志写入器
/// </summary>
public class DatabaseLoggingWriter : IDatabaseLoggingWriter, IDisposable
{
    private readonly ILogger<DatabaseLoggingWriter> _logger;
    private readonly SysConfigService _sysConfigService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IServiceScope _serviceScope;
    private readonly SqlSugarScopeProvider _db;

    public DatabaseLoggingWriter(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DatabaseLoggingWriter> logger,
        IEventPublisher eventPublisher)
    {
        _serviceScope = serviceScopeFactory.CreateScope();
        _sysConfigService = _serviceScope.ServiceProvider.GetRequiredService<SysConfigService>();
        _eventPublisher = eventPublisher;
        _logger = logger;

        // 切换日志独立数据库
        _db = SqlSugarSetup.ITenant.IsAnyConnection(SqlSugarConst.LogConfigId)
            ? SqlSugarSetup.ITenant.GetConnectionScope(SqlSugarConst.LogConfigId)
            : SqlSugarSetup.ITenant.GetConnectionScope(SqlSugarConst.MainConfigId);
    }

    /// <summary>
    /// 根据操作日志内容解析出控制器名称和函数名称
    /// </summary>
    /// <param name="logText"></param>
    /// <returns></returns>
    private static (string actionName, string controllerName) ExtractActionAndController(string logText)
    {
        try
        {
            var targetLine = logText.Split('\n')[1]; // 获取第二行
            var parts = targetLine.Split('.'); // 按点号分割字符串
            // 获取最后两个部分
            if (parts.Length >= 2)
            {
                var actionName = parts[^1].Split('(')[0].Trim(); // 移除方法参数部分
                var controllerName = parts[^2].Trim(); // 获取Controller名称
                return (actionName, controllerName);
            }
            return (string.Empty, string.Empty);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    public async Task WriteAsync(LogMessage logMsg, bool flush)
    {
        var jsonStr = logMsg.Context?.Get("loggingMonitor")?.ToString();
        if (string.IsNullOrWhiteSpace(jsonStr))
        {
            var title = logMsg.Context.Get("Title")?.ToString() ?? "自定义操作日志";
            var actionName = logMsg.Context.Get("Action")?.ToString() ?? "";
            var controllerName = logMsg.Context.Get("Controller")?.ToString() ?? "";
            var url = logMsg.Context.Get("Url")?.ToString() ?? "";
            var method = logMsg.Context.Get("Method")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(actionName) && string.IsNullOrWhiteSpace(controllerName))
            {
                // 从日志内容中获取控制器名称和函数名称
                var (action, controller) = ExtractActionAndController(logMsg.Message);
                actionName = action;
                controllerName = controller;
            }
            await _db.Insertable(new SysLogOp
            {
                DisplayTitle = title,
                LogDateTime = logMsg.LogDateTime,
                ActionName = actionName,
                ControllerName = controllerName,
                EventId = logMsg.EventId.Id,
                ThreadId = logMsg.ThreadId,
                TraceId = logMsg.TraceId,
                Exception = logMsg.Exception == null ? null : JSON.Serialize(logMsg.Exception),
                Message = logMsg.Message,
                LogLevel = logMsg.LogLevel,
                HttpMethod = method,
                RequestUrl = url,
                Status = "200",
            }).ExecuteCommandAsync();
            return;
        }

        // 获取当前操作者
        var loggingMonitor = JSON.Deserialize<LoggingMonitorDto>(jsonStr);
        var userInfo = GetUserInfo(loggingMonitor);

        // 优先获取 X-Forwarded-For 头部信息携带的IP地址（如nginx代理配置转发）
        var reqHeaders = loggingMonitor.RequestHeaders.ToDictionary(u => u.Key, u => u.Value);
        var remoteIPv4 = reqHeaders.GetValueOrDefault("X-Forwarded-For")?.ToString();

        // 获取IP地理位置
        if (string.IsNullOrEmpty(remoteIPv4)) remoteIPv4 = loggingMonitor.RemoteIPv4;
        (string ipLocation, double? longitude, double? latitude) = CommonHelper.GetIpAddress(remoteIPv4);

        // 获取设备信息
        var os = "";
        var browser = "";
        if (loggingMonitor.UserAgent != null)
        {
            var client = Parser.GetDefault().Parse(loggingMonitor.UserAgent);
            browser = $"{client.UA.Family} {client.UA.Major}.{client.UA.Minor} / {client.Device.Family}";
            os = $"{client.OS.Family} {client.OS.Major} {client.OS.Minor}";
        }

        // 捕捉异常，否则会由于 unhandled exception 导致程序崩溃
        try
        {
            var logEntity = new SysLogOp
            {
                ControllerName = loggingMonitor.DisplayName,
                ActionName = loggingMonitor.ActionTypeName,
                DisplayTitle = loggingMonitor.DisplayTitle,
                Status = loggingMonitor.ReturnInformation?.HttpStatusCode?.ToString(),
                RemoteIp = remoteIPv4,
                Location = ipLocation,
                Longitude = longitude,
                Latitude = latitude,
                Browser = browser,
                Os = os,
                Elapsed = loggingMonitor.TimeOperationElapsedMilliseconds,
                Message = logMsg.Message,
                HttpMethod = loggingMonitor.HttpMethod,
                RequestUrl = loggingMonitor.RequestUrl,
                RequestParam = loggingMonitor.Parameters is { Count: > 0 } ? JSON.Serialize(loggingMonitor.Parameters[0].Value) : null,
                ReturnResult = loggingMonitor.ReturnInformation?.Value != null ? JSON.Serialize(loggingMonitor.ReturnInformation?.Value) : null,
                Exception = loggingMonitor.Exception == null ? JSON.Serialize(logMsg.Exception) : JSON.Serialize(loggingMonitor.Exception),
                LogDateTime = logMsg.LogDateTime,
                EventId = logMsg.EventId.Id,
                ThreadId = logMsg.ThreadId,
                TraceId = logMsg.TraceId,
                Account = userInfo.Account,
                RealName = userInfo.RealName,
                CreateUserId = userInfo.UserId,
                CreateUserName = userInfo.RealName,
                TenantId = userInfo.TenantId,
                LogLevel = logMsg.LogLevel,
            };

            // 记录异常日志-发送邮件
            if (logMsg.Exception != null || loggingMonitor.Exception != null)
            {
                await _db.Insertable(logEntity.Adapt<SysLogEx>()).ExecuteCommandAsync();
                // 将异常日志发送到邮件
                await _eventPublisher.PublishAsync(nameof(AppEventSubscriber.SendErrorMail), logMsg.Exception ?? loggingMonitor.Exception);
                return;
            }

            // 记录访问日志-登录退出
            if (loggingMonitor.ActionName == "login" || loggingMonitor.ActionName == "loginPhone" || loggingMonitor.ActionName == "logout")
            {
                await _db.Insertable(logEntity.Adapt<SysLogVis>()).ExecuteCommandAsync();
                return;
            }

            // 记录操作日志
            if (!await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysOpLog)) return;
            await _db.Insertable(logEntity.Adapt<SysLogOp>()).ExecuteCommandAsync();

            await Task.Delay(50); // 延迟 0.05 秒写入数据库，有效减少高频写入数据库导致死锁问题
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "操作日志入库");
            // 将异常日志发送到邮件
            await _eventPublisher.PublishAsync(nameof(AppEventSubscriber.SendErrorMail), ex);
        }
    }

    /// <summary>
    /// 从日志消息中获取用户信息
    /// </summary>
    /// <param name="loggingMonitor"></param>
    /// <returns></returns>
    private LoggingUserInfo GetUserInfo(LoggingMonitorDto loggingMonitor)
    {
        LoggingUserInfo result = new();
        if (loggingMonitor.AuthorizationClaims != null)
        {
            var authDict = loggingMonitor.AuthorizationClaims.ToDictionary(u => u.Type, u => u.Value);
            result.UserId = long.TryParse(authDict?.GetValueOrDefault(ClaimConst.UserId) ?? "", out var userId) ? userId : null;
            result.Account = authDict?.GetValueOrDefault(ClaimConst.Account);
            result.RealName = authDict?.GetValueOrDefault(ClaimConst.RealName);
            result.TenantId = long.TryParse(authDict?.GetValueOrDefault(ClaimConst.TenantId) ?? "", out var tenantId) ? tenantId : null;
        }

        // 登陆时没有用户Id，需要根据入参获取
        if (result.UserId == null && loggingMonitor.ActionName == "login" && loggingMonitor.Parameters is { Count: > 0 })
        {
            result.Account = (loggingMonitor.Parameters[0].Value as JObject)!.GetValue("account")?.ToString();
            if (!string.IsNullOrEmpty(result.Account))
            {
                var db = SqlSugarSetup.ITenant.GetConnectionScope(SqlSugarConst.MainConfigId);
                var user = db.Queryable<SysUser>().First(u => u.Account == result.Account);
                result.TenantId = user?.TenantId;
                result.RealName = user?.RealName;
                result.UserId = user?.Id;
            }
        }
        return result;
    }

    /// <summary>
    /// 释放服务作用域
    /// </summary>
    public void Dispose()
    {
        _serviceScope.Dispose();
    }
}