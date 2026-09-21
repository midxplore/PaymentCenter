// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款订单后台查询 / 管理（F7.2）
/// </summary>
/// <remarks>
/// <para>
/// <b>与 <see cref="PayAllocateService"/> 的边界</b>：本服务是<b>后台运营视角</b>，挂 JWT + RBAC；
/// 对外 3 个接口（`/api/pay/*`）挂签名鉴权，在 <see cref="PayAllocateService"/> / <see cref="PayNotifyService"/>。
/// 两者从代码结构上分离，杜绝「后台接口被开放身份越权调用」（F6.2）。
/// </para>
/// <para>
/// <b>只读</b>：本服务不提供任何修改订单的入口。订单状态只能由业务链路推进
/// （到账通知 / 过期任务 / 人工关联），这是 F7.4「流水不可篡改」的组成部分——
/// 后台能查、能导出，但不能手改订单状态与金额。
/// </para>
/// </remarks>
[ApiDescriptionSettings(Order = 408, Description = "收款订单")]
public class PayOrderService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly SqlSugarRepository<PayNotifyRecord> _payNotifyRecordRep;
    private readonly SqlSugarRepository<PayAccount> _payAccountRep;

    public PayOrderService(SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        SqlSugarRepository<PayAccount> payAccountRep)
    {
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payAccountRep = payAccountRep;
    }

    /// <summary>
    /// 获取收款订单分页列表（F7.2）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 传 <c>accountId</c> 即为「账号维度审计」：该账号名下的订单明细（含每笔的金额与状态）。
    /// 账号信息（账号内容 / 类型）通过一次批量查询补齐，避免逐行查库。
    /// </para>
    /// <para>
    /// <b>不要加 <c>[FromQuery]</c></b>：框架的分页查询约定是复杂入参走 POST body
    /// （参见 <c>SysOpenAccessService.Page</c> / <c>SysLogOpService.Page</c>）。
    /// 加了 <c>[FromQuery]</c> 会让 swagger 把输入对象摊平成十几个位置参数，
    /// 前端每次调用都要按顺序传一长串 <c>undefined</c>，且新增查询条件就会改变调用签名。
    /// </para>
    /// </remarks>
    [DisplayName("获取收款订单分页列表")]
    public async Task<SqlSugarPagedList<PayOrderOutput>> Page(PagePayOrderInput input)
    {
        var paged = await _payOrderRep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.OrderNo), u => u.OrderNo.Contains(input.OrderNo))
            .WhereIF(!string.IsNullOrWhiteSpace(input.ExternalNo), u => u.ExternalNo.Contains(input.ExternalNo))
            .WhereIF(input.Status.HasValue, u => u.Status == input.Status.Value)
            .WhereIF(input.AccountId.HasValue, u => u.AccountId == input.AccountId.Value)
            .WhereIF(input.ClientId.HasValue, u => u.ClientId == input.ClientId.Value)
            .WhereIF(input.StartTime.HasValue, u => u.CreateTime >= input.StartTime.Value)
            .WhereIF(input.EndTime.HasValue, u => u.CreateTime <= input.EndTime.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);

        // 补齐账号信息（一次查询覆盖本页所有账号，不做 N+1）
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
    /// <remarks>
    /// 一次给全三张表：订单当前状态 + 事件流水（谁在什么时候把它推到了哪个状态）+ 每次到账明细。
    /// </remarks>
    [DisplayName("获取收款订单详情")]
    public async Task<PayOrderDetailOutput> Detail([FromQuery] BaseIdInput input)
    {
        var order = await _payOrderRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.API_ORDER_NOT_FOUND);

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
    /// 按订单号获取详情（便于排查：三方只给了订单号）
    /// </summary>
    /// <param name="orderNo">系统订单号（精确匹配）</param>
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
    /// 订单实体 → 列表输出
    /// </summary>
    /// <remarks>
    /// 单处实现，保证 <see cref="Page"/> 与 <see cref="Detail"/> 的字段口径完全一致
    /// （两处各写一遍必然随时间漂移）。
    /// </remarks>
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
            CreateTime = order.CreateTime
        };
        FillComputed(output);
        return output;
    }

    /// <summary>
    /// 填充计算字段（不落库、由其他列推导）
    /// </summary>
    private static void FillComputed(PayOrderOutput output)
    {
        // 未达成金额可能是负数（超额到账），如实呈现、不截断为 0——否则对账时看不出多收
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
    /// <summary>
    /// 显示文本
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// 枚举值
    /// </summary>
    public int Value { get; set; }
}
