// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款账号管理服务（F1）
/// </summary>
[ApiDescriptionSettings(Order = 400, Description = "收款账号")]
public class PayAccountService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly PayAuditService _payAuditService;
    private readonly UserManager _userManager;

    public PayAccountService(SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayOrder> payOrderRep,
        PayAuditService payAuditService,
        UserManager userManager)
    {
        _payAccountRep = payAccountRep;
        _payOrderRep = payOrderRep;
        _payAuditService = payAuditService;
        _userManager = userManager;
    }

    /// <summary>
    /// 计算账号剩余可用额度 = 总额度 − 已用额度 − 锁定中额度
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    [NonAction]
    public static decimal GetRemainingQuota(PayAccount account)
    {
        return account == null ? 0m : account.TotalQuota - account.UsedQuota - account.LockedQuota;
    }

    /// <summary>
    /// 获取收款账号分页列表（F1.5）
    /// </summary>
    /// <remarks>
    /// 按设计决策（2026-09-16）：前后台均**不脱敏**，列表返回完整账号信息，
    /// 以便管理员核对与维护账号。账号明文的保护由接口签名鉴权 + 后台 RBAC 承担。
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取收款账号分页列表")]
    public async Task<SqlSugarPagedList<PayAccountOutput>> Page(PagePayAccountInput input)
    {
        var paged = await _payAccountRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Type), u => u.Type == input.Type)
            .WhereIF(input.Status.HasValue, u => u.Status == input.Status.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .Select<PayAccountOutput>()
            .ToPagedListAsync(input.Page, input.PageSize);

        foreach (var item in paged.Items)
        {
            item.RemainingQuota = item.TotalQuota - item.UsedQuota - item.LockedQuota;
            item.StatusText = item.Status.GetDescription();
        }
        return paged;
    }

    /// <summary>
    /// 获取收款账号详情
    /// </summary>
    /// <remarks>返回完整账号信息（前后台均不脱敏，见设计决策）。</remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取收款账号详情")]
    public async Task<PayAccountOutput> Detail([FromQuery] BaseIdInput input)
    {
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.P1002);
        var output = account.Adapt<PayAccountOutput>();
        output.RemainingQuota = GetRemainingQuota(account);
        output.StatusText = account.Status.GetDescription();
        return output;
    }

    /// <summary>
    /// 新增收款账号/收款码（F1.1）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("新增收款账号")]
    public async Task<long> Add(AddPayAccountInput input)
    {
        var account = new PayAccount
        {
            Type = input.Type.Trim(),
            AccountInfo = input.AccountInfo.Trim(),
            TotalQuota = input.TotalQuota,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = PayAccountStatusEnum.Enabled,
            Remark = input.Remark
        };
        await _payAccountRep.InsertAsync(account);

        // F7.3 审计：只增不改
        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountAdd, nameof(PayAccount), account.Id,
            account.Type, null, account, $"新增收款账号，初始额度 {account.TotalQuota:0.00}");

        return account.Id;
    }

    /// <summary>
    /// 编辑收款账号（F1.2：修改备注、状态）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("编辑收款账号")]
    public async Task Update(UpdatePayAccountInput input)
    {
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.P1002);

        // 「已用完」是系统自动状态，不允许手工设置
        if (input.Status == PayAccountStatusEnum.Exhausted)
            throw Oops.Oh(ErrorCodeEnum.P1013);

        // 启用前校验剩余额度，避免出现"启用但无额度"的无效状态
        if (input.Status == PayAccountStatusEnum.Enabled && GetRemainingQuota(account) <= 0)
            throw Oops.Oh(ErrorCodeEnum.P1014);

        var before = new { account.Remark, account.Status };

        account.Remark = input.Remark;
        account.Status = input.Status;
        await _payAccountRep.AsUpdateable(account)
            .UpdateColumns(u => new { u.Remark, u.Status, u.UpdateTime, u.UpdateUserId, u.UpdateUserName })
            .ExecuteCommandAsync();

        // F7.3 审计：只增不改
        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountUpdate, nameof(PayAccount), account.Id,
            account.Type, before, new { account.Remark, account.Status }, "编辑收款账号");
    }

    /// <summary>
    /// 追加总额度（F1.2）
    /// </summary>
    /// <remarks>
    /// 追加后若账号处于「已用完」且剩余额度恢复为正数，自动回置为「启用」重新参与匹配（F1.4）。
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "AddQuota"), HttpPost]
    [DisplayName("追加总额度")]
    public async Task AddQuota(AddQuotaInput input)
    {
        var before = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.P1002);
        var beforeQuota = before.TotalQuota;

        var rows = await _payAccountRep.AsUpdateable()
            .SetColumns(u => new PayAccount
            {
                TotalQuota = u.TotalQuota + input.Quota,
                UpdateTime = DateTime.Now,
                UpdateUserId = _userManager.UserId,
                UpdateUserName = _userManager.RealName
            })
            .Where(u => u.Id == input.Id)
            .ExecuteCommandAsync();
        if (rows == 0) throw Oops.Oh(ErrorCodeEnum.P1002);

        // 额度恢复后自动解除「已用完」状态（F1.4，条件更新，天然幂等）
        await SyncStatusByQuotaAsync(input.Id);

        // F7.3 审计：记录追加前后总额度，便于对账
        await _payAuditService.WriteAsync(PayAuditActionEnum.QuotaAdd, nameof(PayAccount), input.Id,
            before.Type,
            new { TotalQuota = beforeQuota },
            new { TotalQuota = beforeQuota + input.Quota },
            $"追加额度 {input.Quota:0.00}");
    }

    /// <summary>
    /// 启用/停用收款账号（F1.3）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "SetStatus"), HttpPost]
    [DisplayName("启用/停用收款账号")]
    public async Task SetStatus(SetPayAccountStatusInput input)
    {
        if (input.Status == PayAccountStatusEnum.Exhausted)
            throw Oops.Oh(ErrorCodeEnum.P1013);

        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.P1002);
        if (input.Status == PayAccountStatusEnum.Enabled && GetRemainingQuota(account) <= 0)
            throw Oops.Oh(ErrorCodeEnum.P1014);

        var fromStatus = account.Status;

        account.Status = input.Status;
        await _payAccountRep.AsUpdateable(account)
            .UpdateColumns(u => new { u.Status, u.UpdateTime, u.UpdateUserId, u.UpdateUserName })
            .ExecuteCommandAsync();

        // F7.3 审计：只增不改
        await _payAuditService.WriteAsync(PayAuditActionEnum.StatusChange, nameof(PayAccount), account.Id,
            account.Type,
            new { Status = fromStatus },
            new { Status = input.Status },
            $"状态变更：{fromStatus.GetDescription()} → {input.Status.GetDescription()}");
    }

    /// <summary>
    /// 删除收款账号
    /// </summary>
    /// <remarks>存在未终结订单（待到账/部分到账）时禁止删除，避免订单悬挂。</remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除收款账号")]
    public async Task Delete(BaseIdInput input)
    {
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.P1002);

        var hasActiveOrder = await _payOrderRep.AsQueryable().AnyAsync(u => u.AccountId == input.Id
            && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Partial));
        if (hasActiveOrder) throw Oops.Oh(ErrorCodeEnum.P1012);

        await _payAccountRep.DeleteByIdAsync(input.Id);

        // F7.3 审计：只增不改（删除前快照留档，保证账号历史可追溯）
        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountDelete, nameof(PayAccount), account.Id,
            account.Type, account, null, "删除收款账号");
    }

    /// <summary>
    /// 按当前剩余额度同步账号状态（F1.4）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 供追加额度（F1.2）、额度释放与过期回收（F3.2）、到账结转（F4）等额度变动流程在写完后调用：
    /// 剩余归零 → 自动置「已用完」；剩余恢复为正 → 自动回置「启用」重新参与匹配。
    /// 两个方向都用条件更新，天然幂等，也不会覆盖管理员手工「停用」的状态
    /// （只处理 Enabled ⇄ Exhausted 之间的迁移）。
    /// </para>
    /// <para>
    /// 注意：匹配（F2）路径<b>不</b>调用本方法 —— 那里的「额度耗尽置位」已内联进
    /// <see cref="PayAllocateService.TryLockQuotaAsync"/> 的单条原子语句，避免锁定与置位之间出现窗口。
    /// </para>
    /// </remarks>
    /// <param name="accountId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task SyncStatusByQuotaAsync(long accountId)
    {
        // 额度耗尽：启用 → 已用完
        await _payAccountRep.AsUpdateable()
            .SetColumns(u => new PayAccount { Status = PayAccountStatusEnum.Exhausted })
            .Where(u => u.Id == accountId
                && u.Status == PayAccountStatusEnum.Enabled
                && u.TotalQuota <= u.UsedQuota + u.LockedQuota)
            .ExecuteCommandAsync();

        // 额度恢复（如追加额度、订单过期释放预占）：已用完 → 启用
        await _payAccountRep.AsUpdateable()
            .SetColumns(u => new PayAccount { Status = PayAccountStatusEnum.Enabled })
            .Where(u => u.Id == accountId
                && u.Status == PayAccountStatusEnum.Exhausted
                && u.TotalQuota > u.UsedQuota + u.LockedQuota)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 获取收款类型下拉列表（取字典 pay_account_type）
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取收款类型下拉列表")]
    public async Task<List<PayTypeOption>> GetTypeOptions()
    {
        var dictService = App.GetRequiredService<SysDictDataService>();
        var list = await dictService.GetDataList(PayConst.AccountTypeDictCode);
        return list.Select(u => new PayTypeOption { Label = u.Label, Value = u.Value }).ToList();
    }
}

/// <summary>
/// 收款类型下拉项
/// </summary>
public class PayTypeOption
{
    /// <summary>
    /// 显示名
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// 取值
    /// </summary>
    public string Value { get; set; }
}
