// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 远程请求接口特性
/// </summary>
[SuppressSniffer]
[AttributeUsage(AttributeTargets.Class)]
public class HttpRemoteApiAttribute : Attribute
{
    /// <summary>
    /// 业务编码 / 方法名
    /// </summary>
    [Required]
    public object Action { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    [Required]
    public string Desc { get; set; }

    /// <summary>
    /// 请求方式
    /// </summary>
    [Required]
    public HttpMethodEnum HttpMethod { get; set; } = HttpMethodEnum.Post;

    /// <summary>
    /// 参数类型
    /// </summary>
    public HttpParameterTypeEnum? Type { get; set; }

    /// <summary>
    /// 忽略记录日志
    /// </summary>
    public bool IgnoreLog { get; set; }
}

/// <summary>
/// 远程请求接口参数类型枚举
/// </summary>
[SuppressSniffer]
[Description("远程请求接口参数类型枚举")]
public enum HttpParameterTypeEnum
{
    [Description("查询")]
    Query = 1,

    [Description("表单")]
    FormData = 2,

    [Description("JSON")]
    Json = 3,

    [Description("XML")]
    Xml = 4,

    [Description("自定义")]
    Custom = 5
}