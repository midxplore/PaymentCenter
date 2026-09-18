// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 用于校验 List&lt;DateTime&gt; 时间范围参数的自定义验证特性
/// 要求列表包含两个有效 DateTime 值，构成一个时间范围
/// </summary>
/// <example>
/// <para><b>示例 1：基础时间范围（起始 &lt; 结束）</b></para>
/// <code>
/// public class QueryModel
/// {
///     [DateTimeRange]
///     public List&lt;DateTime&gt; TimeRange { get; set; }
/// }
/// // 合法值: [2025-01-01 00:00:00, 2025-01-07 23:59:59]
/// </code>
///
/// <para><b>示例 2：仅允许日期（时间部分必须为 00:00:00）</b></para>
/// <code>
/// [DateTimeRange(DateOnly = true)]
/// public List&lt;DateTime&gt; DateRange { get; set; }
/// // 合法值: [2025-01-01 00:00:00, 2025-01-31 00:00:00]
/// // 非法值: [2025-01-01 08:00:00, ...] → 报错
/// </code>
///
/// <para><b>示例 3：最小间隔 1 小时（60分钟）</b></para>
/// <code>
/// [DateTimeRange(MinInterval = 60)]
/// public List&lt;DateTime&gt; Duration { get; set; }
/// // 合法值: 间隔 ≥ 1小时（如 2小时 → "2小时"）
/// // 错误提示: 时间间隔必须至少为 1小时
/// </code>
///
/// <para><b>示例 4：最大间隔 7 天</b></para>
/// <code>
/// [DateTimeRange(MaxInterval = 7 * 24 * 60)] // 7天 = 10080分钟
/// public List&lt;DateTime&gt; WeeklyRange { get; set; }
/// // 合法值: 间隔 ≤ 7天
/// // 错误提示: 时间间隔不能超过 7天
/// </code>
///
/// <para><b>示例 5：时间范围必须在过去（且可包含当前时间）</b></para>
/// <code>
/// [DateTimeRange(DirectionEnum = TimeDirectionEnum.Past, AllowNow = true)]
/// public List&lt;DateTime&gt; PastEvents { get; set; }
/// // 合法值: [2025-08-01, 2025-08-20]（若今天是 2025-08-20）
/// // 非法值: [2025-08-21, ...] → 完全在未来
/// </code>
///
/// <para><b>示例 6：时间范围必须在未来（不能包含当前时间）</b></para>
/// <code>
/// [DateTimeRange(DirectionEnum = TimeDirectionEnum.Future, AllowNow = false)]
/// public List&lt;DateTime&gt; FutureTasks { get; set; }
/// // 合法值: [2025-08-21 00:00, 2025-08-30 23:59]
/// // 非法值: [2025-08-20 10:00, ...] → 包含当前时间
/// </code>
///
/// <para><b>示例 7：允许起止时间相等（单日查询）</b></para>
/// <code>
/// [DateTimeRange(AllowEqual = true, DateOnly = true)]
/// public List&lt;DateTime&gt; SingleDay { get; set; }
/// // 合法值: [2025-08-20 00:00:00, 2025-08-20 00:00:00]
/// </code>
///
/// <para><b>示例 8：单元测试中固定“当前时间”</b></para>
/// <code>
/// // 用于测试未来/过去逻辑
/// var attribute = new DateTimeRangeAttribute
/// {
///     DirectionEnum = TimeDirectionEnum.Future,
///     NowProvider = () => new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
/// };
/// </code>
/// </example>
[SuppressSniffer]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class DateTimeRangeAttribute : ValidationAttribute
{
    /// <summary>
    /// 获取或设置时间方向约束
    /// </summary>
    public TimeDirectionEnum DirectionEnum { get; set; } = TimeDirectionEnum.None;

    /// <summary>
    /// 最小时间间隔（分钟） 0表示无限制
    /// </summary>
    public int MinInterval { get; set; } = 0;

    /// <summary>
    /// 最大时间间隔（分钟） 0表示无限制
    /// </summary>
    public int MaxInterval { get; set; } = 0;

    /// <summary>
    /// 是否仅允许日期类型（时间部分必须为 00:00:00）
    /// </summary>
    public bool DateOnly { get; set; } = false;

    /// <summary>
    /// 是否允许起始时间等于结束时间（即零间隔）
    /// 默认为 false，即起始时间必须小于结束时间
    /// </summary>
    public bool AllowEqual { get; set; } = false;

    /// <summary>
    /// 是否允许范围包含当前时间
    /// 当 Direction 为 Future 或 Past 时，此属性可覆盖默认行为
    /// </summary>
    public bool AllowNow { get; set; } = true;

    /// <summary>
    /// 获取或设置自定义的“现在”时间提供器，用于单元测试或特定时区场景
    /// 默认为 DateTimeOffset.Now
    /// </summary>
    public Func<DateTimeOffset> NowProvider { get; set; } = () => DateTimeOffset.Now;

    /// <summary>
    /// 获取格式化的当前时间字符串（用于错误消息）
    /// </summary>
    private string NowString => NowProvider().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 执行验证逻辑
    /// </summary>
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null) return ValidationResult.Success;

        var list = value as List<DateTime>;
        if (list == null) return new ValidationResult($"'{validationContext.DisplayName}' 必须是日期时间列表类型");

        if (list.Count != 2) return new ValidationResult($"'{validationContext.DisplayName}' 必须包含两个日期时间值");

        var start = list[0];
        var end = list[1];

        // 验证 DateTime 值的有效性
        if (!IsValidDateTime(start) || !IsValidDateTime(end)) return new ValidationResult($"'{validationContext.DisplayName}' 包含无效的日期时间值");

        // 检查 DateOnly 约束
        if (DateOnly)
        {
            if (start.TimeOfDay != TimeSpan.Zero) return new ValidationResult($"'{validationContext.DisplayName}' 起始时间需为日期格式（00:00:00），当前为 {start:yyyy-MM-dd HH:mm:ss}");

            if (end.TimeOfDay != TimeSpan.Zero) return new ValidationResult($"'{validationContext.DisplayName}' 结束时间需为日期格式（00:00:00），当前为 {end:yyyy-MM-dd HH:mm:ss}");
        }

        // 检查时间顺序
        if (AllowEqual)
        {
            if (start > end) return new ValidationResult($"'{validationContext.DisplayName}' 起始时间不能晚于结束时间");
        }
        else
        {
            if (start >= end) return new ValidationResult($"'{validationContext.DisplayName}' 起始时间必须早于结束时间");
        }

        // 检查时间方向
        var now = NowProvider().DateTime;
        var directionResult = ValidateDirection(start, end, now, validationContext.DisplayName);
        if (directionResult != null) return directionResult;

        // 检查最小和最大间隔
        var intervalResult = ValidateTimeIntervals(start, end, validationContext.DisplayName);
        if (intervalResult != null) return intervalResult;

        return ValidationResult.Success;
    }

    /// <summary>
    /// 验证时间方向约束
    /// </summary>
    private ValidationResult? ValidateDirection(DateTime start, DateTime end, DateTime now, string? displayName)
    {
        return DirectionEnum switch
        {
            TimeDirectionEnum.Future when !AllowNow && (start <= now || end <= now) =>
                new ValidationResult($"'{displayName}' 必须在 {NowString} 之后"),

            TimeDirectionEnum.Future when AllowNow && (start < now && end < now) =>
                new ValidationResult($"'{displayName}' 必须是未来时间"),

            TimeDirectionEnum.Past when !AllowNow && (start >= now || end >= now) =>
                new ValidationResult($"'{displayName}' 必须在 {NowString} 之前"),

            TimeDirectionEnum.Past when AllowNow && (start > now && end > now) =>
                new ValidationResult($"'{displayName}' 必须是过去时间"),

            _ => null
        };
    }

    /// <summary>
    /// 验证最小和最大时间间隔
    /// </summary>
    private ValidationResult? ValidateTimeIntervals(DateTime start, DateTime end, string? displayName)
    {
        var interval = end - start;
        var totalMinutes = (int)interval.TotalMinutes;

        // 验证最小间隔
        if (MinInterval > 0)
        {
            if (MinInterval < 0) return new ValidationResult($"'{displayName}' 最小间隔不能为负数");

            if (totalMinutes < MinInterval)
            {
                var required = TimeSpan.FromMinutes(MinInterval);
                var actual = TimeSpan.FromMinutes(totalMinutes);
                return new ValidationResult($"'{displayName}'至少间隔 {required.FormatTimeSpanText(2)}");
            }
        }

        // 验证最大间隔
        if (MaxInterval > 0)
        {
            if (MaxInterval < 0) return new ValidationResult($"'{displayName}'最大间隔不能为负数");

            if (totalMinutes > MaxInterval)
            {
                var allowed = TimeSpan.FromMinutes(MaxInterval);
                var actual = TimeSpan.FromMinutes(totalMinutes);
                return new ValidationResult($"'{displayName}'最多间隔 {allowed.FormatTimeSpanText(2)}");
            }
        }

        // 验证最小不能大于最大
        if (MinInterval > 0 && MaxInterval > 0 && MinInterval > MaxInterval)
        {
            var minSpan = TimeSpan.FromMinutes(MinInterval);
            var maxSpan = TimeSpan.FromMinutes(MaxInterval);
            return new ValidationResult($"'{displayName}' 最小间隔({minSpan.FormatTimeSpanText(2)})不能大于最大间隔({maxSpan.FormatTimeSpanText(2)})");
        }

        return null;
    }

    /// <summary>
    /// 验证 DateTime 值是否在合理范围内
    /// </summary>
    private static bool IsValidDateTime(DateTime dt)
    {
        return dt >= new DateTime(1900, 1, 1) && dt <= new DateTime(2100, 12, 31);
    }
}

/// <summary>
/// 指定范围必须是未来时间、过去时间，还是不限制
/// </summary>
[SuppressSniffer]
[Description("时间方向枚举")]
public enum TimeDirectionEnum
{
    /// <summary>
    /// 不限制时间方向
    /// </summary>
    [Description("不限")]
    None,

    /// <summary>
    /// 范围必须在当前时间之后（未来）
    /// </summary>
    [Description("未来")]
    Future,

    /// <summary>
    /// 范围必须在当前时间之前（过去）
    /// </summary>
    [Description("过去")]
    Past
}