// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Net;

namespace Admin.NET.Core.Service;

/// <summary>
/// 请求日志服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 330, Description = "请求日志")]
public class SysLogHttpService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysLogHttp> _sysLogHttpRep;

    public SysLogHttpService(SqlSugarRepository<SysLogHttp> sysLogHttpRep, SysDictTypeService sysDictTypeService, ISqlSugarClient sqlSugarClient)
    {
        _sysLogHttpRep = sysLogHttpRep;
    }

    /// <summary>
    /// 分页查询请求日志 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("分页查询请求日志")]
    [ApiDescriptionSettings(Name = "Page"), HttpPost]
    public async Task<SqlSugarPagedList<PageLogHttpOutput>> Page(PageLogHttpInput input)
    {
        input.Keyword = input.Keyword?.Trim();
        var query = _sysLogHttpRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Keyword), u => u.HttpClientName.Contains(input.Keyword) || u.HttpApiDesc.Contains(input.Keyword) || u.RequestUrl.Contains(input.Keyword) || u.RequestBody.Contains(input.Keyword) || u.ResponseBody.Contains(input.Keyword) || u.Exception.Contains(input.Keyword))
            .WhereIF(!string.IsNullOrWhiteSpace(input.ActionName), u => u.ActionName.Contains(input.ActionName.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.HttpClientName), u => u.HttpClientName.Contains(input.HttpClientName.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.HttpApiDesc), u => u.HttpApiDesc.Contains(input.HttpApiDesc.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.RequestUrl), u => u.RequestUrl.Contains(input.RequestUrl.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.RequestBody), u => u.RequestBody.Contains(input.RequestBody.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.ResponseBody), u => u.ResponseBody.Contains(input.ResponseBody.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Exception), u => u.Exception.Contains(input.Exception.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.HttpMethod), u => u.HttpMethod == input.HttpMethod)
            .WhereIF(input.IsSuccessStatusCode != null, u => u.IsSuccessStatusCode == input.IsSuccessStatusCode)
            .WhereIF(input.StatusCode != null, u => u.StatusCode == (HttpStatusCode)input.StatusCode)
            .WhereIF(input.CreateTimeRange?.Length == 2, u => u.CreateTime >= input.CreateTimeRange[0] && u.CreateTime <= input.CreateTimeRange[1])
            .Select<PageLogHttpOutput>();
        query = query.MergeTable();
        return await query.OrderBuilder(input).ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取请求日志列表 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取请求日志列表")]
    [ApiDescriptionSettings(Name = "List"), HttpGet]
    public async Task<List<SysLogHttp>> GetList([FromQuery] PageLogHttpInput input)
    {
        return await _sysLogHttpRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Keyword), u => u.HttpClientName.Contains(input.Keyword) || u.HttpApiDesc.Contains(input.Keyword) || u.RequestUrl.Contains(input.Keyword) || u.RequestBody.Contains(input.Keyword) || u.ResponseBody.Contains(input.Keyword) || u.Exception.Contains(input.Keyword))
            .WhereIF(!string.IsNullOrWhiteSpace(input.HttpApiDesc), u => u.HttpApiDesc.Contains(input.HttpApiDesc.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.RequestUrl?.Trim()), u => u.RequestUrl.Contains(input.RequestUrl.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.RequestBody?.Trim()), u => u.RequestBody.Contains(input.RequestBody.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.ResponseBody?.Trim()), u => u.ResponseBody.Contains(input.ResponseBody.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.Exception?.Trim()), u => u.Exception.Contains(input.Exception.Trim()))
            .WhereIF(!string.IsNullOrWhiteSpace(input.HttpMethod?.Trim()), u => u.HttpMethod == input.HttpMethod)
            .WhereIF(input.IsSuccessStatusCode != null, u => u.IsSuccessStatusCode == input.IsSuccessStatusCode)
            .WhereIF(input.StatusCode != null, u => u.StatusCode == (HttpStatusCode)input.StatusCode)
            .WhereIF(input.HttpClientName != null, u => u.HttpClientName == input.HttpClientName.Trim())
            .WhereIF(input.CreateTimeRange?.Length == 2, u => u.CreateTime >= input.CreateTimeRange[0] && u.CreateTime <= input.CreateTimeRange[1])
            .ToListAsync();
    }

    /// <summary>
    /// 获取请求日志详情 ℹ️
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取请求日志详情")]
    [ApiDescriptionSettings(Name = "Detail"), HttpGet]
    public async Task<SysLogHttp> Detail([FromQuery] BaseIdInput input)
    {
        return await _sysLogHttpRep.GetFirstAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 按年按天数统计消息日志 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("按年按天数统计消息日志")]
    public async Task<List<StatLogOutput>> GetYearDayStats()
    {
        var _db = _sysLogHttpRep.AsSugarClient();

        var now = DateTime.Now;
        var days = (now - now.AddYears(-1)).Days + 1;
        var day365 = Enumerable.Range(0, days).Select(u => now.AddDays(-u)).ToList();
        var queryableLeft = _db.Reportable(day365).ToQueryable<DateTime>();

        var queryableRight = _db.Queryable<SysLogHttp>(); //.SplitTable(tab => tab);
        var list = await _db.Queryable(queryableLeft, queryableRight, JoinType.Left,
            (x1, x2) => x1.ColumnName.Date == x2.CreateTime.Date)
            .GroupBy((x1, x2) => x1.ColumnName)
            .Select((x1, x2) => new StatLogOutput
            {
                Count = SqlFunc.AggregateSum(SqlFunc.IIF(x2.Id > 0, 1, 0)),
                Date = x1.ColumnName.ToString("yyyy-MM-dd")
            })
            .MergeTable()
            .OrderBy(x => x.Date)
            .ToListAsync();

        return list;
    }

    /// <summary>
    /// 获取客户端名称集 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取客户端名称集")]
    public async Task<List<string>> GetHttpClientName()
    {
        return await _sysLogHttpRep.AsQueryable().Select(u => u.HttpClientName).Distinct().ToListAsync();
    }

    /// <summary>
    /// 导出请求日志记录 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("导出请求日志记录")]
    [ApiDescriptionSettings(Name = "Export"), HttpPost, NonUnify]
    public async Task<IActionResult> Export(ExportLogHttpInput input)
    {
        var list = (await Page(input)).Items?.Adapt<List<ExportLogHttpOutput>>() ?? new();
        if (input.SelectKeyList?.Count > 0) list = list.Where(x => input.SelectKeyList.Contains(x.Id)).ToList();
        return ExcelHelper.ExportTemplate(list, "请求日志导出记录");
    }
}