// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 收款账号管理服务（F1）
/// </summary>
[ApiDescriptionSettings(Order = 400, Description = "收款账号")]
public class PayAccountService : IDynamicApiController, ITransient
{
    private const string QrUrlPrefix = "/upload/pay-qr/";
    private const string QrAllowSuffix = ".jpg.png.bmp.webp";

    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly PayAuditService _payAuditService;
    private readonly SysFileService _sysFileService;
    private readonly UserManager _userManager;

    public PayAccountService(SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayOrder> payOrderRep,
        PayAuditService payAuditService,
        SysFileService sysFileService,
        UserManager userManager)
    {
        _payAccountRep = payAccountRep;
        _payOrderRep = payOrderRep;
        _payAuditService = payAuditService;
        _sysFileService = sysFileService;
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
            if (item.AccountTier == 0) item.AccountTier = PayAccountTierEnum.Normal;
            item.AccountTierText = item.AccountTier.GetDescription();
            item.QrImageUrl ??= "";
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
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);
        var output = account.Adapt<PayAccountOutput>();
        output.RemainingQuota = GetRemainingQuota(account);
        output.StatusText = account.Status.GetDescription();
        if (output.AccountTier == 0) output.AccountTier = PayAccountTierEnum.Normal;
        output.AccountTierText = output.AccountTier.GetDescription();
        output.QrImageUrl ??= "";
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
        var type = (input.Type ?? "").Trim();
        var payeeName = (input.PayeeName ?? "").Trim();
        var accountNo = (input.AccountNo ?? "").Trim();
        var bankName = (input.BankName ?? "").Trim();
        var branchName = (input.BranchName ?? "").Trim();
        var accountInfo = NormalizeAccountInfo(string.IsNullOrEmpty(accountNo) ? input.AccountInfo : accountNo);
        var qrImageUrl = NormalizeQrImageUrl(input.QrImageUrl);
        if (string.IsNullOrEmpty(accountNo) && !string.IsNullOrEmpty(accountInfo))
            accountNo = accountInfo;
        EnsureStructuredPayload(type, payeeName, accountNo, bankName, branchName, qrImageUrl);

        var account = new PayAccount
        {
            Type = type,
            PayeeName = string.IsNullOrEmpty(payeeName) ? null : payeeName,
            AccountNo = string.IsNullOrEmpty(accountNo) ? null : accountNo,
            BankName = string.IsNullOrEmpty(bankName) ? null : bankName,
            BranchName = string.IsNullOrEmpty(branchName) ? null : branchName,
            AccountInfo = accountInfo,
            QrImageUrl = string.IsNullOrEmpty(qrImageUrl) ? null : qrImageUrl,
            TotalQuota = input.TotalQuota,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = PayAccountStatusEnum.Enabled,
            AccountTier = input.AccountTier == 0 ? PayAccountTierEnum.Normal : input.AccountTier,
            MinAmount = NormalizeMinAmount(input.MinAmount, input.TotalQuota),
            Remark = PayHtmlSanitize.Sanitize(input.Remark)
        };
        await _payAccountRep.InsertAsync(account);

        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountAdd, nameof(PayAccount), account.Id,
            account.Type, null,
            new { account.Type, account.TotalQuota, account.MinAmount, account.Status, HasQrImage = account.QrImageUrl != null },
            $"新增收款账号，初始额度 {account.TotalQuota:0.00}");

        return account.Id;
    }

    /// <summary>
    /// 上传收款码图片，返回根相对路径（限边转 JPEG，减轻收银台加载）
    /// </summary>
    [ApiDescriptionSettings(Name = "UploadQr"), HttpPost]
    [DisplayName("上传收款码图片")]
    public async Task<string> UploadQr([Required] IFormFile file)
    {
        // nginx 反代静态时无边缘压缩，落库前限边转 JPEG，收银台加载才快
        var optimized = await PayQrImageOptimize.ToJpegFormFileAsync(file);
        var sysFile = await _sysFileService.UploadFile(
            new UploadFileInput { File = optimized, AllowSuffix = ".jpg.jpeg" },
            "upload/pay-qr");
        return NormalizeQrImageUrl(sysFile.Url);
    }

    /// <summary>
    /// 编辑收款账号（账号文本、收款码、备注、状态）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("编辑收款账号")]
    public async Task Update(UpdatePayAccountInput input)
    {
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);

        if (input.Status == PayAccountStatusEnum.Exhausted || input.Status == PayAccountStatusEnum.InUse)
            throw Oops.Oh(ErrorCodeEnum.API_STATUS_READONLY);

        if (input.Status == PayAccountStatusEnum.Enabled && GetRemainingQuota(account) <= 0)
            throw Oops.Oh(ErrorCodeEnum.API_QUOTA_EXHAUSTED);

        var before = new
        {
            account.Remark,
            account.Status,
            HasAccountInfo = !string.IsNullOrEmpty(account.AccountInfo),
            HasQrImage = !string.IsNullOrEmpty(account.QrImageUrl)
        };

        if (input.PayeeName != null)
            account.PayeeName = string.IsNullOrWhiteSpace(input.PayeeName) ? null : input.PayeeName.Trim();
        if (input.AccountNo != null)
            account.AccountNo = string.IsNullOrWhiteSpace(input.AccountNo) ? null : input.AccountNo.Trim();
        if (input.BankName != null)
            account.BankName = string.IsNullOrWhiteSpace(input.BankName) ? null : input.BankName.Trim();
        if (input.BranchName != null)
            account.BranchName = string.IsNullOrWhiteSpace(input.BranchName) ? null : input.BranchName.Trim();

        if (input.AccountInfo != null)
            account.AccountInfo = NormalizeAccountInfo(input.AccountInfo);
        else if (input.AccountNo != null)
            account.AccountInfo = account.AccountNo ?? "";

        if (input.QrImageUrl != null)
        {
            var qr = NormalizeQrImageUrl(input.QrImageUrl);
            account.QrImageUrl = string.IsNullOrEmpty(qr) ? null : qr;
        }

        EnsureStructuredPayload(
            account.Type,
            account.PayeeName ?? "",
            account.AccountNo ?? account.AccountInfo ?? "",
            account.BankName ?? "",
            account.BranchName ?? "",
            account.QrImageUrl ?? "");

        account.Remark = PayHtmlSanitize.Sanitize(input.Remark);
        account.Status = input.Status;
        if (input.AccountTier.HasValue && input.AccountTier.Value != 0)
            account.AccountTier = input.AccountTier.Value;
        if (input.MinAmount.HasValue)
            account.MinAmount = NormalizeMinAmount(input.MinAmount.Value, account.TotalQuota);
        await _payAccountRep.AsUpdateable(account)
            .UpdateColumns(u => new
            {
                u.PayeeName,
                u.AccountNo,
                u.BankName,
                u.BranchName,
                u.AccountInfo,
                u.Remark,
                u.Status,
                u.AccountTier,
                u.MinAmount,
                u.QrImageUrl,
                u.UpdateTime,
                u.UpdateUserId,
                u.UpdateUserName
            })
            .ExecuteCommandAsync();

        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountUpdate, nameof(PayAccount), account.Id,
            account.Type, before,
            new
            {
                account.Remark,
                account.Status,
                account.AccountTier,
                account.MinAmount,
                HasAccountInfo = !string.IsNullOrEmpty(account.AccountInfo),
                HasQrImage = !string.IsNullOrEmpty(account.QrImageUrl)
            },
            "编辑收款账号");
    }

    /// <summary>
    /// 调整总额度（增加 / 减少）
    /// </summary>
    /// <remarks>
    /// <para>增加：总额度累加；若原为「已用完」且剩余恢复为正，自动回「启用」（F1.4）。</para>
    /// <para>
    /// 减少：存在预占 <c>LockedQuota&gt;0</c> 或状态为历史「占用中」时拒绝（避免动到在途单占用的额度空间）；
    /// 条件更新保证 <c>Total' ≥ Used</c> 且 <c>Total' ≥ MinAmount</c>，防并发超减。
    /// </para>
    /// <para>路由名仍为 <c>addQuota</c>，兼容既有按钮权限。</para>
    /// </remarks>
    [ApiDescriptionSettings(Name = "AddQuota"), HttpPost]
    [DisplayName("调整总额度")]
    public async Task AddQuota(AddQuotaInput input)
    {
        if (input.Quota <= 0m) throw Oops.Oh(ErrorCodeEnum.API_QUOTA_AMOUNT_INVALID);
        if (PayConst.HasExcessScale(input.Quota))
            throw Oops.Bah($"调整金额小数位不能超过 {PayConst.AmountScale} 位");

        var before = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);
        var beforeQuota = before.TotalQuota;
        var delta = input.Increase ? input.Quota : -input.Quota;

        if (!input.Increase)
        {
            // 有在途预占 / 历史占用中：禁止调减（用户口径「占用中不能减少」）
            if (before.LockedQuota > 0m || before.Status == PayAccountStatusEnum.InUse)
                throw Oops.Bah("该账号存在预占或占用中的订单，暂不可调减额度；请待订单完结或过期后再操作");

            var minFloor = before.MinAmount > 0m ? before.MinAmount : 0m;
            var floor = Math.Max(before.UsedQuota, minFloor);
            if (before.TotalQuota - input.Quota < floor)
                throw Oops.Bah($"调减后总额度不能低于 {floor:0.00}（已用 {before.UsedQuota:0.00}"
                    + (minFloor > 0m ? $"，最小匹配金额 {minFloor:0.00}" : "") + "）");
        }

        // 条件更新：增加无额外守卫；减少再次校验 Locked=0 且 Total-quota >= max(Used, MinAmount)
        var rows = input.Increase
            ? await _payAccountRep.AsUpdateable()
                .SetColumns(u => new PayAccount
                {
                    TotalQuota = u.TotalQuota + input.Quota,
                    UpdateTime = DateTime.Now,
                    UpdateUserId = _userManager.UserId,
                    UpdateUserName = _userManager.RealName
                })
                .Where(u => u.Id == input.Id)
                .ExecuteCommandAsync()
            : await _payAccountRep.AsUpdateable()
                .SetColumns(u => new PayAccount
                {
                    TotalQuota = u.TotalQuota - input.Quota,
                    UpdateTime = DateTime.Now,
                    UpdateUserId = _userManager.UserId,
                    UpdateUserName = _userManager.RealName
                })
                .Where(u => u.Id == input.Id
                    && u.LockedQuota == 0m
                    && u.Status != PayAccountStatusEnum.InUse
                    && u.TotalQuota - input.Quota >= u.UsedQuota
                    && u.TotalQuota - input.Quota >= u.MinAmount)
                .ExecuteCommandAsync();

        if (rows == 0)
        {
            if (input.Increase) throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);
            throw Oops.Bah("调减失败：可能刚产生预占/占用，或调减后低于已用/最小金额，请刷新后重试");
        }

        await SyncStatusByQuotaAsync(input.Id);

        var afterQuota = beforeQuota + delta;
        await _payAuditService.WriteAsync(PayAuditActionEnum.QuotaAdd, nameof(PayAccount), input.Id,
            before.Type,
            new { TotalQuota = beforeQuota },
            new { TotalQuota = afterQuota },
            input.Increase ? $"调增额度 {input.Quota:0.00}" : $"调减额度 {input.Quota:0.00}");
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
        if (input.Status == PayAccountStatusEnum.Exhausted || input.Status == PayAccountStatusEnum.InUse)
            throw Oops.Oh(ErrorCodeEnum.API_STATUS_READONLY);

        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);
        if (input.Status == PayAccountStatusEnum.Enabled && GetRemainingQuota(account) <= 0)
            throw Oops.Oh(ErrorCodeEnum.API_QUOTA_EXHAUSTED);

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
        var account = await _payAccountRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_NOT_FOUND);

        var hasActiveOrder = await _payOrderRep.AsQueryable().AnyAsync(u => u.AccountId == input.Id
            && (u.Status == PayOrderStatusEnum.Pending
                || u.Status == PayOrderStatusEnum.Partial
                || u.Status == PayOrderStatusEnum.AwaitingConfirm
                || u.Status == PayOrderStatusEnum.Failed));
        if (hasActiveOrder) throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_IN_USE);

        await _payAccountRep.DeleteByIdAsync(input.Id);

        await _payAuditService.WriteAsync(PayAuditActionEnum.AccountDelete, nameof(PayAccount), account.Id,
            account.Type,
            new { account.Type, account.Status, account.TotalQuota, HasQrImage = !string.IsNullOrEmpty(account.QrImageUrl) },
            null, "删除收款账号");
    }

    /// <summary>
    /// 按当前剩余额度同步账号状态（F1.4）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 供追加额度（F1.2）、额度释放与过期回收（F3.2）、到账结转（F4）等额度变动流程在写完后调用：
    /// 剩余归零 → 自动置「已用完」；剩余恢复为正 → 自动回置「启用」重新参与匹配。
    /// 两个方向都用条件更新，天然幂等，也不会覆盖管理员手工「停用」。
    /// 「占用中」由 <see cref="ReleaseInUseIfIdleAsync"/> 在订单终态结转后按是否仍有活跃单处理。
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
    /// 释放预占额度（不要求 InUse；过期重锁失败回滚等场景）
    /// </summary>
    [NonAction]
    public async Task ReleaseLockedQuotaAsync(long accountId, decimal amount)
    {
        if (accountId <= 0 || amount <= 0) return;
        const string sql = @"
UPDATE pay_account
   SET LockedQuota = LockedQuota - @amount,
       Status      = CASE
                       WHEN Status = @disabled THEN @disabled
                       WHEN TotalQuota <= UsedQuota + LockedQuota - @amount THEN @exhausted
                       ELSE @enabled
                     END,
       UpdateTime  = @now
 WHERE Id = @id
   AND LockedQuota >= @amount;";
        await _payAccountRep.Context.Ado.ExecuteCommandAsync(sql,
            new SugarParameter("@amount", amount),
            new SugarParameter("@id", accountId),
            new SugarParameter("@now", DateTime.Now),
            new SugarParameter("@enabled", (int)PayAccountStatusEnum.Enabled),
            new SugarParameter("@exhausted", (int)PayAccountStatusEnum.Exhausted),
            new SugarParameter("@disabled", (int)PayAccountStatusEnum.Disabled));
    }

    /// <summary>
    /// 对指定账号重锁额度（过期订单重传）。抢不到返回 false。
    /// </summary>
    [NonAction]
    public async Task<bool> TryRelockQuotaOnAccountAsync(long accountId, decimal amount)
    {
        if (accountId <= 0 || amount <= 0) return false;
        const string sql = @"
UPDATE pay_account
   SET LockedQuota = LockedQuota + @amount,
       Status      = CASE
                       WHEN TotalQuota <= UsedQuota + LockedQuota + @amount THEN @exhausted
                       ELSE @enabled
                     END,
       UpdateTime  = @now
 WHERE Id = @id
   AND Status = @enabled
   AND IsDelete = false
   AND TotalQuota >= UsedQuota + LockedQuota + @amount
   AND COALESCE(MinAmount, 0) <= @amount
RETURNING Id;";

        var ids = await _payAccountRep.Context.Ado.SqlQueryAsync<long>(sql,
            new SugarParameter("@amount", amount),
            new SugarParameter("@now", DateTime.Now),
            new SugarParameter("@id", accountId),
            new SugarParameter("@enabled", (int)PayAccountStatusEnum.Enabled),
            new SugarParameter("@exhausted", (int)PayAccountStatusEnum.Exhausted));
        return ids.Count > 0;
    }

    /// <summary>最小金额：&lt;0 拒；&gt;总额度拒；0=不限制。</summary>
    private static decimal NormalizeMinAmount(decimal minAmount, decimal totalQuota)
    {
        if (minAmount < 0m) throw Oops.Bah("最小金额不能为负");
        if (minAmount > 0m && minAmount > totalQuota)
            throw Oops.Bah("最小金额不能大于总额度");
        return decimal.Round(minAmount, PayConst.AmountScale, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 历史「占用中」残留：无其它非终态订单时回写启用/已用完。匹配已不再依赖 InUse。
    /// </summary>
    [NonAction]
    public async Task ReleaseInUseIfIdleAsync(long accountId)
    {
        if (accountId <= 0) return;

        var hasActive = await _payOrderRep.AsQueryable().AnyAsync(u => u.AccountId == accountId
            && (u.Status == PayOrderStatusEnum.Pending
                || u.Status == PayOrderStatusEnum.Partial
                || u.Status == PayOrderStatusEnum.AwaitingConfirm
                || u.Status == PayOrderStatusEnum.Failed));
        if (hasActive) return;

        var account = await _payAccountRep.GetByIdAsync(accountId);
        if (account == null || account.Status != PayAccountStatusEnum.InUse) return;

        var next = GetRemainingQuota(account) > 0m
            ? PayAccountStatusEnum.Enabled
            : PayAccountStatusEnum.Exhausted;

        await _payAccountRep.AsUpdateable()
            .SetColumns(u => new PayAccount { Status = next, UpdateTime = DateTime.Now })
            .Where(u => u.Id == accountId && u.Status == PayAccountStatusEnum.InUse)
            .ExecuteCommandAsync();
    }

    private static void EnsureStructuredPayload(string type, string payeeName, string accountNo,
        string bankName, string branchName, string qrImageUrl)
    {
        type = (type ?? "").Trim();
        payeeName = (payeeName ?? "").Trim();
        accountNo = (accountNo ?? "").Trim();
        bankName = (bankName ?? "").Trim();
        branchName = (branchName ?? "").Trim();
        qrImageUrl = (qrImageUrl ?? "").Trim();

        if (string.IsNullOrEmpty(payeeName))
            throw Oops.Bah("卖家姓名不能为空");

        if (string.Equals(type, PayConst.TypeBank, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(accountNo)) throw Oops.Bah("银行卡账号不能为空");
            if (string.IsNullOrEmpty(bankName)) throw Oops.Bah("开户银行不能为空");
            if (string.IsNullOrEmpty(branchName)) throw Oops.Bah("支行不能为空");
            return;
        }

        // wxpay / alipay / cloudquickpass 等：姓名 +（账号或二维码）
        if (string.IsNullOrEmpty(accountNo) && string.IsNullOrEmpty(qrImageUrl))
            throw Oops.Oh(ErrorCodeEnum.API_ACCOUNT_PAYLOAD_REQUIRED);
    }

    private static string NormalizeAccountInfo(string accountInfo)
        => (accountInfo ?? "").Trim();

    private static string NormalizeQrImageUrl(string qrImageUrl)
    {
        if (string.IsNullOrWhiteSpace(qrImageUrl)) return "";
        var path = qrImageUrl.Trim().Replace('\\', '/');
        if (!path.StartsWith('/')) path = "/" + path;
        if (path.Length > 512
            || path.Contains("..", StringComparison.Ordinal)
            || !path.StartsWith(QrUrlPrefix, StringComparison.Ordinal))
            throw Oops.Oh(ErrorCodeEnum.API_QR_IMAGE_INVALID);
        return path;
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
