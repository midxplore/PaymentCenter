
using Admin.NET.Core.Extension;

namespace Admin.NET.Core;

/// <summary>
/// 支持分布式锁的Job基类
/// </summary>
public abstract class DistributedJob : IJob
{
    protected abstract Task ExecuteJobAsync(JobExecutingContext context, CancellationToken stoppingToken);

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        var lockAttr = GetType().GetCustomAttribute<DistributedLockAttribute>();
        
        if (lockAttr == null)
        {
            await ExecuteJobAsync(context, stoppingToken);
            return;
        }

        var cache =  App.GetRequiredService<ICacheProvider>().Cache;
        // var currentMinute = context.ExecutingTime.ToString("yyyyMMddHHmm");
        var lockWindow = GetLockWindow(context.ExecutingTime, context);
 
        var lockKey = string.IsNullOrEmpty(lockAttr.LockKey) 
            ? $"lock:job:exec:{context.JobId}:{lockWindow}" 
            : $"{lockAttr.LockKey}:{lockWindow}";

        var serverId = App.GetConfig<ClusterOptions>("Cluster", true)?.ServerId ?? Environment.MachineName;
        var actualLockSeconds = CalculateLockHoldSeconds(lockAttr, context);
 
        // ✅ 尝试设置执行标记（使用 Redis SET NX EX 原子操作）
        var lockAcquired = TryAcquireLock(cache, lockKey, serverId, actualLockSeconds);
        
        if (!lockAcquired)
        {
            // ❌ 获取锁失败，标记为跳过执行
            // Log.Information($"⏭️ [{serverId}] 作业 [{context.JobId}] 已在其他实例执行，跳过");
            context.SetPropertyValue("SkippedByDistributedLock", true);
            
            // 抛出特定异常告知框架任务被跳过（而不是失败）
            throw new JobSkippedException($"作业已在其他服务器实例执行");
        }

       // Console.WriteLine($"🔒 [{serverId}] 获取作业 [{context.JobId}] 执行锁成功");

        try
        {
            await ExecuteJobAsync(context, stoppingToken);
            // Log.Information($"✅ [{serverId}] 作业 [{context.JobId}] 执行完成");
        }
        catch (Exception ex)
        {
            Log.Error($"❌ [{serverId}] 作业 [{context.JobId}] 执行异常: {ex.Message}", ex);
            throw;
        }
        // 等锁自动过期
        // finally
        // {
        //     ReleaseLock(cache, lockKey, serverId);
        // } 
       
    }

    /// <summary>
    /// 尝试获取分布式锁（原子操作）
    /// </summary>
    private bool TryAcquireLock(ICache cache, string key, string serverId, int expireSeconds)
    {
        try
        {
            if (cache is FullRedis redis)
            {
                // ✅ 使用 Redis SET NX EX 原子操作
                var lockValue = $"{serverId}:{Guid.NewGuid():N}:{DateTime.Now:yyyyMMddHHmmss}";
                var result = redis.Execute(rds => 
                    rds.Execute("SET", key, lockValue, "NX", "EX", expireSeconds.ToString()), 
                    null);
                
                if (result?.ToString() == "OK")
                {
                    // 将 lockValue 存储到 AsyncLocal，用于释放时验证
                    _currentLockValue.Value = lockValue;
                    return true;
                }
                return false;
            }

            // 降级方案：使用 Add 方法（非完全原子，但可用）
            var fallbackValue = $"{serverId}:{DateTime.Now:yyyyMMddHHmmss}";
            var added = cache.Add(key, fallbackValue, expireSeconds);
            if (added)
            {
                _currentLockValue.Value = fallbackValue;
            }
            return added;
        }
        catch (Exception ex)
        {
            Log.Error($"获取分布式锁失败: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// 释放分布式锁（避免误删其他实例的锁）
    /// </summary>
    private void ReleaseLock(ICache cache, string key, string serverId)
    {
        try
        {
            var currentLockValue = _currentLockValue.Value;
            if (string.IsNullOrEmpty(currentLockValue))
            {
                return;
            }

            if (cache is FullRedis redis)
            {
                var luaScript = @"
                    if redis.call('get', KEYS[1]) == ARGV[1] then
                        return redis.call('del', KEYS[1])
                    else
                        return 0
                    end";
                var result = redis.Execute<long>(key, (rc, k) => rc.Execute<long>("EVAL", new object[] { luaScript, "1", key, currentLockValue }), true);
                // Log.Debug($"🔓 [{serverId}] 释放作业锁 [{key}]");
            }
            else
            {
                // 降级方案：直接删除（存在风险但可接受）
                cache.Remove(key);
            }

            _currentLockValue.Value = null;
        }
        catch (Exception ex)
        {
            Log.Error($"释放分布式锁失败: {ex.Message}", ex);
        }
    }

    // 用于存储当前线程的锁值
    private static readonly AsyncLocal<string?> _currentLockValue = new(); 
    
    
    /// <summary>
    /// 动态计算锁持有时间
    /// </summary>
    private int CalculateLockHoldSeconds(DistributedLockAttribute lockAttr, JobExecutingContext context)
    {
        // 如果手动指定了锁持有时间且大于0，则使用指定值
        if (lockAttr.LockHoldSeconds > 0)
        {
            return lockAttr.LockHoldSeconds;
        }

        // ✅ 自动根据触发器类型计算
        var triggerType = context.Trigger.GetType();
        var triggerAttributes = GetType().GetCustomAttributes<Attribute>()
            .Where(a => a.GetType().Namespace == "Furion.Schedule")
            .ToList();

        int periodSeconds = 60; // 默认60秒（适用于Minutely等）

        foreach (var attr in triggerAttributes)
        {
            var attrType = attr.GetType();
            
            // PeriodSeconds(n) - 每n秒执行
            if (attrType.Name == "PeriodSecondsAttribute")
            {
                var intervalProp = attrType.GetProperty("Interval");
                if (intervalProp != null)
                {
                    var intervalMs = (long)intervalProp.GetValue(attr);
                    periodSeconds = (int)(intervalMs / 1000);
                    break;
                }
            }
            // PeriodMinutes(n) - 每n分钟执行
            else if (attrType.Name == "PeriodMinutesAttribute")
            {
                var intervalProp = attrType.GetProperty("Interval");
                if (intervalProp != null)
                {
                    var intervalMs = (long)intervalProp.GetValue(attr);
                    periodSeconds = (int)(intervalMs / 1000);
                    break;
                }
            }
            // Minutely - 每分钟执行
            else if (attrType.Name == "MinutelyAttribute")
            {
                periodSeconds = 60;
                break;
            }
            // Hourly - 每小时执行
            else if (attrType.Name == "HourlyAttribute")
            {
                periodSeconds = 3600;
                break;
            }
            // Daily - 每天执行
            else if (attrType.Name == "DailyAttribute")
            {
                periodSeconds = 86400;
                break;
            }
            // Cron - 根据下次执行时间计算
            else if (attrType.Name == "CronAttribute")
            {
                var nextRunTime = context.Trigger.NextRunTime;
                if (nextRunTime.HasValue)
                {
                    var interval = (nextRunTime.Value - context.ExecutingTime).TotalSeconds;
                    if (interval > 0 && interval < 86400) // 合理范围内
                    {
                        periodSeconds = (int)interval;
                        break;
                    }
                }
            }
        }

        // ✅ 应用安全系数（默认1.2倍），确保有足够时间完成任务
        var lockSeconds = (int)(periodSeconds * lockAttr.LockHoldMultiplier);
        
        // 设置合理的上下限
        lockSeconds = Math.Max(lockSeconds, 5);     // 最少5秒
        lockSeconds = Math.Min(lockSeconds, 3600);  // 最多1小时

        Log.Debug($"自动计算锁持有时间: 周期={periodSeconds}秒, 系数={lockAttr.LockHoldMultiplier}, 锁持有={lockSeconds}秒");
        
        return lockSeconds;
    }
    
     /// <summary>
    /// 获取锁窗口标识
    /// </summary>
    private string GetLockWindow(DateTime scheduledTime, JobExecutingContext context)
    {
        var triggerType = context.Trigger.GetType();
        var triggerAttributes = GetType().GetCustomAttributes<Attribute>()
            .Where(a => a.GetType().Namespace == "Furion.Schedule")
            .ToList();

        // ✅ 根据不同的触发器类型，生成不同精度的窗口标识
        foreach (var attr in triggerAttributes)
        {
            var attrType = attr.GetType();

            // PeriodSeconds - 精确到秒
            if (attrType.Name == "PeriodSecondsAttribute")
            {
                var intervalProp = attrType.GetProperty("Interval");
                if (intervalProp != null)
                {
                    var intervalMs = (long)intervalProp.GetValue(attr);
                    var intervalSeconds = intervalMs / 1000;
                    
                    // 将时间对齐到周期
                    var totalSeconds = (long)scheduledTime.TimeOfDay.TotalSeconds;
                    var windowSeconds = (totalSeconds / intervalSeconds) * intervalSeconds;
                    var windowTime = scheduledTime.Date.AddSeconds(windowSeconds);
                    
                    return windowTime.ToString("yyyyMMddHHmmss");
                }
            }
            // PeriodMinutes - 精确到分钟
            else if (attrType.Name == "PeriodMinutesAttribute")
            {
                var intervalProp = attrType.GetProperty("Interval");
                if (intervalProp != null)
                {
                    var intervalMs = (long)intervalProp.GetValue(attr);
                    var intervalMinutes = intervalMs / 60000;
                    
                    var totalMinutes = (long)(scheduledTime.TimeOfDay.TotalMinutes);
                    var windowMinutes = (totalMinutes / intervalMinutes) * intervalMinutes;
                    var windowTime = scheduledTime.Date.AddMinutes(windowMinutes);
                    
                    return windowTime.ToString("yyyyMMddHHmm");
                }
            }
            // Minutely - 精确到分钟
            else if (attrType.Name == "MinutelyAttribute")
            {
                return scheduledTime.ToString("yyyyMMddHHmm");
            }
            // Hourly - 精确到小时
            else if (attrType.Name == "HourlyAttribute")
            {
                return scheduledTime.ToString("yyyyMMddHH");
            }
            // Daily - 精确到天
            else if (attrType.Name == "DailyAttribute")
            {
                return scheduledTime.ToString("yyyyMMdd");
            }
            // Cron - 精确到分钟（默认）
            else if (attrType.Name == "CronAttribute")
            {
                return scheduledTime.ToString("yyyyMMddHHmm");
            }
        }

        // 默认精确到分钟
        return scheduledTime.ToString("yyyyMMddHHmm");
    }
}
