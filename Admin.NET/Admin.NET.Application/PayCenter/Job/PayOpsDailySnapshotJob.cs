// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Furion.Logging;
using Furion.Schedule;

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总日终快照（每日午夜，写入昨日存量）
/// </summary>
/// <remarks>与 <c>CleanSysLogJob</c> 同用 <c>[Daily]</c>（00:00）；计划文档写的 00:05 与午夜等价满足「昨日日终」。</remarks>
[JobDetail(PayConst.OpsDailySnapshotJobId, Description = "运营汇总日终快照", GroupName = "default", Concurrent = false)]
[Daily(TriggerId = PayConst.OpsDailySnapshotTriggerId, Description = "运营汇总日终快照")]
public class PayOpsDailySnapshotJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<PayOpsSnapshotService>();
        var written = await svc.WriteYesterdaySnapshotAsync(stoppingToken);
        if (written)
            Log.Information($"【定时任务】运营汇总日终快照已写入 {DateTime.Now.Date.AddDays(-1):yyyy-MM-dd} {DateTime.Now}");
    }
}
