// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Net;

namespace Admin.NET.Core;

/// <summary>
/// Http请求日志表
/// </summary>
[SysTable]
[LogTable]
[SugarTable(null, "Http请求日志表")]
[SugarIndex("i_{table}_n", nameof(HttpClientName), OrderByType.Asc)]
public partial class SysLogHttp : EntityTenantId
{
    /// <summary>
    /// 客户端名称
    /// </summary>
    [SugarColumn(ColumnDescription = "客户端名称", Length = 32)]
    [MaxLength(32)]
    public string? HttpClientName { get; set; }

    /// <summary>
    /// 接口描述
    /// </summary>
    [SugarColumn(ColumnDescription = "接口描述", Length = 64)]
    [MaxLength(32)]
    public string? HttpApiDesc { get; set; }

    /// <summary>
    /// 请求方式
    /// </summary>
    [SugarColumn(ColumnDescription = "请求方式", Length = 8)]
    [MaxLength(8)]
    public string? HttpMethod { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [SugarColumn(ColumnDescription = "是否成功")]
    public YesNoEnum? IsSuccessStatusCode { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [SugarColumn(ColumnDescription = "模块名称", Length = 64)]
    [MaxLength(32)]
    public string? ActionName { get; set; }

    /// <summary>
    /// 请求地址
    /// </summary>
    [SugarColumn(ColumnDescription = "请求地址", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? RequestUrl { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    [SugarColumn(ColumnDescription = "请求头", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? RequestHeaders { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    [SugarColumn(ColumnDescription = "请求体", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? RequestBody { get; set; }

    /// <summary>
    /// 请求体明文
    /// </summary>
    [SugarColumn(ColumnDescription = "请求体明文", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? RequestBodyPlaintext { get; set; }

    /// <summary>
    /// 响应状态码
    /// </summary>
    [SugarColumn(ColumnDescription = "响应状态码")]
    public HttpStatusCode? StatusCode { get; set; }

    /// <summary>
    /// 响应头
    /// </summary>
    [SugarColumn(ColumnDescription = "响应头", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? ResponseHeaders { get; set; }

    /// <summary>
    /// 响应体
    /// </summary>
    [SugarColumn(ColumnDescription = "响应体", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? ResponseBody { get; set; }

    /// <summary>
    /// 响应体明文
    /// </summary>
    [SugarColumn(ColumnDescription = "响应体明文", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? ResponseBodyPlaintext { get; set; }

    /// <summary>
    /// 异常信息
    /// </summary>
    [SugarColumn(ColumnDescription = "异常信息", ColumnDataType = StaticConfig.CodeFirst_BigString)]
    public string? Exception { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    [SugarColumn(ColumnDescription = "开始时间")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [SugarColumn(ColumnDescription = "结束时间")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    [SugarColumn(ColumnDescription = "耗时（毫秒）")]
    public long? Elapsed { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 创建者Id
    /// </summary>
    [OwnerUser]
    [SugarColumn(ColumnDescription = "创建者Id", IsOnlyIgnoreUpdate = true)]
    public virtual long? CreateUserId { get; set; }

    /// <summary>
    /// 创建者姓名
    /// </summary>
    [SugarColumn(ColumnDescription = "创建者姓名", Length = 64, IsOnlyIgnoreUpdate = true)]
    public virtual string? CreateUserName { get; set; }
}