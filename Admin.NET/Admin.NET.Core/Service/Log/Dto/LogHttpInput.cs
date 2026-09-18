// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 请求日志基础输入参数
/// </summary>
public class LogHttpBaseInput
{
    /// <summary>
    /// 请求方式
    /// </summary>
    public virtual string HttpMethod { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public virtual YesNoEnum? IsSuccessStatusCode { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public virtual string RequestUrl { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public virtual string RequestHeaders { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    public virtual string RequestBody { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    public virtual int? StatusCode { get; set; }

    /// <summary>
    /// 响应头
    /// </summary>
    public virtual string ResponseHeaders { get; set; }

    /// <summary>
    /// 响应体
    /// </summary>
    public virtual string ResponseBody { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public virtual string Exception { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public virtual DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public virtual DateTime? EndTime { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public virtual long? Elapsed { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreateTime { get; set; }
}

/// <summary>
/// 请求日志分页查询输入参数
/// </summary>
public class PageLogHttpInput : BasePageInput
{
    /// <summary>
    /// 关键字查询
    /// </summary>
    public string SearchKey { get; set; }

    /// <summary>
    /// 模块名称
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
    [Dict(nameof(YesNoEnum))]
    public YesNoEnum? IsSuccessStatusCode { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    public string RequestUrl { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    public string RequestBody { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// 响应体
    /// </summary>
    public string ResponseBody { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreateTime { get; set; }

    /// <summary>
    /// 创建时间范围
    /// </summary>
    public DateTime?[] CreateTimeRange { get; set; }
}

/// <summary>
/// 请求日志主键查询输入参数
/// </summary>
public class ExportLogHttpInput : PageLogHttpInput
{
    /// <summary>
    /// 需要导入的主键集
    /// </summary>
    [ImporterHeader(IsIgnore = true)]
    [ExporterHeader(IsIgnore = true)]
    public List<long> SelectKeyList { get; set; }
}

/// <summary>
/// 请求日志数据导入实体
/// </summary>
[ExcelImporter(SheetIndex = 1, IsOnlyErrorRows = true)]
public class ImportLogHttpInput : BaseImportInput
{
    /// <summary>
    /// 请求模块
    /// </summary>
    [ImporterHeader(Name = "请求模块")]
    [ExporterHeader("请求模块", Format = "", Width = 25, IsBold = true)]
    public string ActionName { get; set; }

    /// <summary>
    /// 客户端名称
    /// </summary>
    [ImporterHeader(Name = "客户端名称")]
    [ExporterHeader("客户端名称", Format = "", Width = 25, IsBold = true)]
    public string HttpClientName { get; set; }

    /// <summary>
    /// 接口描述
    /// </summary>
    [ImporterHeader(Name = "接口描述")]
    [ExporterHeader("接口描述", Format = "", Width = 25, IsBold = true)]
    public string HttpApiDesc { get; set; }

    /// <summary>
    /// 请求方式
    /// </summary>
    [ImporterHeader(Name = "请求方式")]
    [ExporterHeader("请求方式", Format = "", Width = 25, IsBold = true)]
    public string HttpMethod { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [Dict(nameof(YesNoEnum))]
    [ImporterHeader(Name = "是否成功")]
    [ExporterHeader("是否成功", Format = "", Width = 25, IsBold = true)]
    public YesNoEnum? IsSuccessStatusCode { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    [ImporterHeader(Name = "请求地址")]
    [ExporterHeader("请求地址", Format = "", Width = 25, IsBold = true)]
    public string RequestUrl { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    [ImporterHeader(Name = "请求头")]
    [ExporterHeader("请求头", Format = "", Width = 25, IsBold = true)]
    public string RequestHeaders { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    [ImporterHeader(Name = "请求体")]
    [ExporterHeader("请求体", Format = "", Width = 25, IsBold = true)]
    public string RequestBody { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    [ImporterHeader(Name = "响应状态码")]
    [ExporterHeader("响应状态码", Format = "", Width = 25, IsBold = true)]
    public int? StatusCode { get; set; }

    /// <summary>
    /// 响应头
    /// </summary>
    [ImporterHeader(Name = "响应头")]
    [ExporterHeader("响应头", Format = "", Width = 25, IsBold = true)]
    public string ResponseHeaders { get; set; }

    /// <summary>
    /// 响应体
    /// </summary>
    [ImporterHeader(Name = "响应体")]
    [ExporterHeader("响应体", Format = "", Width = 25, IsBold = true)]
    public string ResponseBody { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    [ImporterHeader(Name = "异常信息")]
    [ExporterHeader("异常信息", Format = "", Width = 25, IsBold = true)]
    public string Exception { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    [ImporterHeader(Name = "开始时间")]
    [ExporterHeader("开始时间", Format = "", Width = 25, IsBold = true)]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [ImporterHeader(Name = "结束时间")]
    [ExporterHeader("结束时间", Format = "", Width = 25, IsBold = true)]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    [ImporterHeader(Name = "耗时（毫秒）")]
    [ExporterHeader("耗时（毫秒）", Format = "", Width = 25, IsBold = true)]
    public long? Elapsed { get; set; }

    /// <summary>
    /// 创建用户
    /// </summary>
    [ImporterHeader(Name = "创建用户")]
    [ExporterHeader("创建用户", Format = "", Width = 25, IsBold = true)]
    public string CreateUserName { get; set; }
}