// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 枚举值合规性校验特性
/// </summary>
[SuppressSniffer]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum | AttributeTargets.Field)]
public class EnumAttribute : ValidationAttribute, ITransient
{
    /// <summary>
    /// 枚举值合规性校验特性
    /// </summary>
    /// <param name="errorMessage"></param>
    public EnumAttribute(string errorMessage = "枚举值不合法！")
    {
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 枚举值合规性校验
    /// </summary>
    /// <param name="value"></param>
    /// <param name="validationContext"></param>
    /// <returns></returns>
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        // 获取属性的类型
        var property = validationContext.ObjectType.GetProperty(validationContext.MemberName!);
        if (property == null) return new ValidationResult(string.Format("未知属性: {0}", validationContext.MemberName));

        // 如果是可空类型，则允许为null
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType);
        if (value == null && propertyType == null) return ValidationResult.Success;

        // 获取实际属性类型
        propertyType ??= property.PropertyType;

        // 检查属性类型是否为枚举
        if (!propertyType.IsEnum)
            return new ValidationResult($"'{validationContext.MemberName}'不是有效的枚举类型！");

        if (value != null && !Enum.IsDefined(propertyType, value))
            return new ValidationResult(ErrorMessage ?? $"'{value}'不是有效的'{validationContext.MemberName}'枚举值");

        return ValidationResult.Success;
    }
}