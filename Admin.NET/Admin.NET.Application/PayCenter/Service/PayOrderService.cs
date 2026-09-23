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
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;
    private readonly SqlSugarRepository<SysOpenAccess> _openAccessRep;
    private readonly PayNotifyService _payNotifyService;
    private readonly PayWebhookService _payWebhookService;
    private readonly PayAuditService _payAuditService;
    private readonly SysFileService _sysFileService;
    private readonly UserManager _userManager;
    private readonly ISqlSugarClient _db;

    public PayOrderService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        SqlSugarRepository<PayAccount> payAccountRep,
        SqlSugarRepository<SysOpenAccess> openAccessRep,
        PayNotifyService payNotifyService,
        PayWebhookService payWebhookService,
        PayAuditService payAuditService,
        SysFileService sysFileService,
        UserManager userManager,
        ISqlSugarClient db)
    {
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payAccountRep = payAccountRep;
        _openAccessRep = openAccessRep;
        _payNotifyService = payNotifyService;
        _payWebhookService = payWebhookService;
        _payAuditService = payAuditService;
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

        return new SqlSugarPagedList<PayOrderOutput>
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            Total = paged.Total,
            TotalPages = paged.TotalPages,
            HasNextPage = paged.HasNextPage,
            HasPrevPage = paged.HasPrevPage,
            Items = paged.Items.Select(u => ToOutput(u, accountMap.GetValueOrDefault(u.AccountId))).ToList()
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

        var notifies = await _payNotifyRecordRep.AsQueryable()
            .Where(u => u.OrderId == order.Id)
            .OrderBy(u => u.NotifyTime, OrderByType.Asc)
            .ToListAsync();

        return new PayOrderDetailOutput
        {
            Order = ToOutput(order, account),
            Events = events.Select(ToEventOutput).ToList(),
            NotifyRecords = notifies.Select(ToNotifyOutput).ToList()
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
    /// 平台确认到账（须水单；按请求金额差额一次结清为 Completed）
    /// </summary>
    [ApiDescriptionSettings(Name = "ConfirmReceived"), HttpPost]
    [DisplayName("确认到账")]
    public async Task ConfirmReceived(ConfirmReceivedInput input)
    {
        if (input == null || input.Id <= 0) throw Oops.Bah("订单不能为空");
        if (string.IsNullOrWhiteSpace(input.VoucherUrl)) throw Oops.Bah("水单不能为空");

        var voucherUrl = input.VoucherUrl.Trim();
        if (voucherUrl.Length > PayConst.VoucherUrlLength)
            throw Oops.Bah($"水单路径长度不能超过 {PayConst.VoucherUrlLength}");

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            var order = await _payOrderRep.GetByIdAsync(input.Id)
                ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);

            if (order.Status != PayOrderStatusEnum.Pending && order.Status != PayOrderStatusEnum.Partial)
                throw Oops.Oh(ErrorCodeEnum.API_ORDER_STATUS_INVALID, order.Status.GetDescription());

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

            var fresh = await _payOrderRep.GetByIdAsync(order.Id);
            var remarkExtra = string.IsNullOrWhiteSpace(input.Remark) ? "" : $"；备注 {input.Remark.Trim()}";
            await _payAuditService.WriteAsync(PayAuditActionEnum.ConfirmReceived,
                PayConst.AuditTargetTypeOrder, fresh.Id, fresh.OrderNo,
                remark: $"确认到账 {gap:0.00}，水单 {voucherUrl}{remarkExtra}",
                db: _db);

            await _payWebhookService.EnqueueOrderResultAsync(fresh);

            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
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

    private static PayOrderOutput ToOutput(PayOrder order, PayAccount account)
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
            CreateTime = order.CreateTime
        };
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
}

/// <summary>
/// 枚举下拉项（通用）
/// </summary>
public class PayEnumOption
{
    public string Label { get; set; }
    public int Value { get; set; }
}
