// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成表单校验类型枚举
/// </summary>
[Description("代码生成表单校验类型枚举")]
public enum CodeGenFromRuleValidEnum
{
    /// <summary>
    /// 唯一性校验
    /// </summary>
    [Description("唯一性")]
    Unique = 100,

    /// <summary>
    /// 字符串长度校验
    /// </summary>
    [Description("字符串长度")]
    MaxLength,

    /// <summary>
    /// 数字范围
    /// </summary>
    [Description("数字范围")]
    Range,

    /// <summary>
    /// 身份证号码
    /// </summary>
    [Description("身份证号码")]
    IDCard,

    /// <summary>
    /// 邮政编码
    /// </summary>
    [Description("邮政编码")]
    PostCode,

    /// <summary>
    /// 手机号码
    /// </summary>
    [Description("手机号码")]
    PhoneNumber,

    /// <summary>
    /// 固话格式
    /// </summary>
    [Description("固话格式")]
    Telephone,

    /// <summary>
    /// 手机或固话类型
    /// </summary>
    [Description("手机或固话类型")]
    PhoneOrTelNumber,

    /// <summary>
    /// 邮箱
    /// </summary>
    [Description("邮箱")]
    EmailAddress,

    /// <summary>
    /// 统一社会信用代码
    /// </summary>
    [Description("统一社会信用代码")]
    SocialCreditCode,

    /// <summary>
    /// 网址类型
    /// </summary>
    [Description("网址类型")]
    Url,

    /// <summary>
    /// 中文
    /// </summary>
    [Description("中文")]
    Chinese,

    /// <summary>
    /// 中文名
    /// </summary>
    [Description("中文名")]
    ChineseName,

    /// <summary>
    /// 英文名
    /// </summary>
    [Description("英文名")]
    EnglishName,

    /// <summary>
    /// 纯大写
    /// </summary>
    [Description("纯大写")]
    Capital,

    /// <summary>
    /// 纯小写
    /// </summary>
    [Description("纯小写")]
    Lowercase,

    /// <summary>
    /// 字母和数字组合
    /// </summary>
    [Description("字母和数字组合")]
    WordWithNumber,

    /// <summary>
    /// Html 标签格式
    /// </summary>
    [Description("Html 标签格式")]
    Html,

    /// <summary>
    /// GUID 或者 UUID
    /// </summary>
    [Description("GUID 或者 UUID")]
    GUID_OR_UUID,

    /// <summary>
    /// 用户名
    /// </summary>
    [Description("用户名")]
    Username,

    /// <summary>
    /// 日期类型
    /// </summary>
    [Description("日期类型")]
    Date,

    /// <summary>
    /// 时间类型
    /// </summary>
    [Description("时间类型")]
    Time,

    /// <summary>
    /// 年龄
    /// </summary>
    [Description("年龄")]
    Age,
}