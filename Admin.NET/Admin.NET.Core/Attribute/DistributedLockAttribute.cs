namespace Admin.NET.Core;

/// <summary>
/// 分布式锁特性 - 标记在Job类上，自动处理分布式互斥
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class DistributedLockAttribute :Attribute, ITransient
{
    
    /// <summary>
    /// 锁的键（为空则自动使用 lock:job:exec:{JobId}）
    /// </summary>
    public string? LockKey { get; set; }

    /// <summary>
    /// 锁持有时间（秒）
    /// 如果设置为 0 或负数，则自动根据触发器周期计算
    /// </summary>
    public int LockHoldSeconds { get; set; } = 0; // 默认0表示自动计算

    /// <summary>
    /// 锁持有时间的安全系数（默认1.2倍周期时间）
    /// 例如：5秒周期 × 1.2 = 6秒锁持有时间
    /// </summary>
    public double LockHoldMultiplier { get; set; } = 1;
}