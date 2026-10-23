// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 收款订单后台查询 / 管理（F7.2）
/// </summary>
/// <remarks>
/// <para>
/// <b>与 <see cref="PayAllocateService"/> 的边界</b>：本服务是<b>后台运营视角</b>，挂 JWT + RBAC；
/// 对外接口（`/api/pay/*`）挂签名鉴权，在 <see cref="PayAllocateService"/>。
/// </para>
/// <para>
/// 平台运营可通过 <see cref="ConfirmReceived"/> 确认到账（须水单）；商户角色按绑定 OpenAccess 隔离订单。
/// </para>
/// </remarks>
[ApiDescriptionSettings(Order = 408, Description = "收款订单")]
public class PayOrderService : IDynamicApiController, ITransient
{
    private const string VoucherAllowSuffix = ".jpg.jpeg.png.bmp.webp.pdf";

    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly SqlSugarRepository<PayNotifyRecord> _payNotifyRecordRep;
    private readonly SqlSugarRepository<PayWebhookOutbox> _payWebhookOutboxRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<PayAuditLog> _payAuditLogRep;
    private readonly SqlSugarRepository<SysOpenAccess> _openAccessRep;
    private readonly PayNotifyService _payNotifyService;
    private readonly PayWebhookService _payWebhookService;
    private readonly PayAuditService _payAuditService;
    private readonly PayAccountService _payAccountService;
    private readonly PayCashierService _payCashierService;
    private readonly SysConfigService _sysConfigService;
    private readonly SysFileService _sysFileService;
    private readonly UserManager _userManager;
    private readonly ISqlSugarClient _db;

    public PayOrderService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        SqlSugarRepository<PayWebhookOutbox> payWebhookOutboxRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<PayAuditLog> payAuditLogRep,
        SqlSugarRepository<SysOpenAccess> openAccessRep,
        PayNotifyService payNotifyService,
        PayWebhookService payWebhookService,
        PayAuditService payAuditService,
        PayAccountService payAccountService,
        PayCashierService payCashierService,
        SysConfigService sysConfigService,
        SysFileService sysFileService,
        UserManager userManager,
        ISqlSugarClient db)
    {
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payWebhookOutboxRep = payWebhookOutboxRep;
        _payAccountRep = payAccountRep;
        _payAuditLogRep = payAuditLogRep;
        _openAccessRep = openAccessRep;
        _payNotifyService = payNotifyService;
        _payWebhookService = payWebhookService;
        _payAuditService = payAuditService;
        _payAccountService = payAccountService;
        _payCashierService = payCashierService;
        _sysConfigService = sysConfigService;
        _sysFileService = sysFileService;
        _userManager = userManager;
        _db = db;
    }

    /// <summary>
    /// 获取收款订单分页列表（F7.2）
    /// </summary>
    [DisplayName("获取收款订单分页列表")]
    public async Task<SqlSugarPagedList<PayOrderOutput>> Page(PagePayOrderInput input)
    {
        var clientIds = await ResolveVisibleClientIdsAsync();
        if (clientIds is { Count: 0 })
            return EmptyPage<PayOrderOutput>(input.Page, input.PageSize);

        if (clientIds != null && input.ClientId.HasValue && !clientIds.Contains(input.ClientId.Value))
            return EmptyPage<PayOrderOutput>(input.Page, input.PageSize);

        var paged = await _payOrderRep.AsQueryable()
            .WhereIF(clientIds != null, u => u.ClientId != null && clientIds.Contains(u.ClientId.Value))
            .WhereIF(!string.IsNullOrWhiteSpace(input.OrderNo), u => u.OrderNo.Contains(input.OrderNo))
            .WhereIF(!string.IsNullOrWhiteSpace(input.ExternalNo), u => u.ExternalNo.Contains(input.ExternalNo))
            .WhereIF(input.Status.HasValue, u => u.Status == input.Status.Value)
            .WhereIF(input.AccountId.HasValue, u => u.AccountId == input.AccountId.Value)
            .WhereIF(input.ClientId.HasValue, u => u.ClientId == input.ClientId.Value)
            .WhereIF(input.StartTime.HasValue, u => u.CreateTime >= input.StartTime.Value)
            .WhereIF(input.EndTime.HasValue, u => u.CreateTime <= input.EndTime.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);

        var accountIds = paged.Items.Select(u => u.AccountId).Where(u => u > 0).Distinct().ToList();
        var accountMap = accountIds.Count == 0
            ? new Dictionary<long, PayAccount>()
            : (await _payAccountRep.AsQueryable().Where(u => accountIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(u => u.Id);

        var items = new List<PayOrderOutput>();
        foreach (var u in paged.Items)
        {
            var output = ToOutput(u, accountMap.GetValueOrDefault(u.AccountId));
            output.WebhookNotified = await HasSuccessfulWebhookForStatusAsync(u.Id, u.Status);
            items.Add(output);
        }

        return new SqlSugarPagedList<PayOrderOutput>
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            Total = paged.Total,
            TotalPages = paged.TotalPages,
            HasNextPage = paged.HasNextPage,
            HasPrevPage = paged.HasPrevPage,
            Items = items
        };
    }

    /// <summary>
    /// 获取收款订单详情（F7.1 全生命周期）
    /// </summary>
    [DisplayName("获取收款订单详情")]
    public async Task<PayOrderDetailOutput> Detail([FromQuery] BaseIdInput input)
    {
        var order = await _payOrderRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
        await EnsureOrderVisibleAsync(order);

        var account = order.AccountId > 0
            ? await _payAccountRep.AsQueryable().Where(u => u.Id == order.AccountId).FirstAsync()
            : null;

        var events = await _payOrderEventRep.AsQueryable()
            .Where(u => u.OrderId == order.Id)
            .OrderBy(u => u.CreateTime, OrderByType.Asc)
            .ToListAsync();

        var webhooks = await _payWebhookOutboxRep.AsQueryable()
            .Where(u => u.OrderId == order.Id)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToListAsync();

        var notifies = await _payNotifyRecordRep.AsQueryable()
            .Where(u => u.OrderId == order.Id)
            .OrderBy(u => u.NotifyTime, OrderByType.Asc)
            .ToListAsync();

        var orderOutput = ToOutput(order, account, includeCashierDebug: true);
        orderOutput.WebhookNotified = await HasSuccessfulWebhookForStatusAsync(order.Id, order.Status);

        var openApiCalls = await _payAuditLogRep.AsQueryable()
            .Where(u => u.TargetType == PayConst.AuditTargetTypeOrder
                && u.TargetId == order.Id
                && (u.Action == PayAuditActionEnum.ApiCall
                    || u.Action == PayAuditActionEnum.ApiAuthFailure))
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .Take(50)
            .ToListAsync();

        return new PayOrderDetailOutput
        {
            Order = orderOutput,
            Events = events.Select(ToEventOutput).ToList(),
            WebhookRecords = webhooks.Select(ToWebhookOutput).ToList(),
            NotifyRecords = notifies.Select(ToNotifyOutput).ToList(),
            OpenApiCalls = openApiCalls.Select(u => new PayOrderOpenApiCallOutput
            {
                Id = u.Id,
                Action = u.Action,
                ActionText = u.Action.GetDescription(),
                ClientKey = u.ClientKey,
                ClientId = u.ClientId,
                OperatorIp = u.OperatorIp,
                Remark = u.Remark,
                CreateTime = u.CreateTime
            }).ToList()
        };
    }

    /// <summary>
    /// 按订单号获取详情
    /// </summary>
    [DisplayName("按订单号获取收款订单详情")]
    public async Task<PayOrderDetailOutput> DetailByNo([FromQuery] string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo)) throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);

        var order = await _payOrderRep.AsQueryable()
            .Where(u => u.OrderNo == orderNo.Trim())
            .FirstAsync() ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);

        return await Detail(new BaseIdInput { Id = order.Id });
    }

    /// <summary>
    /// 上传确认到账水单，返回根相对路径
    /// </summary>
    [ApiDescriptionSettings(Name = "UploadVoucher"), HttpPost]
    [DisplayName("上传到账水单")]
    public async Task<string> UploadVoucher([Required] IFormFile file)
    {
        var sysFile = await _sysFileService.UploadFile(
            new UploadFileInput { File = file, AllowSuffix = VoucherAllowSuffix },
            "upload/pay-voucher");
        var url = sysFile.Url?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url)) throw Oops.Bah("水单上传失败");
        return url.StartsWith('/') ? url : "/" + url.TrimStart('/');
    }

    /// <summary>
    /// 获取到账提示音地址（空则未配置）
    /// </summary>
    [ApiDescriptionSettings(Name = "GetAlertSound"), HttpGet]
    [DisplayName("查询到账提示音")]
    public async Task<string> GetAlertSound()
    {
        var url = (await _sysConfigService.GetConfigValueByCode<string>(PayConst.OrderAlertSoundUrl))?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url)) return "";
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) || url.StartsWith('/')
            ? url
            : "/" + url.TrimStart('/');
    }

    /// <summary>
    /// 平台确认到账（须水单；按请求金额差额一次结清为 Completed）
    /// </summary>
    /// <remarks>
    /// 支持 Pending / Partial / AwaitingConfirm。收银台已交凭证时可用其首张水单。
    /// 确认成功后入队并即时尝试 Webhook；派发失败不回滚到账（可再用「通知商户」重推）。
    /// </remarks>
    [ApiDescriptionSettings(Name = "ConfirmReceived"), HttpPost]
    [DisplayName("确认到账")]
    public async Task<ConfirmReceivedOutput> ConfirmReceived(ConfirmReceivedInput input)
    {
        if (input == null || input.Id <= 0) throw Oops.Bah("订单不能为空");

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        PayOrder fresh;
        try
        {
            var order = await _payOrderRep.GetByIdAsync(input.Id)
                ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
            await EnsureOrderVisibleAsync(order);

            // 正常路径收银台已自动到账；此处保留给异常/补录/历史等待确认/已过期
            if (order.Status != PayOrderStatusEnum.Pending
                && order.Status != PayOrderStatusEnum.Partial
                && order.Status != PayOrderStatusEnum.AwaitingConfirm
                && order.Status != PayOrderStatusEnum.Failed
                && order.Status != PayOrderStatusEnum.Expired)
                throw Oops.Oh(ErrorCodeEnum.API_ORDER_STATUS_INVALID, order.Status.GetDescription());

            // 水单：优先用本次上传；否则回落收银台已传凭证首张
            var voucherUrl = (input.VoucherUrl ?? "").Trim();
            if (string.IsNullOrWhiteSpace(voucherUrl))
            {
                var urls = PayCashierService.ParseVoucherUrls(order.VoucherUrls);
                voucherUrl = urls.Count > 0 ? urls[0] : (order.VoucherUrl ?? "");
            }
            if (string.IsNullOrWhiteSpace(voucherUrl)) throw Oops.Bah("水单不能为空");
            if (voucherUrl.Length > PayConst.VoucherUrlLength)
                throw Oops.Bah($"水单路径长度不能超过 {PayConst.VoucherUrlLength}");

            var gap = order.RequestAmount - order.ReceivedAmount;
            if (gap <= 0)
                throw Oops.Bah("订单已足额到账，无需再确认");

            var applied = await _payNotifyService.ApplyReceiptCoreAsync(order, gap);
            if (applied == null)
                throw Oops.Oh(ErrorCodeEnum.API_ORDER_STATUS_INVALID, "已被并发终结");
            if (applied.Status != PayOrderStatusEnum.Completed)
                throw Oops.Bah("确认到账后订单未进入完成态，请重试");

            var now = DateTime.Now;
            await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    VoucherUrl = voucherUrl,
                    UpdateTime = now
                })
                .Where(u => u.Id == order.Id)
                .ExecuteCommandAsync();

            await _payAccountRep.AsUpdateable()
                .SetColumns(u => new PayAccount
                {
                    LastSuccessTime = now,
                    UpdateTime = now
                })
                .Where(u => u.Id == order.AccountId)
                .ExecuteCommandAsync();

            fresh = await _payOrderRep.GetByIdAsync(order.Id);
            var remarkExtra = string.IsNullOrWhiteSpace(input.Remark) ? "" : $"；备注 {input.Remark.Trim()}";
            await _payAuditService.WriteAsync(PayAuditActionEnum.ConfirmReceived,
                PayConst.AuditTargetTypeOrder, fresh.Id, fresh.OrderNo,
                remark: $"确认到账 {gap:0.00}，水单 {voucherUrl}{remarkExtra}",
                db: _db);

            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }

        // ★ 入队/派发放在提交之后：outbox 失败不得回滚已确认到账
        var webhook = await NotifyMerchantAfterConfirmAsync(fresh);
        return webhook;
    }

    /// <summary>
    /// 运营确认未到账：订单置 Failed（额度仍预占，收银台可再传凭证），Webhook status=Failed
    /// </summary>
    [ApiDescriptionSettings(Name = "MarkUnpaid"), HttpPost]
    [DisplayName("确认未到账")]
    public async Task<ConfirmReceivedOutput> MarkUnpaid(MarkUnpaidInput input)
    {
        if (input == null || input.Id <= 0) throw Oops.Bah("订单不能为空");

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        PayOrder fresh;
        try
        {
            var order = await _payOrderRep.GetByIdAsync(input.Id)
                ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
            await EnsureOrderVisibleAsync(order);

            if (order.Status != PayOrderStatusEnum.Pending
                && order.Status != PayOrderStatusEnum.Partial
                && order.Status != PayOrderStatusEnum.AwaitingConfirm
                && order.Status != PayOrderStatusEnum.Failed)
                throw Oops.Oh(ErrorCodeEnum.API_ORDER_STATUS_INVALID, order.Status.GetDescription());

            if (order.ReceivedAmount > 0m)
                throw Oops.Bah("订单已有到账金额，请走确认到账或异常处理，不能标记未到账");

            var now = DateTime.Now;
            var fromStatus = order.Status;
            var expireMinutes = await _sysConfigService.GetConfigValueByCode<int>(PayConst.OrderExpireMinutes);
            if (expireMinutes <= 0) expireMinutes = PayConst.DefaultOrderExpireMinutes;
            var newExpire = now.AddMinutes(expireMinutes);

            var claimed = await _payOrderRep.AsUpdateable()
                .SetColumns(u => new PayOrder
                {
                    Status = PayOrderStatusEnum.Failed,
                    // 清空凭证，便于付款人按未到账结果重新上传
                    VoucherUrls = null,
                    VoucherUrl = null,
                    CompleteTime = null,
                    ExpireTime = newExpire,
                    UpdateTime = now
                })
                .Where(u => u.Id == order.Id
                    && (u.Status == PayOrderStatusEnum.Pending
                        || u.Status == PayOrderStatusEnum.Partial
                        || u.Status == PayOrderStatusEnum.AwaitingConfirm
                        || u.Status == PayOrderStatusEnum.Failed)
                    && u.ReceivedAmount == 0m)
                .ExecuteCommandAsync();
            if (claimed == 0)
                throw Oops.Oh(ErrorCodeEnum.API_ORDER_STATUS_INVALID, "已被并发变更");

            fresh = await _payOrderRep.GetByIdAsync(order.Id);
            var remarkExtra = string.IsNullOrWhiteSpace(input.Remark) ? "" : $"；{input.Remark.Trim()}";
            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = fresh.Id,
                OrderNo = fresh.OrderNo,
                EventType = PayEventTypeEnum.MarkUnpaid,
                FromStatus = fromStatus,
                ToStatus = PayOrderStatusEnum.Failed,
                Amount = 0m,
                ReceivedTotal = 0m,
                Remark = $"运营确认未到账，通知商户 Failed；额度继续预占，收银台可重新上传凭证{remarkExtra}",
                OperatorId = _userManager.UserId,
                OperatorName = _userManager.RealName
            });

            await _payAuditService.WriteAsync(PayAuditActionEnum.MarkUnpaid,
                PayConst.AuditTargetTypeOrder, fresh.Id, fresh.OrderNo,
                remark: $"确认未到账（Failed），可继续支付{remarkExtra}",
                db: _db);

            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }

        // Failed 通知用 forceNew：每次未到账都可再推一条，便于下游跟踪
        return await NotifyMerchantOrderResultAsync(fresh, forceNew: true);
    }

    /// <summary>
    /// 人工重发订单结果 Webhook（仅已完成 / 未到账）。
    /// 已过期不可人工重推：额度已释放，过期仅在状态变更时自动入队一次。
    /// 已成功通知过也可再推（forceNew 新 eventId），便于到账后重复确认异步回调。
    /// </summary>
    [ApiDescriptionSettings(Name = "ResendWebhook"), HttpPost]
    [DisplayName("重发商户通知")]
    public async Task ResendWebhook(BaseIdInput input)
    {
        if (input == null || input.Id <= 0) throw Oops.Bah("订单不能为空");

        var order = await _payOrderRep.GetByIdAsync(input.Id)
            ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
        await EnsureOrderVisibleAsync(order);

        if (order.Status == PayOrderStatusEnum.Expired)
            throw Oops.Bah("已过期订单额度已释放，不再支持人工通知商户");

        if (order.Status != PayOrderStatusEnum.Completed
            && order.Status != PayOrderStatusEnum.Failed)
            throw Oops.Bah("仅已完成 / 未到账的订单可通知商户");

        if (order.ClientId is null or <= 0)
            throw Oops.Bah("订单无关联开放凭证，无法通知");

        var access = await _openAccessRep.GetByIdAsync(order.ClientId.Value);
        var url = access?.WebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            throw Oops.Bah("该凭证未配置 Webhook 地址，请先在开放接口凭证中填写");

        var alreadyOk = await HasSuccessfulWebhookForStatusAsync(order.Id, order.Status);
        await _payWebhookService.EnqueueOrderResultAsync(order, forceNew: true);
        var ok = await _payWebhookService.TryDispatchLatestPendingAsync(order.Id);

        await _payAuditService.WriteAsync(PayAuditActionEnum.ResendWebhook,
            PayConst.AuditTargetTypeOrder, order.Id, order.OrderNo,
            remark: ok
                ? $"人工{(alreadyOk ? "再次" : "重发")} Webhook 成功（{order.Status}）→ {url}"
                : $"人工{(alreadyOk ? "再次" : "重发")} Webhook 已入队（{order.Status}，即时派发未成功，将按退避重试）→ {url}");

        if (!ok)
            throw Oops.Bah("已入队通知，但即时派发未成功（对方未返回 success）。系统将自动重试，也可稍后再点「再次通知」");
    }

    /// <summary>确认到账后的 Webhook：无地址则跳过；有则入队并即时尝试一次。</summary>
    private Task<ConfirmReceivedOutput> NotifyMerchantAfterConfirmAsync(PayOrder order)
        => NotifyMerchantOrderResultAsync(order, forceNew: false);

    /// <summary>终态/未到账结果通知商户。</summary>
    private async Task<ConfirmReceivedOutput> NotifyMerchantOrderResultAsync(PayOrder order, bool forceNew)
    {
        var output = new ConfirmReceivedOutput
        {
            OrderNo = order.OrderNo,
            Status = order.Status.ToString(),
            WebhookQueued = false,
            WebhookDispatched = false,
            WebhookMessage = "未关联开放凭证，跳过商户通知"
        };

        if (order.ClientId is null or <= 0) return output;

        var access = await _openAccessRep.GetByIdAsync(order.ClientId.Value);
        var url = access?.WebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            output.WebhookMessage = "凭证未配置 Webhook 地址；配置后可在订单上点「重试通知」补推";
            return output;
        }

        await _payWebhookService.EnqueueOrderResultAsync(order, forceNew);
        output.WebhookQueued = true;
        output.WebhookDispatched = await _payWebhookService.TryDispatchLatestPendingAsync(order.Id);
        output.WebhookMessage = output.WebhookDispatched
            ? $"已通知商户（{order.Status}，对方返回 success）"
            : "已入队通知，但即时派发未成功；可稍后点「重试通知」重推，或等待系统自动重试";
        return output;
    }

    private async Task<bool> HasSuccessfulWebhookForStatusAsync(long orderId, PayOrderStatusEnum status)
    {
        var prefix = $"order:{orderId}:result:{status}";
        return await _payWebhookOutboxRep.AsQueryable()
            .AnyAsync(u => u.OrderId == orderId
                && u.Status == PayWebhookStatusEnum.Success
                && (u.EventId == prefix || u.EventId.StartsWith(prefix + ":")));
    }

    /// <summary>
    /// 获取订单状态下拉列表
    /// </summary>
    [DisplayName("获取订单状态下拉列表")]
    public List<PayEnumOption> GetStatusOptions()
    {
        return Enum.GetValues<PayOrderStatusEnum>()
            .Select(u => new PayEnumOption { Label = u.GetDescription(), Value = (int)u })
            .ToList();
    }

    /// <summary>
    /// 超管看全量（null）；绑定了 OpenAccess 的用户只看这些 ClientId；未绑定凭证的平台用户看全量。
    /// </summary>
    private async Task<List<long>> ResolveVisibleClientIdsAsync()
    {
        if (_userManager.SuperAdmin) return null;

        var ids = await _openAccessRep.AsQueryable()
            .Where(u => u.BindUserId == _userManager.UserId)
            .Select(u => u.Id)
            .ToListAsync();

        return ids.Count == 0 ? null : ids;
    }

    private async Task EnsureOrderVisibleAsync(PayOrder order)
    {
        var clientIds = await ResolveVisibleClientIdsAsync();
        if (clientIds == null) return;
        if (order.ClientId == null || !clientIds.Contains(order.ClientId.Value))
            throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);
    }

    private static SqlSugarPagedList<T> EmptyPage<T>(int page, int pageSize) => new()
    {
        Page = page,
        PageSize = pageSize,
        Total = 0,
        TotalPages = 0,
        HasNextPage = false,
        HasPrevPage = false,
        Items = []
    };

    private PayOrderOutput ToOutput(PayOrder order, PayAccount account, bool includeCashierDebug = false)
    {
        var output = new PayOrderOutput
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            ExternalNo = order.ExternalNo,
            AccountId = order.AccountId,
            AccountInfo = account?.AccountInfo,
            QrImageUrl = account?.QrImageUrl ?? "",
            AccountType = account?.Type,
            RequestAmount = order.RequestAmount,
            ReceivedAmount = order.ReceivedAmount,
            Status = order.Status,
            ClientId = order.ClientId,
            ExpireTime = order.ExpireTime,
            CompleteTime = order.CompleteTime,
            OverpayRemark = order.OverpayRemark,
            VoucherUrl = order.VoucherUrl,
            VoucherUrls = PayCashierService.ParseVoucherUrls(order.VoucherUrls),
            CreateTime = order.CreateTime,
            ReturnUrl = order.ReturnUrl,
            ReturnLabel = order.ReturnLabel
        };
        if (includeCashierDebug && !string.IsNullOrWhiteSpace(order.CashierToken))
        {
            try
            {
                output.CashierUrl = _payCashierService.BuildCashierUrl(order.CashierToken);
            }
            catch
            {
                // 未配置公网基址时详情仍可看其它字段
                output.CashierUrl = "";
            }
        }
        FillComputed(output);
        return output;
    }

    private static void FillComputed(PayOrderOutput output)
    {
        output.OutstandingAmount = output.RequestAmount - output.ReceivedAmount;
        output.StatusText = output.Status.GetDescription();
    }

    private static PayOrderEventOutput ToEventOutput(PayOrderEvent e) => new()
    {
        Id = e.Id,
        EventType = e.EventType,
        EventTypeText = e.EventType.GetDescription(),
        FromStatus = e.FromStatus,
        FromStatusText = e.FromStatus?.GetDescription(),
        ToStatus = e.ToStatus,
        ToStatusText = e.ToStatus?.GetDescription(),
        Amount = e.Amount,
        ReceivedTotal = e.ReceivedTotal,
        Remark = e.Remark,
        OperatorId = e.OperatorId,
        OperatorName = e.OperatorName,
        CreateTime = e.CreateTime
    };

    private static PayNotifyRecordOutput ToNotifyOutput(PayNotifyRecord n) => new()
    {
        Id = n.Id,
        Amount = n.Amount,
        NotifyTime = n.NotifyTime,
        VoucherNo = n.VoucherNo,
        ClientId = n.ClientId,
        Applied = n.Applied,
        RawBody = n.RawBody,
        CreateTime = n.CreateTime
    };

    private static PayWebhookOutboxOutput ToWebhookOutput(PayWebhookOutbox w) => new()
    {
        Id = w.Id,
        EventId = w.EventId,
        EventType = w.EventType,
        WebhookUrl = w.WebhookUrl,
        Status = w.Status,
        StatusText = w.Status.GetDescription(),
        AttemptCount = w.AttemptCount,
        NextRetryTime = w.NextRetryTime,
        LastError = w.LastError,
        Payload = w.Payload,
        CreateTime = w.CreateTime
    };
}

/// <summary>
/// 枚举下拉项（通用）
/// </summary>
public class PayEnumOption
{
    public string Label { get; set; }
    public int Value { get; set; }
}
