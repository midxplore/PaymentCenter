// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Admin.NET.Application;

/// <summary>
/// 收银台配置（Application/Configuration/PayCenter.json）
/// </summary>
public class PayCenterOptions
{
    /// <summary>收银台对外 Base URL（无尾斜杠）；生产必配</summary>
    public string CashierPublicBaseUrl { get; set; }
}

/// <summary>
/// 匿名收银台（token 门禁）
/// </summary>
public class PayCashierService : ITransient
{
    private static readonly HashSet<string> VoucherExts = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".pdf" };

    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly PayAccountService _payAccountService;
    private readonly PayAuditService _payAuditService;
    private readonly PayWebhookService _payWebhookService;
    private readonly PayNotifyService _payNotifyService;
    private readonly SysFileService _sysFileService;
    private readonly SysOnlineUserService _sysOnlineUserService;
    private readonly ISqlSugarClient _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PayCashierService> _logger;

    public PayCashierService(
        SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        PayAccountService payAccountService,
        PayAuditService payAuditService,
        PayWebhookService payWebhookService,
        PayNotifyService payNotifyService,
        SysFileService sysFileService,
        SysOnlineUserService sysOnlineUserService,
        ISqlSugarClient db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PayCashierService> logger)
    {
        _payOrderRep = payOrderRep;
        _payAccountRep = payAccountRep;
        _payOrderEventRep = payOrderEventRep;
        _payAccountService = payAccountService;
        _payAuditService = payAuditService;
        _payWebhookService = payWebhookService;
        _payNotifyService = payNotifyService;
        _sysFileService = sysFileService;
        _sysOnlineUserService = sysOnlineUserService;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private PayCenterOptions Options => App.GetConfig<PayCenterOptions>("PayCenter", true) ?? new PayCenterOptions();

    /// <summary>生成 URL-safe token（32 字节 → 43 字符）</summary>
    public static string NewCashierToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public string BuildCashierUrl(string token)
    {
        var baseUrl = (Options.CashierPublicBaseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // 仅 Development 允许回落到当前请求 Host；生产未配则 fail-closed
            var isDev = string.Equals(App.HostEnvironment?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);
            if (isDev)
            {
                var req = _httpContextAccessor.HttpContext?.Request;
                if (req != null)
                    baseUrl = $"{req.Scheme}://{req.Host.Value}".TrimEnd('/');
            }
        }
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw Oops.Bah("未配置收银台地址 PayCenter:CashierPublicBaseUrl");
        // 前端 createWebHashHistory：路径必须带 /#/c/...，否则落到登录页
        return $"{baseUrl}/#/c/{token}";
    }

    public async Task EnsureCashierTokenAsync(PayOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.CashierToken)) return;

        for (var i = 0; i < 3; i++)
        {
            var token = NewCashierToken();
            var rows = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder { CashierToken = token, UpdateTime = DateTime.Now })
                .Where(u => u.Id == order.Id && (u.CashierToken == null || u.CashierToken == ""))
                .ExecuteCommandAsync();
            if (rows > 0)
            {
                order.CashierToken = token;
                return;
            }

            var fresh = await _payOrderRep.GetByIdAsync(order.Id);
            if (!string.IsNullOrWhiteSpace(fresh?.CashierToken))
            {
                order.CashierToken = fresh.CashierToken;
                return;
            }
        }

        throw Oops.Bah("收银台令牌生成失败，请重试");
    }

    public async Task<CashierOrderOutput> GetAsync(string token)
    {
        var order = await RequireOrderAsync(token);
        var account = await _payAccountRep.GetByIdAsync(order.AccountId);
        return ToOutput(order, account);
    }

    /// <param name="replace">立即清空订单凭证再写入（旧行为；重传请用 draft + Submit 带 URL）</param>
    /// <param name="draft">只落盘返回 URL，不改订单凭证列表</param>
    public async Task<string> UploadVoucherAsync(string token, IFormFile file, bool replace = false, bool draft = false)
    {
        var order = await RequireOrderAsync(token);
        if (!IsCashierUploadable(order.Status))
            throw Oops.Bah("当前状态不可上传凭证");
        // 已过期仍允许重传凭证；其它状态须未过墙钟过期
        if (order.Status != PayOrderStatusEnum.Expired && order.ExpireTime <= DateTime.Now)
            throw Oops.Bah("订单已过期");

        ValidateUpload(file);

        var prefix = VoucherPathPrefix(order.CashierToken ?? token);
        var sysFile = await _sysFileService.UploadFile(
            new UploadFileInput { File = file, AllowSuffix = ".jpg.jpeg.png.pdf" },
            $"upload/pay-voucher/{prefix}");

        var url = NormalizeUrl(sysFile.Url);
        if (string.IsNullOrEmpty(url)) throw Oops.Bah("上传失败");

        // 草稿：文件已存，订单凭证仍保留旧图，待确认提交再替换
        if (draft) return url;

        // 行锁后再合并 JSON，避免并发冲破张数上限
        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            await _db.Ado.ExecuteCommandAsync(
                "SELECT id FROM pay_order WHERE id = @id FOR UPDATE",
                new SugarParameter("@id", order.Id));

            var locked = await _payOrderRep.GetByIdAsync(order.Id)
                ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
            if (!IsCashierUploadable(locked.Status))
                throw Oops.Bah("当前状态不可上传凭证");

            var urls = replace ? new List<string>() : ParseVoucherUrls(locked.VoucherUrls);
            if (urls.Count >= PayConst.CashierVoucherMaxCount)
                throw Oops.Bah($"最多上传 {PayConst.CashierVoucherMaxCount} 张凭证");

            urls.Add(url);
            var json = JsonSerializer.Serialize(urls);
            var first = urls[0];
            await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    VoucherUrls = json,
                    VoucherUrl = first,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.Id == order.Id
                    && (u.Status == PayOrderStatusEnum.Pending
                        || u.Status == PayOrderStatusEnum.Failed
                        || u.Status == PayOrderStatusEnum.AwaitingConfirm
                        || u.Status == PayOrderStatusEnum.Expired))
                .ExecuteCommandAsync();

            tenant.CommitTran();
            return url;
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
    }

    public async Task<CashierSubmitOutput> SubmitAsync(string token, CashierSubmitInput input = null)
    {
        var order = await RequireOrderAsync(token);
        if (!IsCashierUploadable(order.Status))
            throw Oops.Bah("当前状态不可提交");
        if (order.Status != PayOrderStatusEnum.Expired && order.ExpireTime <= DateTime.Now)
            throw Oops.Bah("订单已过期");

        List<string> urls;
        var replaceOnSubmit = input?.VoucherUrls != null && input.VoucherUrls.Count > 0;
        if (replaceOnSubmit)
            urls = NormalizeSubmitUrls(order, input.VoucherUrls);
        else
        {
            urls = ParseVoucherUrls(order.VoucherUrls);
            if (urls.Count == 0) throw Oops.Bah("请先上传付款凭证");
        }

        var now = DateTime.Now;
        var fromStatus = order.Status;
        var json = JsonSerializer.Serialize(urls);
        var first = urls[0];
        var amount = order.RequestAmount;

        // 过期单：先尝试重锁额度；失败则记异常台账，订单保持 Expired
        if (fromStatus == PayOrderStatusEnum.Expired)
        {
            var relocked = await _payAccountService.TryRelockQuotaOnAccountAsync(order.AccountId, amount);
            if (!relocked)
            {
                await PersistVouchersOnExpiredAsync(order.Id, json, first, now);
                var clientId = order.ClientId ?? 0L;
                var voucherNo = $"EXP-REUPLOAD-{order.OrderNo}-{now:yyyyMMddHHmmss}";
                await _payNotifyService.WriteAbnormalAsync(
                    clientId, order.OrderNo, amount, now, voucherNo,
                    PayAbnormalReasonEnum.ExpiredReuploadNoQuota,
                    $"{{\"orderNo\":\"{order.OrderNo}\",\"reason\":\"quota_conflict\"}}");
                return new CashierSubmitOutput
                {
                    Status = nameof(PayOrderStatusEnum.Expired),
                    StatusText = "已过期（待人工）",
                    Paid = false,
                    Message = "凭证已提交，但收款额度已被占用，请等待人工处理"
                };
            }
        }

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        PayOrder fresh;
        try
        {
            await _db.Ado.ExecuteCommandAsync(
                "SELECT id FROM pay_order WHERE id = @id FOR UPDATE",
                new SugarParameter("@id", order.Id));

            var locked = await _payOrderRep.GetByIdAsync(order.Id)
                ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
            if (!IsCashierUploadable(locked.Status))
                throw Oops.Bah("提交失败，订单状态已变更");

            var rows = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    Status = PayOrderStatusEnum.Completed,
                    ReceivedAmount = amount,
                    VoucherUrls = json,
                    VoucherUrl = first,
                    CompleteTime = now,
                    UpdateTime = now
                })
                .Where(u => u.Id == order.Id
                    && (u.Status == PayOrderStatusEnum.Pending
                        || u.Status == PayOrderStatusEnum.Failed
                        || u.Status == PayOrderStatusEnum.AwaitingConfirm
                        || u.Status == PayOrderStatusEnum.Expired))
                .ExecuteCommandAsync();
            if (rows == 0) throw Oops.Bah("提交失败，订单状态已变更");

            // 有预占：Locked→Used；无预占（极端）只加 Used
            var moved = await _payAccountRep.AsUpdateable()
                .SetColumns(u => new PayAccount
                {
                    UsedQuota = u.UsedQuota + amount,
                    LockedQuota = u.LockedQuota - amount,
                    LastSuccessTime = now,
                    UpdateTime = now
                })
                .Where(u => u.Id == locked.AccountId && u.LockedQuota >= amount)
                .ExecuteCommandAsync();
            if (moved == 0)
            {
                await _payAccountRep.AsUpdateable()
                    .SetColumns(u => new PayAccount
                    {
                        UsedQuota = u.UsedQuota + amount,
                        LastSuccessTime = now,
                        UpdateTime = now
                    })
                    .Where(u => u.Id == locked.AccountId)
                    .ExecuteCommandAsync();
            }

            await _payAccountService.SyncStatusByQuotaAsync(locked.AccountId);
            await _payAccountService.ReleaseInUseIfIdleAsync(locked.AccountId);

            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = locked.Id,
                OrderNo = locked.OrderNo,
                EventType = PayEventTypeEnum.Completed,
                FromStatus = fromStatus,
                ToStatus = PayOrderStatusEnum.Completed,
                Amount = amount,
                ReceivedTotal = amount,
                Remark = fromStatus == PayOrderStatusEnum.Expired
                    ? $"收银台过期重传凭证到账 {urls.Count} 张"
                    : $"收银台提交凭证自动到账 {urls.Count} 张",
                OperatorId = null,
                OperatorName = "收银台"
            });

            fresh = await _payOrderRep.GetByIdAsync(order.Id);
            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            // 过期重锁在事务外，落单失败须吐回刚锁的额度
            if (fromStatus == PayOrderStatusEnum.Expired)
                await _payAccountService.ReleaseLockedQuotaAsync(order.AccountId, amount);
            throw;
        }

        await _payWebhookService.EnqueueOrderResultAsync(fresh, forceNew: false);
        await _payWebhookService.TryDispatchLatestPendingAsync(fresh.Id);
        await NotifyOpsPaidAsync(fresh);

        return new CashierSubmitOutput
        {
            Status = nameof(PayOrderStatusEnum.Completed),
            StatusText = "已完成",
            Paid = true,
            Message = "已确认到账"
        };
    }

    private async Task PersistVouchersOnExpiredAsync(long orderId, string json, string first, DateTime now)
    {
        await _payOrderRep.AsUpdateable()
            .SetColumns(u => new PayOrder
            {
                VoucherUrls = json,
                VoucherUrl = first,
                UpdateTime = now
            })
            .Where(u => u.Id == orderId && u.Status == PayOrderStatusEnum.Expired)
            .ExecuteCommandAsync();
    }

    private async Task NotifyOpsPaidAsync(PayOrder order)
    {
        try
        {
            await _sysOnlineUserService.BroadcastPayOrderPaidAsync(new PayOrderPaidAlert
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                ExternalNo = order.ExternalNo ?? "",
                RequestAmount = order.RequestAmount.ToString("0.00"),
                Title = "收款到账",
                Message = $"订单 {order.OrderNo} 已到账 {order.RequestAmount:0.00}，点击查看"
            });
        }
        catch (Exception ex)
        {
            // 提醒失败不影响主流程，但要落日志便于排障「到账成功却无弹窗」
            _logger.LogWarning(ex, "收银台到账提醒推送失败 OrderNo={OrderNo}", order.OrderNo);
        }
    }

    /// <summary>提交时替换用的 URL：张数、本单上传目录前缀校验</summary>
    private List<string> NormalizeSubmitUrls(PayOrder order, List<string> raw)
    {
        var urls = (raw ?? [])
            .Select(NormalizeUrl)
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (urls.Count == 0) throw Oops.Bah("请先上传付款凭证");
        if (urls.Count > PayConst.CashierVoucherMaxCount)
            throw Oops.Bah($"最多上传 {PayConst.CashierVoucherMaxCount} 张凭证");

        var prefix = VoucherPathPrefix(order.CashierToken ?? "");
        var needle = $"/upload/pay-voucher/{prefix}/";
        foreach (var u in urls)
        {
            if (u.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                throw Oops.Bah("凭证无效，请重新上传后再提交");
        }
        return urls;
    }

    private static string VoucherPathPrefix(string token)
    {
        var prefix = (token ?? "").Trim();
        if (prefix.Length > 8) prefix = prefix[..8];
        return prefix;
    }

    public async Task CancelAsync(string token)
    {
        var order = await RequireOrderAsync(token);
        if (!IsCashierPayable(order.Status))
            throw Oops.Bah("当前状态不可取消");

        var now = DateTime.Now;
        var fromStatus = order.Status;
        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            var rows = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    Status = PayOrderStatusEnum.Expired,
                    CompleteTime = now,
                    UpdateTime = now
                })
                .Where(u => u.Id == order.Id
                    && (u.Status == PayOrderStatusEnum.Pending || u.Status == PayOrderStatusEnum.Failed))
                .ExecuteCommandAsync();
            if (rows == 0) throw Oops.Bah("取消失败，订单状态已变更");

            var locked = order.RequestAmount - order.ReceivedAmount;
            if (locked > 0)
                await ReleaseQuotaInTranAsync(order.AccountId, locked);

            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                EventType = PayEventTypeEnum.Expired,
                FromStatus = fromStatus,
                ToStatus = PayOrderStatusEnum.Expired,
                Amount = 0m,
                ReceivedTotal = order.ReceivedAmount,
                Remark = "收银台取消订单",
                OperatorId = null,
                OperatorName = "收银台"
            });

            await _payAuditService.WriteAsync(PayAuditActionEnum.ApiCall,
                PayConst.AuditTargetTypeOrder, order.Id, order.OrderNo,
                remark: "收银台取消订单",
                db: _db);

            var fresh = await _payOrderRep.GetByIdAsync(order.Id);
            tenant.CommitTran();
            await _payWebhookService.EnqueueOrderResultAsync(fresh);
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
    }

    private async Task ReleaseQuotaInTranAsync(long accountId, decimal amount)
    {
        await _db.Ado.ExecuteCommandAsync(
            @"UPDATE pay_account SET
                lockedquota = lockedquota - @amount,
                updatetime = @now
              WHERE id = @id AND lockedquota >= @amount",
            new SugarParameter("@amount", amount),
            new SugarParameter("@id", accountId),
            new SugarParameter("@now", DateTime.Now));
        await _payAccountService.SyncStatusByQuotaAsync(accountId);
        await _payAccountService.ReleaseInUseIfIdleAsync(accountId);
    }

    private static bool IsCashierPayable(PayOrderStatusEnum status)
        => status is PayOrderStatusEnum.Pending or PayOrderStatusEnum.Failed;

    private static bool IsCashierUploadable(PayOrderStatusEnum status)
        => status is PayOrderStatusEnum.Pending
            or PayOrderStatusEnum.Failed
            or PayOrderStatusEnum.AwaitingConfirm
            or PayOrderStatusEnum.Expired;

    private async Task<PayOrder> RequireOrderAsync(string token)
    {
        token = (token ?? "").Trim();
        if (token.Length < 16 || token.Length > PayConst.CashierTokenLength)
            throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);

        var order = await _payOrderRep.AsQueryable()
            .FirstAsync(u => u.CashierToken == token);
        if (order == null) throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
        return order;
    }

    private CashierOrderOutput ToOutput(PayOrder order, PayAccount account)
    {
        var remain = (int)Math.Max(0, (order.ExpireTime - DateTime.Now).TotalSeconds);
        var step = order.Status switch
        {
            PayOrderStatusEnum.Completed => 3,
            PayOrderStatusEnum.AwaitingConfirm => 2,
            PayOrderStatusEnum.Expired => ParseVoucherUrls(order.VoucherUrls).Count > 0 ? 2 : 1,
            PayOrderStatusEnum.Failed => 1,
            _ => 1
        };
        var canPay = IsCashierPayable(order.Status) && remain > 0;
        // 已过期仍可重传凭证；其它可上传态须未超时
        var canUpload = IsCashierUploadable(order.Status)
            && (order.Status == PayOrderStatusEnum.Expired || remain > 0);
        var accountNo = !string.IsNullOrWhiteSpace(account?.AccountNo)
            ? account.AccountNo
            : (account?.AccountInfo ?? "");
        // 警示文案取自收款账号备注（可含受限 HTML）；出库再消毒一次
        var notice = PayHtmlSanitize.Sanitize(account?.Remark);

        return new CashierOrderOutput
        {
            OrderNo = order.OrderNo,
            Type = account?.Type ?? "",
            Step = step,
            Status = order.Status,
            StatusText = order.Status.GetDescription(),
            RequestAmount = order.RequestAmount,
            ExpireTime = order.ExpireTime,
            RemainSeconds = remain,
            CanPay = canPay,
            CanCancel = canPay,
            CanUpload = canUpload,
            PayeeName = account?.PayeeName ?? "",
            AccountNo = accountNo,
            BankName = account?.BankName ?? "",
            BranchName = account?.BranchName ?? "",
            QrImageUrl = string.IsNullOrEmpty(account?.QrImageUrl) ? "" : account.QrImageUrl,
            VoucherUrls = ParseVoucherUrls(order.VoucherUrls),
            Notice = notice,
            ReturnUrl = order.ReturnUrl ?? "",
            ReturnLabel = string.IsNullOrWhiteSpace(order.ReturnLabel)
                ? PayConst.DefaultReturnLabel
                : order.ReturnLabel.Trim()
        };
    }

    public static List<string> ParseVoucherUrls(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void ValidateUpload(IFormFile file)
    {
        if (file == null || file.Length <= 0) throw Oops.Bah("请选择文件");
        if (file.Length > PayConst.CashierVoucherMaxBytes)
            throw Oops.Bah("单文件不能超过 3MB");
        var ext = Path.GetExtension(file.FileName ?? "");
        if (!VoucherExts.Contains(ext))
            throw Oops.Bah("仅支持 jpg / png / pdf");

        using var stream = file.OpenReadStream();
        var header = new byte[8];
        var read = stream.Read(header, 0, header.Length);
        if (read < 4) throw Oops.Bah("文件无效");
        if (!(IsJpeg(header) || IsPng(header) || IsPdf(header)))
            throw Oops.Bah("文件内容与扩展名不符");
    }

    private static bool IsJpeg(byte[] b) => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;
    private static bool IsPng(byte[] b) => b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;
    private static bool IsPdf(byte[] b) => b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        url = url.Trim();
        return url.StartsWith('/') ? url : "/" + url.TrimStart('/');
    }
}
