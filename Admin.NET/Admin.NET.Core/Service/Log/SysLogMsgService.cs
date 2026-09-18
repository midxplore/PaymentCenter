// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统消息日志服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 370, Description = "消息日志")]
public class SysLogMsgService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysLogMsg> _sysLogMsgRep;

    public SysLogMsgService(SqlSugarRepository<SysLogMsg> sysLogMsgRep)
    {
        _sysLogMsgRep = sysLogMsgRep;
    }

    /// <summary>
    /// 获取消息日志分页列表 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("获取消息日志分页列表")]
    public async Task<SqlSugarPagedList<SysLogMsg>> Page(PageMsgLogInput input)
    {
        return await _sysLogMsgRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.StartTime.ToString()), u => u.CreateTime >= input.StartTime)
            .WhereIF(!string.IsNullOrWhiteSpace(input.EndTime.ToString()), u => u.CreateTime <= input.EndTime)
            .WhereIF(!string.IsNullOrWhiteSpace(input.ReceiveUserName), u => u.ReceiveUserName == input.ReceiveUserName)
            .WhereIF(!string.IsNullOrWhiteSpace(input.SendUserName), u => u.SendUserName == input.SendUserName)
            .IgnoreColumns(u => new { u.Message })
            .OrderBuilder(input)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取消息日志详情 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("获取消息日志详情")]
    public async Task<SysLogMsg> GetDetail(long id)
    {
        return await _sysLogMsgRep.GetByIdAsync(id);
    }

    /// <summary>
    /// 清空消息日志 🔖
    /// </summary>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Clear"), HttpPost]
    [DisplayName("清空消息日志")]
    public void Clear()
    {
        _sysLogMsgRep.AsSugarClient().DbMaintenance.TruncateTable<SysLogMsg>();
    }

    /// <summary>
    /// 按年按天数统计消息日志 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("按年按天数统计消息日志")]
    public async Task<List<StatLogOutput>> GetYearDayStats()
    {
        var _db = _sysLogMsgRep.AsSugarClient();

        // SqlServer 数据库，全局设置 IsWithNoLockQuery = true 时 Reportable 查询会报错
        // 通过禁用当前上下文的全局设置 Nolock，解决 Reportable 查询报错的问题
        _db.CurrentConnectionConfig.MoreSettings.IsWithNoLockQuery = false;

        var now = DateTime.Now;
        var days = (now - now.AddYears(-1)).Days + 1;
        var day365 = Enumerable.Range(0, days).Select(u => now.AddDays(-u)).ToList();
        var queryableLeft = _db.Reportable(day365).ToQueryable<DateTime>();

        var queryableRight = _db.Queryable<SysLogMsg>(); //.SplitTable(tab => tab);
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
}