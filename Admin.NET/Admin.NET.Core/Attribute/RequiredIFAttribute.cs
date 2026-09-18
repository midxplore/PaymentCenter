// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 条件必填参数验证特性（支持多条件判断）
/// 当另一个属性的值满足指定条件时，验证当前属性是否非空。
/// </summary>
/// <code>
/// // 实例1
/// [RequiredIF(nameof(TarProperty), 1, ErrorMessage = "TarProperty为1时，SomeProperty不能为空")] <br/>
/// public string SomeProperty { get; set; } <br/>
/// </code>
/// <code>
/// // 实例2
/// [RequiredIF(nameof(TarProperty), 1, Operator.NotEqual, ErrorMessage = "TarProperty不为1时，SomeProperty不能为空")] <br/>
/// public string SomeProperty { get; set; } <br/>
/// </code>
/// <code>
/// // 实例3
/// [RequiredIF(nameof(TarProperty), new[]{ 1, 2 }, Operator.Contains, ErrorMessage = "TarProperty包含为1或2时，SomeProperty不能为空")] <br/>
/// public string SomeProperty { get; set; } <br/>
/// </code>
/// <code>
/// // 实例4
/// [RequiredIF(nameof(TarProperty), new[]{ 1, 2 }, Operator.NotContains, ErrorMessage = "TarProperty不包含1和2时，SomeProperty不能为空")] <br/>
/// public string SomeProperty { get; set; } <br/>
/// </code>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredIFAttribute(
    string propertyName,
    object targetValue = null,
    Operator comparison = Operator.Equal)
    : ValidationAttribute
{
    /// <summary>
    /// 依赖的属性名称
    /// </summary>
    private string PropertyName { get; set; } = propertyName;

    /// <summary>
    /// 目标比较值
    /// </summary>
    private object TargetValue { get; set; } = targetValue;

    /// <summary>
    /// 比较运算符
    /// </summary>
    private Operator Comparison { get; set; } = comparison;

    public RequiredIFAttribute(string propertyName, object[] targetValues, Operator comparison = Operator.Equal) : this(propertyName, targetValues as object, comparison)
    {
    }

    /// <summary>
    /// 验证属性值是否符合要求
    /// </summary>
    /// <param name="value">当前属性的值</param>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果</returns>
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        var instance = validationContext.ObjectInstance;
        var targetProperty = instance.GetType().GetProperty(PropertyName);
        // 判断校验字段内容是否为字典
        var dictAttr = targetProperty?.GetCustomAttribute<DictAttribute>();

        if (targetProperty == null) return new ValidationResult(string.Format("找不到属性: {0}", PropertyName));
        var targetValue = targetProperty.GetValue(instance);

        if (!ShouldValidate(targetValue, dictAttr)) return ValidationResult.Success;

        return IsEmpty(value) ? new ValidationResult(ErrorMessage ?? $"{validationContext.MemberName}不能为空") : ValidationResult.Success;
    }

    /// <summary>
    /// 判断是否需要进行验证
    /// </summary>
    /// <param name="targetValue"></param>
    /// <param name="dictAttr"></param>
    /// <returns></returns>
    private bool ShouldValidate(object targetValue, DictAttribute dictAttr)
    {
        switch (Comparison)
        {
            case Operator.Equal:
                return TargetValue == null ? IsEmpty(targetValue) : CompareValues(targetValue, TargetValue, Comparison);

            case Operator.NotEqual:
                return TargetValue == null ? !IsEmpty(targetValue) : CompareValues(targetValue, TargetValue, Comparison);

            case Operator.GreaterThan:
            case Operator.LessThan:
            case Operator.GreaterThanOrEqual:
            case Operator.LessThanOrEqual:
            case Operator.Contains:
            case Operator.NotContains:
                // 多选字典
                if (dictAttr != null && targetValue is string targetString && targetString.Contains(','))
                {
                    var values = targetString.Split(',')
                                            .Select(v => v.Trim())
                                            .Where(v => !string.IsNullOrEmpty(v))
                                            .ToArray();

                    return values.Any(value => CompareValues(value, TargetValue, Comparison));
                }
                // 处理其他集合情况
                else if (targetValue is IEnumerable enumerable && targetValue is not string)
                {
                    return enumerable.Cast<object>().Any(item => CompareValues(item, TargetValue, Comparison));
                }
                // 单选字典
                else
                {
                    return TargetValue == null ? !IsEmpty(targetValue) : CompareValues(targetValue, TargetValue, Comparison);
                }

            default:
                return false;
        }
    }

    /// <summary>
    /// 比较两个值
    /// </summary>
    /// <param name="sourceValue">源值</param>
    /// <param name="targetValue">目标值</param>
    /// <param name="comparison">比较运算符</param>
    /// <returns>比较结果</returns>
    private static bool CompareValues(object sourceValue, object targetValue, Operator comparison)
    {
        switch (comparison)
        {
            case Operator.Equal:
            case Operator.NotEqual:
            case Operator.GreaterThan:
            case Operator.LessThan:
            case Operator.GreaterThanOrEqual:
            case Operator.LessThanOrEqual:
                if (sourceValue is IComparable sourceComparable && targetValue is IComparable targetComparable)
                {
                    int result = sourceComparable.CompareTo(targetComparable);
                    return comparison switch
                    {
                        Operator.Equal => result == 0,
                        Operator.NotEqual => result != 0,
                        Operator.GreaterThan => result > 0,
                        Operator.LessThan => result < 0,
                        Operator.GreaterThanOrEqual => result >= 0,
                        Operator.LessThanOrEqual => result <= 0,
                        _ => false,
                    };
                }
                return false;

            case Operator.Contains:
            case Operator.NotContains:
                if (targetValue == null) return false;
                if (sourceValue == null) return comparison == Operator.NotContains;

                // 多选字典
                if (targetValue is string targetString)
                {
                    string sourceString = sourceValue.ToString();
                    bool stringContains = targetString.Equals(sourceString);
                    return comparison == Operator.Contains ? stringContains : !stringContains;
                }
                // 其他集合类型处理
                else if (targetValue is IEnumerable enumerable && targetValue is not string)
                {
                    bool contains = enumerable.OfType<object>().Any(item =>
                        item != null && item.Equals(sourceValue));
                    return comparison == Operator.Contains ? contains : !contains;
                }
                else
                {
                    bool valuesEqual = targetValue.Equals(sourceValue);
                    return comparison == Operator.Contains ? valuesEqual : !valuesEqual;
                }

            default:
                return false;
        }
    }

    /// <summary>
    /// 检查值是否为空
    /// </summary>
    private static bool IsEmpty(object? value)
    {
        return value switch
        {
            null => true,
            long l => l == 0,
            string s => string.IsNullOrWhiteSpace(s),
            IList list => list.Count == 0,
            _ => false
        };
    }
}

/// <summary>
/// 比较运算符枚举
/// </summary>
[SuppressSniffer]
[Description("比较运算符枚举")]
public enum Operator
{
    /// <summary>
    /// 等于 (==)
    /// </summary>
    [Description("等于")]
    Equal,

    /// <summary>
    /// 不等于 (!=)
    /// </summary>
    [Description("不等于")]
    NotEqual,

    /// <summary>
    /// 大于 (>)
    /// </summary>
    [Description("大于")]
    GreaterThan,

    /// <summary>
    /// 小于 (&lt;)
    /// </summary>
    [Description("小于")]
    LessThan,

    /// <summary>
    /// 大于等于 (>=)
    /// </summary>
    [Description("大于等于")]
    GreaterThanOrEqual,

    /// <summary>
    /// 小于等于 (&lt;=)
    /// </summary>
    [Description("小于等于")]
    LessThanOrEqual,

    /// <summary>
    /// 包含 (包含于集合)
    /// </summary>
    [Description("包含")]
    Contains,

    /// <summary>
    /// 不包含 (不包含于集合)
    /// </summary>
    [Description("不包含")]
    NotContains
}