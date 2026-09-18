// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 清理系统日志作业任务（每天 00:00:00 执行）
/// </summary>
[JobDetail("clean_sys_log_job", Description = "清理系统日志", GroupName = "default", Concurrent = false)]
[Daily(TriggerId = "clean_sys_log_trigger", Description = "清理系统日志")]
public class CleanSysLogJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var serviceScope = _serviceScopeFactory.CreateScope();

        var logVisRep = serviceScope.ServiceProvider.GetRequiredService<SqlSugarRepository<SysLogVis>>().CopyNew();
        var logOpRep = serviceScope.ServiceProvider.GetRequiredService<SqlSugarRepository<SysLogOp>>().CopyNew();
        var logDiffRep = serviceScope.ServiceProvider.GetRequiredService<SqlSugarRepository<SysLogDiff>>().CopyNew();
        var logTriggerRep = serviceScope.ServiceProvider.GetRequiredService<SqlSugarRepository<SysJobTriggerRecord>>().CopyNew();
        var sysConfigService = serviceScope.ServiceProvider.GetRequiredService<SysConfigService>();

        // 日志保留天数
        var daysAgo = await sysConfigService.GetConfigValueByCode<int>(ConfigConst.SysLogRetentionDays);
        // 删除访问日志
        await logVisRep.AsDeleteable().Where(u => u.CreateTime < DateTime.Now.AddDays(-daysAgo)).ExecuteCommandAsync(stoppingToken);
        // 删除操作日志
        await logOpRep.AsDeleteable().Where(u => u.CreateTime < DateTime.Now.AddDays(-daysAgo)).ExecuteCommandAsync(stoppingToken);
        // 删除差异日志
        await logDiffRep.AsDeleteable().Where(u => u.CreateTime < DateTime.Now.AddDays(-daysAgo)).ExecuteCommandAsync(stoppingToken);
        // 删除作业触发器运行记录
        await logTriggerRep.AsDeleteable().Where(u => u.CreatedTime < DateTime.Now.AddDays(-daysAgo)).ExecuteCommandAsync(stoppingToken);

        var originColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        var message = $"【定时任务】清理系统日志成功，清理 {daysAgo} 天前的日志数据 {DateTime.Now}";
        Console.WriteLine(message);
        Log.Information(message);
        Console.ForegroundColor = originColor;

        // 清理默认临时文件夹目录
        var tempFolderPath = Path.Combine(App.WebHostEnvironment.WebRootPath, "temp");
        DeleteAllFiles(tempFolderPath);
    }

    /// <summary>
    /// 递归删除文件及子文件夹中的文件
    /// </summary>
    /// <param name="folderPath"></param>
    public void DeleteAllFiles(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return;

            // 删除当前文件夹中的所有文件
            foreach (string file in Directory.GetFiles(folderPath))
            {
                File.Delete(file);
            }

            // 递归删除子文件夹中的文件
            foreach (string subDirectory in Directory.GetDirectories(folderPath))
            {
                DeleteAllFiles(subDirectory);
            }
        }
        catch (Exception ex)
        {
            var message = $"【定时任务】清理临时文件异常，错误信息：{ex.Message} {DateTime.Now}";
            Console.WriteLine(message);
            Log.Information(message);
        }
    }
}