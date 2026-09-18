// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Mvc.Rendering;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统流水号服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 100, Description = "系统流水号")]
public class SysSerialService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysSerial> _sysSerialRep;
    private readonly SysCacheService _sysCacheService;

    private readonly UserManager _userManager;
    private readonly ISqlSugarClient _db;

    /// <summary>
    /// 因为缓存的是代理，无法序列化，所以只能用字典缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, Dictionary<string, Func<long, long, DateTime, UserManager, string>>> _slotTempMap = new();

    public SysSerialService(SqlSugarRepository<SysSerial> sysSerialRep,
        SysCacheService sysCacheService, ISqlSugarClient db,
        UserManager userManager)
    {
        _db = db;
        _userManager = userManager;
        _sysSerialRep = sysSerialRep;
        _sysCacheService = sysCacheService;
    }

    /// <summary>
    /// 分页查询本地序列 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("分页查询本地序列")]
    [ApiDescriptionSettings(Name = "Page"), HttpPost]
    public async Task<SqlSugarPagedList<PageSerialOutput>> Page(PageSerialInput input)
    {
        input.Keyword = input.Keyword?.Trim();
        var query = _sysSerialRep.AsQueryable()
            .Where(u => u.TenantId == _userManager.TenantId)
            .WhereIF(input.Type != null, u => u.Type == input.Type)
            .WhereIF(input.Status != null, u => u.Status == input.Status)
            .Select<PageSerialOutput>();
        return await query.MergeTable().OrderBuilder(input).ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 获取本地序列详情 ℹ️
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取本地序列详情")]
    [ApiDescriptionSettings(Name = "Detail"), HttpGet]
    public async Task<SysSerial> Detail([FromQuery] BaseIdInput input)
    {
        return await _sysSerialRep.GetFirstAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 增加本地序列 ➕
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("增加本地序列")]
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    public async Task<long> Add(AddSerialInput input)
    {
        var typeList = GetTypeList();
        if (typeList.All(u => u.Value != input.Type)) throw Oops.Oh(ErrorCodeEnum.S0007, input.Type);
        if (await _sysSerialRep.IsAnyAsync(u => u.Type == input.Type && u.TenantId == _userManager.TenantId)) throw Oops.Oh(ErrorCodeEnum.D1006);

        var entity = input.Adapt<SysSerial>();
        entity.Expy = DateTime.Now;
        entity.Seq = entity.Min - 1;
        return await _sysSerialRep.InsertAsync(entity) ? entity.Id : default;
    }

    /// <summary>
    /// 更新本地序列 ✏️
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("更新本地序列")]
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    public async Task Update(UpdateSerialInput input)
    {
        var typeList = GetTypeList();
        if (typeList.All(u => u.Value != input.Type)) throw Oops.Oh(ErrorCodeEnum.S0007, input.Type);
        var seq = await _sysSerialRep.GetFirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        if (await _sysSerialRep.IsAnyAsync(u => u.Type == input.Type && u.TenantId == _userManager.TenantId && input.Id != seq.Id)) throw Oops.Oh(ErrorCodeEnum.D1006);

        var entity = input.Adapt<SysSerial>();
        // 原来的数据
        var original = await _sysSerialRep.GetFirstAsync(u => u.Id == input.Id);
        // 最小值发生变化，并且当最小值比当前流水号大时
        var seqChange = false;
        if (original.Min != entity.Min && entity.Min - 1 > original.Seq)
        {
            // 使下个流水号为更新后的最小值
            entity.Seq = entity.Min - 1;
            seqChange = true;
        }
        await _sysSerialRep.AsUpdateable(entity).IgnoreColumns(u => new { u.Expy }).IgnoreColumnsIF(seqChange == false, u => new { u.Seq }).ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除本地序列 ❌
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("删除本地序列")]
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    public async Task Delete(BaseIdInput input)
    {
        var entity = await _sysSerialRep.GetFirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        await _sysSerialRep.DeleteAsync(entity);
    }

    /// <summary>
    /// 设置本地序列状态 🚫
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("设置本地序列状态")]
    [ApiDescriptionSettings(Name = "SetStatus"), HttpPost]
    public async Task SetStatus(BaseStatusInput input)
    {
        await _sysSerialRep.AsUpdateable().SetColumns(u => u.Status, input.Status).Where(u => u.Id == input.Id).ExecuteCommandAsync();
    }

    /// <summary>
    /// 获取流水号类型
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取流水号类型"), HttpGet]
    public List<SelectListItem> GetTypeList()
    {
        var typeList = _sysCacheService.Get<List<SelectListItem>>(CacheConst.KeySysSerialType);
        if (typeList != null) return typeList;

        typeList = App.EffectiveTypes
            .Where(u => !u.IsInterface && u.IsClass && u.HasImplementedRawGeneric(typeof(ISerialTypeProvider)))
            .SelectMany(u => u.GetFields())
            .Select(u => new SelectListItem(u.GetCustomAttribute<DescriptionAttribute>()?.Description ?? u.Name, u.Name))
            .ToList();

        if (typeList.GroupBy(u => u.Value).Any(u => u.Count() > 1)) throw Oops.Oh(ErrorCodeEnum.S0008);
        _sysCacheService.Set(CacheConst.KeySysSerialType, typeList);
        return typeList;
    }

    /// <summary>
    /// 获取流水号
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取流水号"), HttpPost]
    public async Task<string> NextSeqNo(GetNextSeqInput input)
    {
        return await NextSeqNo(input.Type);
    }

    /// <summary>
    /// 获取全局流水号
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取全局流水号"), HttpPost]
    public async Task<string> GlobalNextSeqNo(GetNextSeqInput input)
    {
        return await NextSeqNo(input.Type, true);
    }

    /// <summary>
    /// 预览流水号
    /// </summary>
    /// <returns></returns>
    [DisplayName("预览流水号"), HttpPost]
    public string Preview(PreviewSysSerialInput input)
    {
        return FormatSeqNo(new()
        {
            Formater = input.Formater,
            Seq = input.Seq + 1,
            Max = input.Max,
        }, DateTime.Now);
    }

    /// <summary>
    /// 获取插槽列表
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取插槽列表")]
    public List<string> GetSlotList()
    {
        return GetSlotMap(0, 1, DateTime.Now).Select(k => k.Key).ToList();
    }

    /// <summary>
    /// 获取流水号
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="isGlobal">是否是全局唯一流水号</param>
    /// <param name="isTran">是否开启事务</param>
    /// <returns></returns>
    [NonAction]
    public async Task<string> NextSeqNo(string type, bool isGlobal = false, bool isTran = true)
    {
        // 获取租户Id, 以及分布式锁缓存键名
        long? tenantId = isGlobal ? SqlSugarConst.DefaultTenantId : _userManager.TenantId;
        string cacheKey = $"{CacheConst.KeySerialLock}:{tenantId}:{type}";

        // 获取分布式锁
        using var distributedLock = _sysCacheService.BeginCacheLock(cacheKey, 1000) ?? throw Oops.Oh(ErrorCodeEnum.S0001);

        // 记录流水号生成耗时
        var stopWatch = Stopwatch.StartNew();
        try
        {
            // 查询系统流水号配置
            var seq = await _sysSerialRep.AsQueryable().ClearFilter<ITenantIdFilter>().FirstAsync(u =>
                u.Type == type && u.Status == StatusEnum.Enable && u.TenantId == tenantId) ?? throw Oops.Oh(ErrorCodeEnum.S0002);

            // 尝试重置系统流水号配置
            var nowDate = DateTime.Now;
            TryRestSeqNo(seq, nowDate);

            // 检查是否超出最大值
            seq.Seq++;
            if (seq.Seq > seq.Max) throw Oops.Oh(ErrorCodeEnum.S0004);

            // 开启事务
            using var uow = _db.CreateContext(isTran);

            // 更新失败，抛出异常
            await _db.RunWithoutFilter(async () =>
            {
                if (!await _sysSerialRep.UpdateAsync(u => new() { Expy = seq.Expy, Seq = seq.Seq }, u => u.Id == seq.Id))
                    throw Oops.Oh(ErrorCodeEnum.S0005);
            });

            uow.Commit();
            return FormatSeqNo(seq, nowDate);
        }
        finally
        {
            stopWatch.Stop();
            Console.WriteLine(string.Format("流水号【{0}】生成耗时：{1}毫秒", type.GetDescription(), stopWatch.ElapsedMilliseconds));
        }
    }

    /// <summary>
    /// 重置系统流水号配置
    /// </summary>
    /// <param name="seq"></param>
    /// <param name="nowDate"></param>
    private void TryRestSeqNo(SysSerial seq, DateTime nowDate)
    {
        switch (seq.ResetInterval)
        {
            case ResetIntervalEnum.Never:
                break;

            case ResetIntervalEnum.Day:
                if (nowDate.Date != seq.Expy.Date)
                {
                    seq.Expy = nowDate.Date;
                    seq.Seq = seq.Min - 1;
                }
                break;

            case ResetIntervalEnum.Month:
                if (nowDate.Month != seq.Expy.Month || nowDate.Year != seq.Expy.Year)
                {
                    seq.Expy = nowDate.Date;
                    seq.Seq = seq.Min - 1;
                }
                break;

            case ResetIntervalEnum.Year:
                if (nowDate.Year != seq.Expy.Year)
                {
                    seq.Expy = nowDate.Date;
                    seq.Seq = seq.Min - 1;
                }
                break;

            default:
                throw Oops.Oh(ErrorCodeEnum.S0006);
        }
    }

    /// <summary>
    /// 按格式渲染流水号
    /// </summary>
    /// <param name="seq"></param>
    /// <param name="nowDate"></param>
    /// <returns></returns>
    private string FormatSeqNo(SysSerial seq, DateTime nowDate)
    {
        var replacements = GetSlotMap(seq.Seq, seq.Max, nowDate);
        return Regex.Replace(seq.Formater, @"\{.*?\}", match =>
        {
            string key = match.Value;
            if (replacements.TryGetValue(key, out var valueFunc)) return valueFunc();
            throw Oops.Oh(string.Format("无效插槽: {0}", key));
        });
    }

    /// <summary>
    /// 获取插槽映射表
    /// </summary>
    /// <returns></returns>
    [NonAction]
    private Dictionary<string, Func<string>> GetSlotMap(long seq, long max, DateTime nowDate)
    {
        // 获取插槽逻辑模板
        var slotLogic = _slotTempMap.GetOrAdd(CacheConst.KeySysSerialSlot, _ =>
        {
            // 默认插槽逻辑
            var logicMap = new Dictionary<string, Func<long, long, DateTime, UserManager, string>>
            {
                // 序号格式化
                ["{SEQ}"] = (s, m, _, _) => s.ToString($"D{m.ToString().Length}"),
                // 时间相关（依赖 nowDate）
                ["{yyyy}"] = (_, _, d, _) => d.Year.ToString(),
                ["{yy}"] = (_, _, d, _) => d.Year.ToString().Substring(2),
                ["{MM}"] = (_, _, d, _) => d.Month.ToString("D2"),
                ["{dd}"] = (_, _, d, _) => d.Day.ToString("D2"),
                ["{HH}"] = (_, _, d, _) => d.Hour.ToString("D2"),
                ["{mm}"] = (_, _, d, _) => d.Minute.ToString("D2"),
                ["{ss}"] = (_, _, d, _) => d.Second.ToString("D2"),
                ["{TenantId}"] = (_, _, _, u) => u?.TenantId.ToString(),
                ["{UserId}"] = (_, _, _, u) => u?.UserId.ToString(),
                ["{OrgId}"] = (_, _, _, u) => u?.OrgId.ToString(),
                ["{OrgType}"] = (_, _, _, u) => u?.OrgType,
            };

            // 扩展插槽提供者
            var providers = App.EffectiveTypes
                .Where(u => !u.IsInterface && u.IsClass && u.HasImplementedRawGeneric(typeof(ISerialSlotProvider)))
                .Select(u => (ISerialSlotProvider)Activator.CreateInstance(u))
                .ToList();
            foreach (var provider in providers)
            {
                var providerLogic = provider.GetSlotMap();
                foreach (var kv in providerLogic) logicMap[kv.Key] = kv.Value;
            }

            return logicMap;
        });

        // 将逻辑模板绑定到当前参数，生成最终的 Func<string>
        var result = new Dictionary<string, Func<string>>();
        foreach (var kv in slotLogic) result.TryAdd(kv.Key, () => kv.Value(seq, max, nowDate, _userManager));
        return result;
    }
}