// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

// 作业调度相关的特性与接口（JobDetail / Minutely / IJob / JobExecutingContext）
// 只在本文件用到，因此就地 using，不扩大 Admin.NET.Application 的全局 using 面。
using Furion.Logging;
using Furion.Schedule;

namespace Admin.NET.Application;

/// <summary>
/// 订单过期扫描作业（F3.2，每分钟执行）
/// </summary>
/// <remarks>
/// <para>
/// 照骨架自带的 <c>CleanSysLogJob</c> 写：<c>[JobDetail]</c> 声明作业本体，
/// <c>[Minutely]</c> 声明触发器（每分钟一次）。作业会被骨架的调度器自动发现并持久化到
/// <c>sysjobdetail</c> / <c>sysjobtrigger</c>，可在后台「任务调度」页面查看与手工执行。
/// </para>
/// <para>
/// <c>Concurrent = false</c>：同一时刻只允许一个实例在跑，避免两轮扫描互相抢同一批订单
/// （虽然订单侧的条件更新已经能保证正确性，但串行能省掉无谓的抢占失败）。
/// </para>
/// <para>
/// 本作业只做「取 scope → 调服务」，过期逻辑全部在
/// <see cref="PayOrderExpireService"/>，便于脱离调度器测试。
/// </para>
/// </remarks>
[JobDetail(PayConst.ExpireJobId, Description = "收款订单过期扫描", GroupName = "default", Concurrent = false)]
[Minutely(TriggerId = PayConst.ExpireJobTriggerId, Description = "收款订单过期扫描")]
public class PayOrderExpireJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var serviceScope = _serviceScopeFactory.CreateScope();
        var expireService = serviceScope.ServiceProvider.GetRequiredService<PayOrderExpireService>();

        var expiredCount = await expireService.ExpireBatchAsync(PayConst.ExpireScanBatchSize, stoppingToken);
        if (expiredCount > 0)
            Log.Information($"【定时任务】收款订单过期扫描完成，本轮过期 {expiredCount} 单 {DateTime.Now}");
    }
}
