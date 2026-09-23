// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Furion.Logging;
using Furion.Schedule;

namespace Admin.NET.Application;

/// <summary>
/// Webhook outbox 派发作业（每分钟）
/// </summary>
[JobDetail(PayConst.WebhookJobId, Description = "商户Webhook派发", GroupName = "default", Concurrent = false)]
[Minutely(TriggerId = PayConst.WebhookJobTriggerId, Description = "商户Webhook派发")]
public class PayWebhookDispatchJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var serviceScope = _serviceScopeFactory.CreateScope();
        var webhookService = serviceScope.ServiceProvider.GetRequiredService<PayWebhookService>();

        var ok = await webhookService.DispatchBatchAsync(PayConst.WebhookDispatchBatchSize, stoppingToken);
        if (ok > 0)
            Log.Information($"【定时任务】Webhook 派发完成，本轮成功 {ok} 条 {DateTime.Now}");
    }
}
