// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 外键控件配置信息类
/// </summary>
public class EffectForeignKeyConfigInput
{
    /// <summary>
    /// 外键库标识
    /// </summary>
    [Required(ErrorMessage = "外键库标识不能为空")]
    public virtual string ConfigId { get; set; }

    /// <summary>
    /// 外键实体名称
    /// </summary>
    [Required(ErrorMessage = "外键实体名称不能为空")]
    public virtual string EntityName { get; set; }

    /// <summary>
    /// 首字母小写实体名称
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string LowerEntityName => EntityName?[..1].ToLower() + EntityName?[1..];

    /// <summary>
    /// 外键表名称
    /// </summary>
    [Required(ErrorMessage = "外键表名称不能为空")]
    public virtual string TableName { get; set; }

    /// <summary>
    /// 表注释
    /// </summary>
    [Required(ErrorMessage = "表注释不能为空")]
    public virtual string TableComment { get; set; }

    /// <summary>
    /// 外键显示属性名（多选）
    /// </summary>
    [Required(ErrorMessage = "外键显示属性名不能为空")]
    public virtual string DisplayPropertyNames { get; set; }

    /// <summary>
    /// 首位外键显示属性名
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string FirstDisplayName => DisplayPropertyNames?.Split(",").First();

    /// <summary>
    /// 首字母小写首位外键显示属性名
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string LowerFirstDisplayName
    {
        get
        {
            var displayName = FirstDisplayName;
            return displayName?[..1].ToLower() + displayName?[1..];
        }
    }

    /// <summary>
    /// 外键属性名
    /// </summary>
    [Required(ErrorMessage = "外键属性名不能为空")]
    public virtual string LinkPropertyName { get; set; }

    /// <summary>
    /// 首字母小写外键属性名
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string LowerLinkPropertyName => LinkPropertyName?[..1].ToLower() + LinkPropertyName?[1..];

    /// <summary>
    /// 外键显示属性.NET类型
    /// </summary>
    [Required(ErrorMessage = "外键显示属性.NET类型")]
    public virtual string LinkPropertyType { get; set; }

    /// <summary>
    /// 用于检索的属性名
    /// </summary>
    public virtual string SearchPropertyName { get; set; }

    /// <summary>
    /// 首字母小写外键属性名
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public virtual string LowerSearchPropertyName => SearchPropertyName?[..1].ToLower() + SearchPropertyName?[1..];

    /// <summary>
    /// 用于检索的属性.NET类型
    /// </summary>
    public virtual string SearchPropertyType { get; set; }

    /// <summary>
    /// 是否使用表格
    /// </summary>
    public virtual bool UseTable { get; set; }

    /// <summary>
    /// 是否多选
    /// </summary>
    public virtual bool Multiple { get; set; }
}

/// <summary>
/// 树控件配置信息类
/// </summary>
public class EffectTreeConfigInput : EffectForeignKeyConfigInput
{
    /// <summary>
    /// 树组件标题不能为空
    /// </summary>
    public virtual string TreeTitle { get; set; }

    /// <summary>
    /// 父属性名称
    /// </summary>
    public virtual string ParentPropertyName { get; set; }

    /// <summary>
    /// 父属性.NET类型
    /// </summary>
    public virtual string ParentPropertyType { get; set; }
}

/// <summary>
/// 字典控件配置信息类
/// </summary>
public class EffectDictConfigInput
{
    /// <summary>
    /// 字典编码
    /// </summary>
    [Required(ErrorMessage = "字典编码不能为空")]
    public virtual string Code { get; set; }

    /// <summary>
    /// 是否多选
    /// </summary>
    public virtual bool Multiple { get; set; }
}

/// <summary>
/// 文件控件配置信息类
/// </summary>
public class EffectFileConfigInput
{
    /// <summary>
    /// 链接预览
    /// </summary>
    public virtual bool UseDownload { get; set; }

    /// <summary>
    /// 链接文本
    /// </summary>
    public virtual string DownloadText { get; set; }

    /// <summary>
    /// 是否图片
    /// </summary>
    public virtual bool IsImage { get; set; }
}

/// <summary>
/// 时间控件配置信息类
/// </summary>
public class EffectDatePickerConfigInput
{
    /// <summary>
    /// 格式
    /// </summary>
    [Required(ErrorMessage = "时间控件格式不能为空")]
    public virtual string Format { get; set; } = "datetime";

    /// <summary>
    /// 默认值
    /// </summary>
    public virtual string Default { get; set; }
}