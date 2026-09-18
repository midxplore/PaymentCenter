// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 异常到账台账服务（F5）
/// </summary>
/// <remarks>
/// <para>
/// 台账的<b>自动入库</b>在 <see cref="PayNotifyService.Notify"/> 的各个异常分支里完成（F5.1），
/// 本服务只负责<b>人工处置</b>（F5.2 关联 / F5.3 确认无需处理）与台账查询。
/// </para>
/// <para>
/// 本服务属后台管理接口族：不挂签名鉴权，走骨架的 JWT + RBAC（§7 的接口可见性分离）。
/// </para>
/// </remarks>
[ApiDescriptionSettings(Order = 407, Description = "异常到账台账")]
public class PayAbnormalService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayAbnormalReceipt> _payAbnormalReceiptRep;
    private readonly SqlSugarRepository<PayOrder> _payOrderRep;
    private readonly SqlSugarRepository<PayOrderEvent> _payOrderEventRep;
    private readonly SqlSugarRepository<PayNotifyRecord> _payNotifyRecordRep;
    private readonly PayNotifyService _payNotifyService;
    private readonly PayAuditService _payAuditService;
    private readonly UserManager _userManager;
    private readonly ISqlSugarClient _db;

    public PayAbnormalService(SqlSugarRepository<PayAbnormalReceipt> payAbnormalReceiptRep,
        SqlSugarRepository<PayOrder> payOrderRep,
        SqlSugarRepository<PayOrderEvent> payOrderEventRep,
        SqlSugarRepository<PayNotifyRecord> payNotifyRecordRep,
        PayNotifyService payNotifyService,
        PayAuditService payAuditService,
        UserManager userManager,
        ISqlSugarClient db)
    {
        _payAbnormalReceiptRep = payAbnormalReceiptRep;
        _payOrderRep = payOrderRep;
        _payOrderEventRep = payOrderEventRep;
        _payNotifyRecordRep = payNotifyRecordRep;
        _payNotifyService = payNotifyService;
        _payAuditService = payAuditService;
        _userManager = userManager;
        _db = db;
    }

    /// <summary>
    /// 获取异常到账台账分页列表（F5.3）
    /// </summary>
    /// <remarks>不返回 <c>RawBody</c>（原始报文可能很大），需要时按 Id 单独查库。</remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取异常到账台账分页列表")]
    public async Task<SqlSugarPagedList<PayAbnormalOutput>> Page(PagePayAbnormalInput input)
    {
        var paged = await _payAbnormalReceiptRep.AsQueryable()
            .WhereIF(input.HandleStatus.HasValue, u => u.HandleStatus == input.HandleStatus.Value)
            .WhereIF(input.Reason.HasValue, u => u.Reason == input.Reason.Value)
            .WhereIF(input.ClientId.HasValue, u => u.ClientId == input.ClientId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(input.VoucherNo), u => u.VoucherNo == input.VoucherNo)
            .WhereIF(!string.IsNullOrWhiteSpace(input.ReportOrderNo), u => u.ReportOrderNo.Contains(input.ReportOrderNo))
            .WhereIF(input.StartTime.HasValue, u => u.NotifyTime >= input.StartTime.Value)
            .WhereIF(input.EndTime.HasValue, u => u.NotifyTime <= input.EndTime.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .Select<PayAbnormalOutput>()
            .ToPagedListAsync(input.Page, input.PageSize);

        foreach (var item in paged.Items)
        {
            item.ReasonText = item.Reason.GetDescription();
            item.HandleStatusText = item.HandleStatus.GetDescription();
        }
        return paged;
    }

    /// <summary>
    /// 人工关联异常到账到指定订单（F5.2）
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>复用</b> <see cref="PayNotifyService.ApplyReceiptCoreAsync"/>，不另写一套累加逻辑：
    /// 这样「部分到账 / 完成 / 超额」的判定与额度结转与正常到账通知完全一致，
    /// 不会出现「通知走一套、人工补录走另一套」导致口径分叉。
    /// </para>
    /// <para>
    /// 目标订单必须是<b>非终态</b>（待到账 / 部分到账）。订单已过期/已完成的场景，
    /// 正确做法是重新走匹配接口下新单，再把该笔到账关联到新单上——金额终究要落到一个活跃订单里，
    /// 直接改终态订单会破坏「终态不可逆」（§6）。
    /// </para>
    /// <para>台账标记、订单事件、通知记录回填、审计与累加在<b>同一事务</b>内，要么全成要么全不成。</para>
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Link"), HttpPost]
    [DisplayName("人工关联异常到账到订单")]
    public async Task<LinkAbnormalOutput> Link(LinkAbnormalInput input)
    {
        var targetOrderNo = input.TargetOrderNo?.Trim();
        var now = DateTime.Now;
        var operatorName = string.IsNullOrWhiteSpace(_userManager.RealName) ? _userManager.Account : _userManager.RealName;

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            // ── 步骤 1：校验台账记录 ────────────────────────────────────────
            var abnormal = await _payAbnormalReceiptRep.AsQueryable()
                .Where(u => u.Id == input.Id).FirstAsync()
                ?? throw Oops.Oh(ErrorCodeEnum.P1008);
            if (abnormal.HandleStatus != PayHandleStatusEnum.Pending)
                throw Oops.Oh(ErrorCodeEnum.P1009);

            // ── 步骤 2：校验目标订单可接收 ──────────────────────────────────
            var order = await _payOrderRep.AsQueryable()
                .Where(u => u.OrderNo == targetOrderNo).FirstAsync()
                ?? throw Oops.Oh(ErrorCodeEnum.P1004);
            if (order.Status != PayOrderStatusEnum.Pending && order.Status != PayOrderStatusEnum.Partial)
                throw Oops.Oh(ErrorCodeEnum.P1016, order.Status.GetDescription());

            // ── 步骤 3：先记「人工关联」事件（说明这笔金额是被人挂上来的）──────
            await _payOrderEventRep.InsertAsync(new PayOrderEvent
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                EventType = PayEventTypeEnum.ManualLinked,
                FromStatus = order.Status,
                ToStatus = order.Status,
                Amount = abnormal.Amount,
                ReceivedTotal = order.ReceivedAmount,
                Remark = $"人工关联异常到账：凭证号 {abnormal.VoucherNo}，金额 {abnormal.Amount:0.00}，原上报订单号 {abnormal.ReportOrderNo ?? "<空>"}",
                OperatorId = _userManager.UserId,
                OperatorName = operatorName
            });

            // ── 步骤 4：复用 §5.2 的累加逻辑 ────────────────────────────────
            var applied = await _payNotifyService.ApplyReceiptCoreAsync(order, abnormal.Amount);
            if (applied == null)
            {
                // 影响行数为 0：订单在校验之后被并发终结（过期任务 / 另一笔到账推它完成）
                throw Oops.Oh(ErrorCodeEnum.P1016, "已被并发终结");
            }

            // ── 步骤 5：标记台账已处理（条件更新，防重复处置）────────────────
            var handled = await _payAbnormalReceiptRep.AsUpdateable()
                .SetColumns(u => new PayAbnormalReceipt
                {
                    RelatedOrderId = order.Id,
                    RelatedOrderNo = order.OrderNo,
                    HandleStatus = PayHandleStatusEnum.Linked,
                    HandlerId = _userManager.UserId,
                    HandlerName = operatorName,
                    HandleTime = now,
                    HandleRemark = input.HandleRemark,
                    UpdateTime = now
                })
                .Where(u => u.Id == abnormal.Id && u.HandleStatus == PayHandleStatusEnum.Pending)
                .ExecuteCommandAsync();
            if (handled == 0) throw Oops.Oh(ErrorCodeEnum.P1009);

            // ── 步骤 6：回填通知记录「已累加」────────────────────────────────
            // 该凭证号此前是"未累加"（异常入库），现在钱确实入账了，把标记补上。
            // 只改 Applied 这一个派生字段，原始报文与上报订单号保持原样（可追溯）。
            await _payNotifyRecordRep.AsUpdateable()
                .SetColumns(u => new PayNotifyRecord { Applied = true })
                .Where(u => u.ClientId == abnormal.ClientId && u.VoucherNo == abnormal.VoucherNo)
                .ExecuteCommandAsync();

            // ── 步骤 7：审计（与业务变更同事务，避免"改了但没留痕"）─────────
            await _payAuditService.WriteAsync(PayAuditActionEnum.AbnormalHandle, nameof(PayAbnormalReceipt),
                abnormal.Id, abnormal.VoucherNo,
                new { abnormal.HandleStatus },
                new { HandleStatus = PayHandleStatusEnum.Linked, RelatedOrderNo = order.OrderNo },
                $"人工关联到订单 {order.OrderNo}，金额 {abnormal.Amount:0.00}",
                _db);

            tenant.CommitTran();

            return new LinkAbnormalOutput
            {
                OrderNo = applied.OrderNo,
                OrderStatus = applied.Status,
                OrderStatusText = applied.Status.GetDescription(),
                ReceivedAmount = applied.ReceivedAmount,
                RequestAmount = applied.RequestAmount,
                Amount = abnormal.Amount
            };
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
    }

    /// <summary>
    /// 确认异常到账无需处理（F5.3）
    /// </summary>
    /// <remarks>
    /// 仅改状态、不动金额：用于「重复上报」「测试打款」等确认无需入账的场景。
    /// 处理备注建议写明判定依据，便于事后复核。
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Ignore"), HttpPost]
    [DisplayName("确认异常到账无需处理")]
    public async Task Ignore(IgnoreAbnormalInput input)
    {
        var now = DateTime.Now;
        var operatorName = string.IsNullOrWhiteSpace(_userManager.RealName) ? _userManager.Account : _userManager.RealName;

        var tenant = _db.AsTenant();
        tenant.BeginTran();
        try
        {
            var abnormal = await _payAbnormalReceiptRep.AsQueryable()
                .Where(u => u.Id == input.Id).FirstAsync()
                ?? throw Oops.Oh(ErrorCodeEnum.P1008);
            if (abnormal.HandleStatus != PayHandleStatusEnum.Pending)
                throw Oops.Oh(ErrorCodeEnum.P1009);

            var handled = await _payAbnormalReceiptRep.AsUpdateable()
                .SetColumns(u => new PayAbnormalReceipt
                {
                    HandleStatus = PayHandleStatusEnum.Ignored,
                    HandlerId = _userManager.UserId,
                    HandlerName = operatorName,
                    HandleTime = now,
                    HandleRemark = input.HandleRemark,
                    UpdateTime = now
                })
                .Where(u => u.Id == abnormal.Id && u.HandleStatus == PayHandleStatusEnum.Pending)
                .ExecuteCommandAsync();
            if (handled == 0) throw Oops.Oh(ErrorCodeEnum.P1009);

            await _payAuditService.WriteAsync(PayAuditActionEnum.AbnormalHandle, nameof(PayAbnormalReceipt),
                abnormal.Id, abnormal.VoucherNo,
                new { abnormal.HandleStatus },
                new { HandleStatus = PayHandleStatusEnum.Ignored },
                $"确认无需处理：{input.HandleRemark}",
                _db);

            tenant.CommitTran();
        }
        catch
        {
            tenant.RollbackTran();
            throw;
        }
    }
}
