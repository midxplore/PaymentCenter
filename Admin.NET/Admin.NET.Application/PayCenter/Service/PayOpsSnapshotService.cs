// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总日终快照（供作业与汇总查询复用）
/// </summary>
public class PayOpsSnapshotService : ITransient
{
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOpsDailySnapshot> _snapshotRep;

    public PayOpsSnapshotService(
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOpsDailySnapshot> snapshotRep)
    {
        _payAccountRep = payAccountRep;
        _payOrderRep = payOrderRep;
        _snapshotRep = snapshotRep;
    }

    /// <summary>
    /// 读取此刻池况（活库合计）
    /// </summary>
    public async Task<PayOpsPoolNowOutput> GetPoolNowAsync()
    {
        var accounts = await _payAccountRep.AsQueryable()
            .Select(u => new { u.TotalQuota, u.UsedQuota, u.LockedQuota, u.Status })
            .ToListAsync();

        var total = accounts.Sum(u => u.TotalQuota);
        var used = accounts.Sum(u => u.UsedQuota);
        var locked = accounts.Sum(u => u.LockedQuota);
        var enabled = accounts.Count(u => u.Status == PayAccountStatusEnum.Enabled);

        var unpaidList = await _payOrderRep.AsQueryable()
            .Where(u => u.Status == PayOrderStatusEnum.Failed)
            .Select(u => u.RequestAmount)
            .ToListAsync();
        var unpaid = unpaidList.Sum();

        return new PayOpsPoolNowOutput
        {
            TotalQuotaSum = total,
            UsedQuotaSum = used,
            LockedQuotaSum = locked,
            RemainingQuotaSum = total - used - locked,
            UnpaidLockedSum = unpaid,
            AccountEnabledCount = enabled,
            AccountCount = accounts.Count
        };
    }

    /// <summary>
    /// 写入指定业务日日终快照；已存在则跳过（不改历史）
    /// </summary>
    /// <returns>是否新写入</returns>
    public async Task<bool> TryWriteSnapshotAsync(DateTime bizDate, CancellationToken ct = default)
    {
        bizDate = bizDate.Date;
        var exists = await _snapshotRep.AsQueryable()
            .AnyAsync(u => u.BizDate == bizDate);
        if (exists) return false;

        var now = await GetPoolNowAsync();
        var row = new PayOpsDailySnapshot
        {
            BizDate = bizDate,
            TotalQuotaSum = now.TotalQuotaSum,
            UsedQuotaSum = now.UsedQuotaSum,
            LockedQuotaSum = now.LockedQuotaSum,
            UnpaidLockedSum = now.UnpaidLockedSum,
            AccountEnabledCount = now.AccountEnabledCount,
            SnapshotTime = DateTime.Now,
            CreateTime = DateTime.Now,
            IsDelete = false
        };
        await _snapshotRep.InsertAsync(row);
        return true;
    }

    /// <summary>
    /// 作业入口：为「昨天」写日终快照（本地自然日）
    /// </summary>
    public Task<bool> WriteYesterdaySnapshotAsync(CancellationToken ct = default)
        => TryWriteSnapshotAsync(DateTime.Now.Date.AddDays(-1), ct);
}
