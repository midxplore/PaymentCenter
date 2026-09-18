// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 作业执行监视器
/// </summary>
public class JobMonitor : IJobMonitor
{
    private readonly SysConfigService _sysConfigService;
    private readonly SysCacheService _sysCacheService;
    private readonly SysWeComService _sysWeComService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<JobMonitor> _logger;

    public JobMonitor(IServiceScopeFactory serviceScopeFactory, SysCacheService sysCacheService, IEventPublisher eventPublisher, ILogger<JobMonitor> logger)
    {
        var serviceScope = serviceScopeFactory.CreateScope();
        _sysConfigService = serviceScope.ServiceProvider.GetRequiredService<SysConfigService>();
        _sysWeComService = serviceScope.ServiceProvider.GetRequiredService<SysWeComService>();
        _sysCacheService = sysCacheService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public Task OnExecutingAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        var key = $"{CacheConst.KeyDisLockJob}{context.JobId}";
        using var dis = _sysCacheService.BeginCacheLock(key, throwOnFailure: false);
        if (dis == null || _sysCacheService.ExistKey(key)) return Task.CompletedTask;

        // 标记作业正在运行，防止重复执行
        _sysCacheService.Set($"{key}:running", 1, TimeSpan.FromMinutes(60));
        return Task.CompletedTask;
    }

    public async Task OnExecutedAsync(JobExecutedContext context, CancellationToken stoppingToken)
    {
        var key = $"{CacheConst.KeyDisLockJob}{context.JobId}";
        _sysCacheService.Remove($"{key}:running");

        // 将作业信息发送到企业微信
        if (await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysErrorWeCom))
            await _sysWeComService.PushScheduledTask(new()
            {
                TaskDesc = context.JobDetail.Description,
                BeginTime = context.OccurrenceTime,
                EndTime = context.ExecutedTime,
                Exception = context.Exception?.Message
            });

        if (context.Exception == null) return;

        var exception = string.Format("定时任务【{0}】错误：{1}", context.Trigger.Description, context.Exception);

        // 将作业异常信息记录到本地
        _logger.LogError(exception);

        // 将作业异常信息发送到邮件
        if (await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysErrorMail))
            await _eventPublisher.PublishAsync(nameof(AppEventSubscriber.SendErrorMail), exception, stoppingToken);
    }
}