// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 请求日志输出参数
/// </summary>
public class PageLogHttpOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 请求模块
    /// </summary>
    public string ActionName { get; set; }

    /// <summary>
    /// 客户端名称
    /// </summary>
    public string HttpClientName { get; set; }

    /// <summary>
    /// 请求接口描述
    /// </summary>
    public string HttpApiDesc { get; set; }

    /// <summary>
    /// 请求方式
    /// </summary>
    public string HttpMethod { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public YesNoEnum? IsSuccessStatusCode { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string RequestUrl { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public long? Elapsed { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreateTime { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    public string CreateUserName { get; set; }
}

/// <summary>
/// 请求日志数据导出模板实体
/// </summary>
public class ExportLogHttpOutput : ImportLogHttpInput
{
    [ImporterHeader(IsIgnore = true)]
    [ExporterHeader(IsIgnore = true)]
    public override string Error { get; set; }
}