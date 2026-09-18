// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Admin.NET.Application;

/// <summary>
/// 业务操作审计服务（F7.3）
/// </summary>
/// <remarks>
/// 只增不改：本服务**只提供写入与查询**，不提供任何 update / delete 入口，
/// 保证审计流水不可篡改。写入方法为 <see cref="WriteAsync"/>，标记 <see cref="NonActionAttribute"/>
/// 不暴露为 HTTP 接口，只在业务事务内被调用。
/// </remarks>
[ApiDescriptionSettings(Order = 405, Description = "业务审计")]
public class PayAuditService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<PayAuditLog> _payAuditLogRep;
    private readonly UserManager _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PayAuditService(SqlSugarRepository<PayAuditLog> payAuditLogRep,
        UserManager userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _payAuditLogRep = payAuditLogRep;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 追加一条审计记录（只增不改）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该方法是本表**唯一**的写入入口，不暴露为接口。
    /// 若在业务事务内调用，请传入已有的 <paramref name="db"/> 以保证与业务变更同事务提交。
    /// </para>
    /// <para>
    /// 开放接口发起的动作请改用 <see cref="WriteOpenApiAsync"/>：它会自动补上调用方身份，
    /// 并且把「操作人」正确置为系统（开放接口没有登录用户）。
    /// </para>
    /// </remarks>
    /// <param name="action">审计动作</param>
    /// <param name="targetType">目标类型（Account / Order / Abnormal）</param>
    /// <param name="targetId">目标主键</param>
    /// <param name="targetNo">目标业务标识（账号类型 / 订单号等）</param>
    /// <param name="before">变更前对象快照（可为 null）</param>
    /// <param name="after">变更后对象快照（可为 null）</param>
    /// <param name="remark">说明</param>
    /// <param name="db">可选：复用调用方的事务</param>
    /// <param name="clientId">调用方Id（开放接口动作；后台动作为 null）</param>
    /// <param name="clientKey">调用方身份标识（开放接口动作；后台动作为 null）</param>
    [NonAction]
    public async Task WriteAsync(PayAuditActionEnum action,
        string targetType,
        long targetId,
        string targetNo = null,
        object before = null,
        object after = null,
        string remark = null,
        ISqlSugarClient db = null,
        long? clientId = null,
        string clientKey = null)
    {
        var isOpenApi = clientId.HasValue || !string.IsNullOrWhiteSpace(clientKey);

        var log = new PayAuditLog
        {
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetNo = targetNo,
            BeforeJson = before == null ? null : JsonSerializer.Serialize(before, SnapshotJsonOptions),
            AfterJson = after == null ? null : JsonSerializer.Serialize(after, SnapshotJsonOptions),
            Remark = remark,
            ClientId = clientId,
            ClientKey = clientKey,
            // 开放接口没有登录用户：操作人记为 0（系统），身份由 ClientId/ClientKey 表达。
            // 不能沿用 _userManager —— 签名鉴权虽然也会写入 UserId 声明，
            // 但那是「凭证绑定的用户」，与「谁在调用」不是一回事，混用会让审计误导。
            OperatorId = isOpenApi ? 0 : _userManager.UserId,
            OperatorName = isOpenApi
                ? PayConst.OpenApiOperatorName
                : (string.IsNullOrWhiteSpace(_userManager.RealName) ? _userManager.Account : _userManager.RealName),
            OperatorIp = _httpContextAccessor.HttpContext?.GetRemoteIpAddressToIPv4(true)
        };

        if (db != null)
            await db.Insertable(log).ExecuteCommandAsync();
        else
            await _payAuditLogRep.InsertAsync(log);
    }

    /// <summary>
    /// 追加一条「开放接口调用」审计（资金相关动作）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 调用方身份取自签名鉴权**已经校验过**的身份
    /// （框架在 <c>OnValidated</c> 时写入 <c>HttpContext.Items</c>），
    /// 而不是自己去读 <c>accessKey</c> 请求头再查一次库 —— 那等于把已校验的身份重新推导一遍。
    /// </para>
    /// <para>
    /// 若在业务事务内调用（推荐），传 <paramref name="db"/> 让审计与业务变更**同事务提交**：
    /// 资金变动与「谁发起的」必须同生共死，不能出现「订单落了库但审计没落」的缺口。
    /// </para>
    /// </remarks>
    /// <param name="action">审计动作（ApiCall / ApiAuthFailure）</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="targetId">目标主键</param>
    /// <param name="targetNo">目标业务标识（订单号等）</param>
    /// <param name="remark">说明</param>
    /// <param name="db">可选：复用调用方的事务</param>
    [NonAction]
    public async Task WriteOpenApiAsync(PayAuditActionEnum action,
        string targetType,
        long targetId,
        string targetNo,
        string remark,
        ISqlSugarClient db = null)
    {
        var openAccess = _httpContextAccessor.HttpContext?.Items[SignatureAuthenticationDefaults.OpenAccessItemKey] as SysOpenAccess;
        await WriteAsync(action, targetType, targetId, targetNo, null, null, remark, db,
            openAccess?.Id, openAccess?.AccessKey);
    }

    /// <summary>
    /// 获取业务审计分页列表（F7.3）
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取业务审计分页列表")]
    public async Task<SqlSugarPagedList<PayAuditLog>> Page(PagePayAuditLogInput input)
    {
        return await _payAuditLogRep.AsQueryable()
            .WhereIF(input.Action.HasValue, u => u.Action == input.Action.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(input.TargetType), u => u.TargetType == input.TargetType)
            .WhereIF(input.TargetId.HasValue, u => u.TargetId == input.TargetId.Value)
            .WhereIF(!string.IsNullOrWhiteSpace(input.TargetNo), u => u.TargetNo.Contains(input.TargetNo))
            .WhereIF(!string.IsNullOrWhiteSpace(input.OperatorName), u => u.OperatorName.Contains(input.OperatorName))
            .WhereIF(input.StartTime.HasValue, u => u.CreateTime >= input.StartTime.Value)
            .WhereIF(input.EndTime.HasValue, u => u.CreateTime <= input.EndTime.Value)
            .OrderBy(u => u.CreateTime, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);
    }
}
