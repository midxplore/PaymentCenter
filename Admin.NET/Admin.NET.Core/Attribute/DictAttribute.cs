// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 字典值合规性校验特性
/// </summary>
[SuppressSniffer]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class DictAttribute : ValidationAttribute, ITransient
{
    /// <summary>
    /// 字典编码
    /// </summary>
    public readonly string DictTypeCode;

    /// <summary>
    /// 是否多选
    /// </summary>
    public readonly bool Multiple;

    /// <summary>
    /// 是否允许空字符串
    /// </summary>
    [Obsolete("参数已废弃，逻辑已删除，请使用[Required]特性校验必填")]
    public bool AllowEmptyStrings { get; set; } = false;

    /// <summary>
    /// 允许空值，有值才验证，默认 false
    /// </summary>
    [Obsolete("参数已废弃，逻辑已删除，请使用[Required]特性校验必填")]
    public bool AllowNullValue { get; set; } = false;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="dictTypeCode">字典类型编码</param>
    /// <param name="multiple">是否允许多选</param>
    /// <param name="errorMessage">自定义错误消息</param>
    public DictAttribute(string dictTypeCode = "", bool multiple = false, string errorMessage = "字典值不合法！")
    {
        DictTypeCode = dictTypeCode;
        Multiple = multiple;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 验证方法（异步安全）
    /// </summary>
    /// <param name="value"></param>
    /// <param name="validationContext"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    private async Task<ValidationResult> IsValidAsync(object? value, ValidationContext validationContext, CancellationToken cancellation)
    {
        // 1. 空值检查
        if (IsEmpty(value)) return ValidationResult.Success;

        // 2. 获取属性类型
        var property = validationContext.ObjectType.GetProperty(validationContext.MemberName!);
        if (property == null)
            return new ValidationResult(string.Format("未知属性: {0}", validationContext.MemberName));

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        // 3. 枚举类型校验
        if (propertyType.IsEnum)
        {
            return !Enum.IsDefined(propertyType, value!) ? new ValidationResult(string.Format("提示：{0}|枚举值【{1}】不是有效的【{2}】枚举类型值！", ErrorMessage, value, propertyType.Name)) : ValidationResult.Success;
        }

        // 4. 获取字典服务
        var sysDictDataService = validationContext.GetService(typeof(SysDictDataService)) as SysDictDataService
                                 ?? App.GetRequiredService<SysDictDataService>();

        // 5. 异步获取字典数据
        var dictDataList = await sysDictDataService.GetDataList(DictTypeCode);
        var dictHashSet = new HashSet<string>(dictDataList.Select(d => d.Value), StringComparer.OrdinalIgnoreCase);

        // 6. 单选校验
        if (!Multiple)
        {
            var valStr = value!.GetType().IsEnum ? value.ToInt().ToString() : value.ToString();
            if (string.IsNullOrEmpty(valStr) || !dictHashSet.Contains(valStr))
            {
                return new ValidationResult(string.Format("提示：{0}|字典【{1}】不包含【{2}】！", ErrorMessage, DictTypeCode, value));
            }
            return ValidationResult.Success;
        }

        // 7. 多选校验
        if (value is string stringValue)
        {
            var codes = stringValue.Split(',');
            foreach (var code in codes)
            {
                if (!dictHashSet.Contains(code))
                {
                    return new ValidationResult(string.Format("提示：{0}|字典【{1}】不包含【{2}】！", ErrorMessage, DictTypeCode, code));
                }
            }
            return ValidationResult.Success;
        }

        // 集合类数据校验
        if (value is IList listValue)
        {
            foreach (var item in listValue)
            {
                var itemStr = item.GetType().IsEnum ? item.ToInt().ToString() : item.ToString();
                if (string.IsNullOrEmpty(itemStr) || !dictHashSet.Contains(itemStr))
                {
                    return new ValidationResult(string.Format("提示：{0}|字典【{1}】不包含【{2}】！", ErrorMessage, DictTypeCode, item));
                }
            }
            return ValidationResult.Success;
        }

        // 8. 类型不匹配
        return new ValidationResult(string.Format("提示：{0}|不支持的值类型：{1}，多选时应为逗号分隔字符串或集合类型。", ErrorMessage, value?.GetType().Name));
    }

    /// <summary>
    /// 同步入口（兼容旧版调用）
    /// </summary>
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        return IsValidAsync(value, validationContext, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 判断值是否为空（null、空字符串、空集合）
    /// </summary>
    private static bool IsEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            IList list => list.Count == 0,
            _ => false
        };
    }
}