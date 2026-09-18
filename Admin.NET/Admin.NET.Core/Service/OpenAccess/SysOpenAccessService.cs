// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Security.Claims;
using System.Security.Cryptography;

namespace Admin.NET.Core.Service;

/// <summary>
/// 开放接口身份服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 244, Description = "开放接口")]
public class SysOpenAccessService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<SysOpenAccess> _sysOpenAccessRep;
    private readonly SysCacheService _sysCacheService;

    /// <summary>
    /// 开放接口身份服务构造函数
    /// </summary>
    public SysOpenAccessService(SqlSugarRepository<SysOpenAccess> sysOpenAccessRep,
        SysCacheService sysCacheService)
    {
        _sysOpenAccessRep = sysOpenAccessRep;
        _sysCacheService = sysCacheService;
    }

    /// <summary>
    /// 生成签名 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("生成签名")]
    public GenerateSignatureOutput GenerateSignature(GenerateSignatureInput input)
    {
        // 时间戳
        if (input.Timestamp == 0)
            input.Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();

        // 密钥
        var appSecretByte = Encoding.UTF8.GetBytes(input.AccessSecret);

        // 拼接参数
        var parameter = $"{input.Method.ToString().ToUpper()}&{input.Url}&{input.AccessKey}&{input.Timestamp}&{input.Nonce}";
        // 使用 HMAC-SHA256 协议创建基于哈希的消息身份验证代码 (HMAC)，以appSecretByte 作为密钥，对上面拼接的参数进行计算签名，所得签名进行 Base-64 编码
        using HMAC hmac = new HMACSHA256();
        hmac.Key = appSecretByte;
        var sign = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(parameter)));
        return new GenerateSignatureOutput
        {
            Timestamp = input.Timestamp,
            Signature = sign
        };
    }

    /// <summary>
    /// 获取开放接口身份分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取开放接口身份分页列表")]
    public async Task<SqlSugarPagedList<OpenAccessOutput>> Page(PageOpenAccessInput input)
    {
        return await _sysOpenAccessRep.AsQueryable()
            .LeftJoin<SysUser>((u, a) => u.BindUserId == a.Id)
            .LeftJoin<SysTenant>((u, a, b) => u.BindTenantId == b.Id)
            .LeftJoin<SysOrg>((u, a, b, c) => b.OrgId == c.Id)
            .WhereIF(!string.IsNullOrWhiteSpace(input.AccessKey?.Trim()), (u, a, b, c) => u.AccessKey.Contains(input.AccessKey))
            .Select((u, a, b, c) => new OpenAccessOutput
            {
                BindUserAccount = a.Account,
                BindTenantName = c.Name,
            }, true)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 增加开放接口身份 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    [DisplayName("增加开放接口身份")]
    public async Task AddOpenAccess(AddOpenAccessInput input)
    {
        if (await _sysOpenAccessRep.AsQueryable().AnyAsync(u => u.AccessKey == input.AccessKey && u.Id != input.Id))
            throw Oops.Oh(ErrorCodeEnum.O1000);

        // 列表只回显掩码，防止有人把掩码当密钥复制进来（真密钥是 Base64，不含 *）
        if (OpenAccessSecretMask.LooksLikeMask(input.AccessSecret))
            throw Oops.Bah($"密钥不能包含 {OpenAccessSecretMask.Marker}：该值看起来是列表里的掩码，请点「生成密钥」重新生成");

        var openAccess = input.Adapt<SysOpenAccess>();
        await _sysOpenAccessRep.InsertAsync(openAccess);
    }

    /// <summary>
    /// 更新开放接口身份 🔖
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>密钥语义</b>：列表接口只回显掩码（见 <see cref="OpenAccessSecretMask"/>），
    /// 所以编辑弹窗带回来的 <see cref="UpdateOpenAccessInput.AccessSecret"/> 通常就是掩码。
    /// 此时**保留库中原密钥**，不把掩码写进去；只有提交了掩码之外的其它值才认为是轮换密钥。
    /// </para>
    /// <para>
    /// <b>缓存</b>：按 accessKey 缓存，所以 accessKey 本身被改名时，
    /// 旧 accessKey 的缓存条目也要清 —— 否则旧 key 在缓存过期前仍然能通过认证。
    /// </para>
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新开放接口身份")]
    public async Task UpdateOpenAccess(UpdateOpenAccessInput input)
    {
        if (await _sysOpenAccessRep.AsQueryable().AnyAsync(u => u.AccessKey == input.AccessKey && u.Id != input.Id))
            throw Oops.Oh(ErrorCodeEnum.O1000);

        var existing = await _sysOpenAccessRep.GetByIdAsync(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        var openAccess = input.Adapt<SysOpenAccess>();

        // ★ Status 可空 = 「不修改」。不要写成 `input.Status ?? StatusEnum.Enable` ——
        //   那会让「只改 scopes、没带 status」的请求**静默把已停用的凭证重新启用**。
        //   停用是密钥泄漏时的止血手段，不能存在任何「不显式声明就被关掉」的路径。
        openAccess.Status = input.Status ?? existing.Status;

        if (OpenAccessSecretMask.MeansUnchanged(input.AccessSecret, existing.AccessSecret))
        {
            // 提交的是空值或本行掩码 → 不修改密钥
            openAccess.AccessSecret = existing.AccessSecret;
        }
        else if (OpenAccessSecretMask.LooksLikeMask(input.AccessSecret))
        {
            // 像掩码但不是本行的掩码：多半是前端把别的行/陈旧的值带过来了，直接拒绝而不是存进去
            throw Oops.Bah($"密钥不能包含 {OpenAccessSecretMask.Marker}：请点「生成密钥」重新生成，或留空表示不修改");
        }

        _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);
        if (!string.Equals(existing.AccessKey, openAccess.AccessKey, StringComparison.Ordinal))
            _sysCacheService.Remove(CacheConst.KeyOpenAccess + existing.AccessKey);

        await _sysOpenAccessRep.UpdateAsync(openAccess);
    }

    /// <summary>
    /// 删除开放接口身份 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    [DisplayName("删除开放接口身份")]
    public async Task DeleteOpenAccess(DeleteOpenAccessInput input)
    {
        var openAccess = await _sysOpenAccessRep.GetByIdAsync(input.Id);
        if (openAccess != null)
            _sysCacheService.Remove(CacheConst.KeyOpenAccess + openAccess.AccessKey);

        await _sysOpenAccessRep.DeleteByIdAsync(input.Id);
    }

    /// <summary>
    /// 创建密钥 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("创建密钥")]
    public async Task<string> CreateSecret()
    {
        return await Task.FromResult(Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..^2]);
    }

    /// <summary>
    /// 根据 Key 获取对象
    /// </summary>
    /// <param name="accessKey"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<SysOpenAccess> GetByKey(string accessKey)
    {
        return await Task.FromResult(
            _sysCacheService.GetOrAdd(CacheConst.KeyOpenAccess + accessKey, _ =>
            {
                return _sysOpenAccessRep.AsQueryable()
                    .Includes(u => u.BindUser)
                    .Includes(u => u.BindUser, p => p.SysOrg)
                    .First(u => u.AccessKey == accessKey);
            })
        );
    }

    /// <summary>
    /// Signature 身份验证事件默认实现
    /// </summary>
    [NonAction]
    public static SignatureAuthenticationEvent GetSignatureAuthenticationEventImpl()
    {
        return new SignatureAuthenticationEvent
        {
            OnGetAccessSecret = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<SysOpenAccessService>>();
                try
                {
                    var openAccessService = context.HttpContext.RequestServices.GetRequiredService<SysOpenAccessService>();
                    var openAccess = openAccessService.GetByKey(context.AccessKey).GetAwaiter().GetResult();
                    if (openAccess == null) return Task.FromResult("");

                    // ── 状态闸门（停用即失效）────────────────────────────
                    // 返回空密钥 = 让签名校验以「accessKey 无效」失败，
                    // 与「凭证不存在」对外**不可区分** —— 不向未授权方泄漏
                    // 「这个 accessKey 存在但被停用了」这一信息。
                    // 停用是「吊销」的主路径：不删行，历史订单/审计的 ClientId 引用不会悬空。
                    if (openAccess.Status != StatusEnum.Enable)
                    {
                        logger.LogWarning("开放接口凭证已停用，拒绝访问：accessKey={AccessKey}｜status={Status}",
                            context.AccessKey, openAccess.Status);
                        return Task.FromResult("");
                    }

                    return Task.FromResult(openAccess.AccessSecret);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "开放接口身份验证");
                    return Task.FromResult("");
                }
            },
            OnValidated = context =>
            {
                var httpContext = context.HttpContext;
                var openAccessService = httpContext.RequestServices.GetRequiredService<SysOpenAccessService>();
                var openAccess = openAccessService.GetByKey(context.AccessKey).GetAwaiter().GetResult();
                var identity = ((ClaimsIdentity)context.Principal!.Identity!);

                identity.AddClaims(
                [
                    new Claim(ClaimConst.UserId, openAccess.BindUserId + ""),
                    new Claim(ClaimConst.TenantId, openAccess.BindTenantId + ""),
                    new Claim(ClaimConst.Account, openAccess.BindUser.Account + ""),
                    new Claim(ClaimConst.RealName, openAccess.BindUser.RealName),
                    new Claim(ClaimConst.AccountType, ((int)openAccess.BindUser.AccountType).ToString()),
                    new Claim(ClaimConst.OrgId, openAccess.BindUser.OrgId + ""),
                    new Claim(ClaimConst.OrgName, openAccess.BindUser.SysOrg?.Name + ""),
                    new Claim(ClaimConst.OrgType, openAccess.BindUser.SysOrg?.Type + ""),
                    new Claim(ClaimConst.TokenVersion, openAccess.BindUser.TokenVersion + ""),
                ]);

                // 把已校验的身份透出给业务层，业务不必再按 accessKey 反查一次库
                httpContext.Items[SignatureAuthenticationDefaults.OpenAccessItemKey] = openAccess;

                // ── 权限范围（scope）校验（F6.2）──────────────────────────────
                // 骨架只负责「问业务需要什么 scope」，规则由业务实现 IOpenAccessScopeResolver 提供。
                // 未注册实现时 requiredScope 为 null，等同于不限制。
                var requiredScope = httpContext.RequestServices
                    .GetServices<IOpenAccessScopeResolver>()
                    .Select(u => u.ResolveRequiredScope(httpContext))
                    .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));

                if (!OpenAccessScopeMatcher.IsGranted(openAccess.Scopes, requiredScope))
                {
                    var message = string.IsNullOrWhiteSpace(openAccess.Scopes)
                        ? $"accessKey 未配置权限范围，拒绝访问（需要 scope={requiredScope}）"
                        : $"accessKey 无权访问该接口（需要 scope={requiredScope}，已授权 {openAccess.Scopes}）";

                    // 复用骨架既有的失败消息通道：让 401 响应带上具体原因
                    // （AdminNETResultProvider 会读取 AuthenticateFailMsgKey 作为响应 message）
                    httpContext.Items[SignatureAuthenticationDefaults.AuthenticateFailMsgKey] = message;
                    context.Fail(message);
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                // ── 签名失败告警（F6.5）────────────────────────────────────
                // 骨架里并没有现成的「签名失败日志」可复用：
                // AdminNETResultProvider 只负责把失败原因写进 401 响应体；
                // 而 401 发生在 MVC 之前，LoggingMonitor（SysLogOp）根本采集不到。
                // 所以在这里补一条告警——签名校验失败是安全事件，必须留痕，
                // 否则密钥被爆破、或接入方签名算法写错，都只能在响应体里一闪而过。
                // 走 OnChallenge 而不是新写中间件：失败原因（含 scope 越权）已经在这里了。
                if (context.AuthenticateFailure == null) return;

                var httpContext = context.HttpContext;
                var logger = httpContext.RequestServices.GetRequiredService<ILogger<SysOpenAccessService>>();

                var accessKey = httpContext.Request.Headers["accessKey"].FirstOrDefault() ?? "";
                var method = httpContext.Request.Method;
                var path = httpContext.Request.Path.Value;
                var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

                // 1) 文本日志：面向**实时告警**（日志聚合规则能立刻命中「签名校验失败」）
                logger.LogWarning(
                    "开放接口签名校验失败：{Reason}｜accessKey={AccessKey}｜{Method} {Path}｜remoteIp={RemoteIp}",
                    context.AuthenticateFailure.Message,
                    string.IsNullOrEmpty(accessKey) ? "<空>" : accessKey,
                    method,
                    path,
                    remoteIp);

                // 2) 审计落库：面向**事后检索与对账**
                //    文本日志会轮转、无法按接入方检索、也无法和订单/审计表关联，
                //    所以再由业务模块把这条失败事实写进自己的审计表（pay_audit_log）。
                //    骨架只负责「广播」，落哪张表、留哪些字段由业务决定。
                var auditSinks = httpContext.RequestServices.GetServices<IOpenAccessAuditSink>();
                if (!auditSinks.Any()) return;

                var auditContext = new OpenAccessAuthFailureContext
                {
                    HttpContext = httpContext,
                    AccessKey = accessKey,
                    Reason = context.AuthenticateFailure.Message,
                    Method = method,
                    Path = path,
                    RemoteIp = remoteIp
                };

                foreach (var sink in auditSinks)
                {
                    // ★ 逐个 try/catch：审计失败**绝不能**把 401 变成 500。
                    //   认证的可用性优先于审计的完整性——审计写不进去要能被发现（Error 日志），
                    //   但不能因此让所有接入方的正常请求一起失败。
                    try
                    {
                        await sink.OnAuthenticationFailedAsync(auditContext);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "开放接口认证失败审计写入异常：accessKey={AccessKey}｜{Method} {Path}",
                            accessKey, method, path);
                    }
                }
            }
        };
    }
}