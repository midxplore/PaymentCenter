// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成详细配置参数
/// </summary>
public class CodeGenColumnConfig : SysCodeGenColumn
{
    /// <summary>
    /// 首字母小写的属性名
    /// </summary>
    public string LowerPropertyName => PropertyName[..1].ToLower() + PropertyName[1..];

    /// <summary>
    /// 是否字典字段
    /// </summary>
    public bool IsDict => EffectType is CodeGenEffectTypeEnum.DictSelector;

    /// <summary>
    /// 是否枚举字段
    /// </summary>
    public bool IsEnum => EffectType is CodeGenEffectTypeEnum.EnumSelector;

    /// <summary>
    /// 是否常量字段
    /// </summary>
    public bool IsConst => EffectType is CodeGenEffectTypeEnum.ConstSelector;

    /// <summary>
    /// 是否上传字段
    /// </summary>
    public bool IsUpload => EffectType is CodeGenEffectTypeEnum.Upload;

    /// <summary>
    /// 是否开关字段
    /// </summary>
    public bool IsSwitch => EffectType is CodeGenEffectTypeEnum.Switch;

    /// <summary>
    /// 是否输入框字段
    /// </summary>
    public bool IsInput => EffectType is CodeGenEffectTypeEnum.Input;

    /// <summary>
    /// 是否数字输入框字段
    /// </summary>
    public bool IsInputNumber => EffectType is CodeGenEffectTypeEnum.InputNumber;

    /// <summary>
    /// 是否文本域字段
    /// </summary>
    public bool IsInputTextArea => EffectType is CodeGenEffectTypeEnum.InputTextArea;

    /// <summary>
    /// 是否时间选择器字段
    /// </summary>
    public bool IsDatePicker => EffectType is CodeGenEffectTypeEnum.DatePicker;

    /// <summary>
    /// 是否外键字段
    /// </summary>
    public bool IsForeignKey => EffectType is CodeGenEffectTypeEnum.ForeignKey;

    /// <summary>
    /// 是否树字段
    /// </summary>
    public bool IsTree => EffectType is CodeGenEffectTypeEnum.ApiTreeSelector;

    /// <summary>
    /// 是否多选字段
    /// </summary>
    public bool Multiple => JoinConfig?.Multiple ?? false;

    /// <summary>
    /// 是否唯一字段
    /// </summary>
    public bool IsUnique => FromValid == CodeGenFromRuleValidEnum.Unique;

    /// <summary>
    /// 状态字段
    /// </summary>
    public bool IsStatus => PropertyName == nameof(BaseStatusInput.Status) && DictConfig?.Code?.Trim('?') == nameof(StatusEnum);

    /// <summary>
    /// 是否要联表
    /// </summary>
    public bool HasJoinTable => EffectType is CodeGenEffectTypeEnum.Upload or CodeGenEffectTypeEnum.ForeignKey or CodeGenEffectTypeEnum.ApiTreeSelector;

    /// <summary>
    /// 是否要插槽
    /// </summary>
    public bool HasSlots => !(IsConst || IsInput || IsInputNumber || IsInputTextArea) || IsCopy;

    /// <summary>
    /// 连表别名
    /// </summary>
    public string LeftJoinAliasName => Regex.Replace(LowerPropertyName, "Id$", "");

    /// <summary>
    /// 字典配置
    /// </summary>
    public EffectFileConfigInput FileConfig => !string.IsNullOrWhiteSpace(Config) ? JSON.Deserialize<EffectFileConfigInput>(Config) : null;

    /// <summary>
    /// 字典配置
    /// </summary>
    public EffectDictConfigInput DictConfig => !string.IsNullOrWhiteSpace(Config) ? JSON.Deserialize<EffectDictConfigInput>(Config) : null;

    /// <summary>
    /// 联表配置
    /// </summary>
    public EffectTreeConfigInput JoinConfig => !string.IsNullOrWhiteSpace(Config) ? JSON.Deserialize<EffectTreeConfigInput>(Config) : null;

    /// <summary>
    /// 时间控件配置
    /// </summary>
    public EffectDatePickerConfigInput DateConfig => !string.IsNullOrWhiteSpace(Config) ? JSON.Deserialize<EffectDatePickerConfigInput>(Config) : null;

    /// <summary>
    /// 获取可空类型
    /// </summary>
    public string NullableNetType
    {
        get
        {
            if (IsEnum) return DictConfig.Multiple || NetType.StartsWith("string") ? "string" : $"{DictConfig.Code}?";
            if (NetType.StartsWith("string")) return "string";
            if (NetType.StartsWith("Guid")) return "Guid";
            return NetType.EndsWith("?") ? NetType : NetType + "?";
        }
    }

    /// <summary>
    /// 获取字段验证特性
    /// </summary>
    /// <returns></returns>
    public string GetFromValidAttribute()
    {
        bool isCustomValid = FromValid is CodeGenFromRuleValidEnum.IDCard or CodeGenFromRuleValidEnum.Range or CodeGenFromRuleValidEnum.MaxLength or CodeGenFromRuleValidEnum.Unique;
        if (isCustomValid)
        {
            switch (FromValid)
            {
                case CodeGenFromRuleValidEnum.Unique: return null;
                case CodeGenFromRuleValidEnum.IDCard:
                    if (!NetType.StartsWith("string")) return null;
                    return "[IdCardNo]";

                case CodeGenFromRuleValidEnum.MaxLength:
                    if (!NetType.StartsWith("string") || ColumnLength <= 0) return null;
                    return $"[MaxLength({ColumnLength}, ErrorMessage = \"{ColumnComment}不能超过{ColumnLength}个字符\")]";

                case CodeGenFromRuleValidEnum.Range:
                    return $"[Range(0, 100, ErrorMessage = \"{ColumnComment}范围不正确\")]";

                default:
                    return null;
            }
        }
        return $"[DataValidation(ValidationTypes.{FromValid.ToString()}, ErrorMessage = \"{ColumnComment}不正确\")]";
    }

    /// <summary>
    /// 显示字段
    /// </summary>
    public string DisplayPropertyNames
    {
        get
        {
            var expList = JoinConfig?.DisplayPropertyNames?.Split(",").Select(u => $"{LeftJoinAliasName}.{u}");
            if (expList?.Count() == 1) return expList.FirstOrDefault();
            if (expList == null) return null;
            return $"$\"{{{expList.Join("}-{")}}}\"";
        }
    }

    /// <summary>
    /// 连表属性名称
    /// </summary>
    public string LeftJoinPropertyName
    {
        get
        {
            if (!HasJoinTable) throw Oops.Bah("不是连表字段");
            return EffectType switch
            {
                CodeGenEffectTypeEnum.ForeignKey or CodeGenEffectTypeEnum.ApiTreeSelector => Regex.Replace(PropertyName, "Id([s]?)$", "$1") + JoinConfig?.DisplayPropertyNames?.Split(",").FirstOrDefault(),
                CodeGenEffectTypeEnum.Upload => Regex.Replace(PropertyName, "Id([s]?)$", "$1") + "Attachment",
                _ => null
            };
        }
    }

    /// <summary>
    /// 获取列格式化配置方法
    /// </summary>
    public string GetVxeColumnExtraConfig
    {
        get
        {
            if (!HasSlots) return "";
            if (IsCopy) return ", slots: { default: '$row_copy' }"; // 复制字段
            if (IsStatistical) return ", slots: { default: '$row_statistical' }"; // 统计字段
            switch (EffectType)
            {
                case CodeGenEffectTypeEnum.Switch: // 开关
                    return ", slots: { default: '$row_switch' }";

                case CodeGenEffectTypeEnum.ApiTreeSelector: // 树组件
                case CodeGenEffectTypeEnum.ForeignKey: // 外键
                    return $", formatter: ({{ row }}) => row.{LeftJoinPropertyName.ToFirstLetterLowerCase()}";

                case CodeGenEffectTypeEnum.DatePicker: // 时间选择器
                    return $", formatter: ({{ cellValue, row }}) => commonFun.dateFormat{GetDateReaderName(DateConfig.Format)}([], 0, cellValue)";

                case CodeGenEffectTypeEnum.Upload: // 上传
                    return $", params: {{ attachmentName: '{LeftJoinAliasName.ToFirstLetterLowerCase()}', isImage: {FileConfig.IsImage.ToString().ToLower()}, useDownload: {FileConfig.UseDownload.ToString().ToLower()}, downloadText: '{FileConfig.DownloadText}' }}" +
                           ", slots: { default: '$row_upload' }";

                case CodeGenEffectTypeEnum.ConstSelector: // 常量选择器
                case CodeGenEffectTypeEnum.DictSelector: // 字典选择器
                case CodeGenEffectTypeEnum.EnumSelector: // 枚举选择器
                    if (IsStatus) return ", slots: { default: '$row_status' }"; // 状态字段
                    return $", params: {{ code: '{DictConfig.Code}', multiple: {DictConfig.Multiple.ToString().ToLower()}, isConst: {IsConst.ToString().ToLower()} }}" +
                           ", slots: { default: '$row_sysDict' }";
            }
            return "";
            string GetDateReaderName(string format) => format switch
            {
                "date" => "YMD",
                "time" => "HMS",
                _ => "YMDHMS"
            };
        }
    }

    /// <summary>
    /// 获取表单验证规则
    /// </summary>
    public string GetFormRules
    {
        get
        {
            var result = "";
            var trigger = IsDatePicker || IsDict || IsEnum || IsTree ? "change" : "blur";
            if (IsRequired) result += $" :rules=\"[{{required: true, message: '请选择{ColumnComment}', trigger: '{trigger}'}}]\"";
            return result;
        }
    }

    /// <summary>
    /// 获取外键显示值语句
    /// </summary>
    /// <param name="tableAlias">表别名</param>
    /// <param name="separator">多字段时的连接符</param>
    /// <returns></returns>
    public string GetDisplayColumn(string tableAlias, string separator = "-") => "$\"" + string.Join(separator, DisplayPropertyNames?.Split(",").Select(name => $"{{{tableAlias}.{name}}}") ?? new List<string>()) + "\"";
}