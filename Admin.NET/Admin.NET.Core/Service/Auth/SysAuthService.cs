// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Furion.SpecificationDocument;
using Lazy.Captcha.Core;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统登录授权服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 500, Description = "登录授权")]
[AppApiDescription("登录授权")]
public class SysAuthService : IDynamicApiController, ITransient
{
    private readonly UserManager _userManager;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;
    private readonly SysConfigService _sysConfigService;
    private readonly SysCacheService _sysCacheService;
    private readonly SysTenantService _sysTenantService;
    private readonly SysTotpService _sysTotpService;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICaptcha _captcha;
    private readonly IEventPublisher _eventPublisher;

    public SysAuthService(UserManager userManager,
        SqlSugarRepository<SysUser> sysUserRep,
        SysConfigService sysConfigService,
        SysCacheService sysCacheService,
        SysTenantService sysTenantService,
        SysTotpService sysTotpService,
        IHttpContextAccessor httpContextAccessor,
        ICaptcha captcha,
        IEventPublisher eventPublisher)
    {
        _userManager = userManager;
        _sysUserRep = sysUserRep;
        _sysConfigService = sysConfigService;
        _sysCacheService = sysCacheService;
        _sysTenantService = sysTenantService;
        _sysTotpService = sysTotpService;
        _httpContextAccessor = httpContextAccessor;
        _captcha = captcha;
        _eventPublisher = eventPublisher;
    }

    /// <summary>
    /// 账号密码登录 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("账号密码登录")]
    [AllowAnonymous]
    public virtual async Task<LoginOutput> Login([Required] LoginInput input)
    {
        // 判断密码错误次数（缓存30分钟）
        var keyPasswordErrorTimes = $"{CacheConst.KeyPasswordErrorTimes}{input.Account}";
        var passwordErrorTimes = _sysCacheService.Get<int>(keyPasswordErrorTimes);
        var passwordMaxErrorTimes = await _sysConfigService.GetConfigValueByCode<int>(ConfigConst.SysPasswordMaxErrorTimes);
        // 若未配置或误配置为0、负数, 则默认密码错误次数最大为5次
        if (passwordMaxErrorTimes < 1) passwordMaxErrorTimes = 5;
        if (passwordErrorTimes > passwordMaxErrorTimes) throw Oops.Oh(ErrorCodeEnum.D1027);

        // 判断是否开启验证码并校验
        input.TenantId = input.TenantId <= 0 ? SqlSugarConst.DefaultTenantId : input.TenantId;
        var tenant = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant)?.FirstOrDefault(u => u.Id == input.TenantId);
        if (tenant == null)
        {
            await App.GetRequiredService<SysTenantService>().CacheTenant(); // 重新生成租户列表缓存
            tenant = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant)?.FirstOrDefault(u => u.Id == input.TenantId);
            if (tenant == null) throw Oops.Oh(ErrorCodeEnum.D0007);
        }
        if (tenant.Captcha == true && !_captcha.Validate(input.CodeId.ToString(), input.Code))
            throw Oops.Oh(ErrorCodeEnum.D0008);

        // 获取并验证账号
        var user = await GetLoginUser(input.TenantId, account: input.Account);

        // 是否开启域登录验证
        if (await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysDomainLogin))
        {
            var userLdap = await _sysUserRep.ChangeRepository<SqlSugarRepository<SysUserLdap>>().GetFirstAsync(u => u.UserId == user.Id && u.TenantId == user.TenantId);
            if (userLdap == null)
            {
                VerifyPassword(input.Password, keyPasswordErrorTimes, passwordErrorTimes, user);
            }
            else if (!await App.GetRequiredService<SysLdapService>().AuthAccount(user.TenantId, userLdap.Account, CryptogramHelper.Decrypt(input.Password)))
            {
                _sysCacheService.Set(keyPasswordErrorTimes, ++passwordErrorTimes, TimeSpan.FromMinutes(30));
                throw Oops.Oh(ErrorCodeEnum.D1000);
            }
        }
        else
            VerifyPassword(input.Password, keyPasswordErrorTimes, passwordErrorTimes, user);

        // 登录成功则清空密码错误次数
        _sysCacheService.Remove(keyPasswordErrorTimes);

        return await CompleteTotpOrChallenge(user, input.TotpCode);
    }

    /// <summary>
    /// 获取登录用户
    /// </summary>
    /// <param name="tenantId"></param>
    /// <param name="account"></param>
    /// <param name="phone"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<SysUser> GetLoginUser(long tenantId, string account = null, string phone = null)
    {
        //// 若没有传值租户Id，则从请求页URL参数中获取租户Id（空则默认租户）
        //if (tenantId < 1)
        //{
        //    var tenantidStr = _httpContextAccessor.HttpContext.Request.Query["tenantid"].ToString();
        //    tenantId = string.IsNullOrWhiteSpace(tenantidStr) ? SqlSugarConst.DefaultTenantId : long.Parse(tenantidStr);
        //}

        // 判断账号是否存在
        var user = await _sysUserRep.AsQueryable().Includes(u => u.SysOrg).ClearFilter<ITenantIdFilter>()
            //.WhereIF(tenantId > 0, u => u.TenantId == tenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(account), u => u.Account.Equals(account))
            .WhereIF(!string.IsNullOrWhiteSpace(phone), u => u.Phone.Equals(phone))
            .FirstAsync();
        _ = user ?? throw Oops.Oh(ErrorCodeEnum.D0009);

        // 判断账号是否被冻结
        if (user.Status != StatusEnum.Enable) throw Oops.Oh(ErrorCodeEnum.D1017);

        // 判断租户是否存在及状态
        var tenant = await _sysUserRep.ChangeRepository<SqlSugarRepository<SysTenant>>().GetFirstAsync(u => u.Id == user.TenantId);
        if (tenant?.Status != StatusEnum.Enable) throw Oops.Oh(ErrorCodeEnum.Z1003);

        return user;
    }

    /// <summary>
    /// 根据域名获取租户ID 🔖
    /// </summary>
    /// <param name="hostname"></param>
    /// <returns></returns>
    [DisplayName("根据域名获取租户ID")]
    [AllowAnonymous]
    public long GetTenantIdByHostname(string hostname)
    {
        var tenant = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant).FirstOrDefault(u => u.Host == hostname);
        return tenant != null ? tenant.Id : 0;
    }

    /// <summary>
    /// 验证用户密码
    /// </summary>
    /// <param name="password"></param>
    /// <param name="keyPasswordErrorTimes"></param>
    /// <param name="passwordErrorTimes"></param>
    /// <param name="user"></param>
    private void VerifyPassword(string password, string keyPasswordErrorTimes, int passwordErrorTimes, SysUser user)
    {
        // 国密SM2解密（前端密码传输SM2加密后的）
        try
        {
            password = CryptogramHelper.SM2Decrypt(password);
        }
        catch
        {
            throw Oops.Oh(ErrorCodeEnum.D0010);
        }

        if (CryptogramHelper.CryptoType == CryptogramEnum.MD5.ToString())
        {
            if (user.Password.Equals(MD5Encryption.Encrypt(password))) return;
        }
        else
        {
            if (CryptogramHelper.Decrypt(user.Password).Equals(password)) return;
        }

        _sysCacheService.Set(keyPasswordErrorTimes, ++passwordErrorTimes, TimeSpan.FromMinutes(30));
        throw Oops.Oh(ErrorCodeEnum.D1000);
    }

    /// <summary>
    /// 验证锁屏密码 🔖
    /// </summary>
    /// <param name="password"></param>
    /// <returns></returns>
    [DisplayName("验证锁屏密码")]
    public virtual async Task<bool> UnLockScreen([Required, FromQuery] string password)
    {
        // 账号是否存在
        var user = await _sysUserRep.GetFirstAsync(u => u.Id == _userManager.UserId);
        _ = user ?? throw Oops.Oh(ErrorCodeEnum.D0009);

        var keyPasswordErrorTimes = $"{CacheConst.KeyPasswordErrorTimes}{user.Account}";
        var passwordErrorTimes = _sysCacheService.Get<int>(keyPasswordErrorTimes);

        // 是否开启域登录验证
        if (await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysDomainLogin))
        {
            var userLdap = await _sysUserRep.ChangeRepository<SqlSugarRepository<SysUserLdap>>().GetFirstAsync(u => u.UserId == user.Id && u.TenantId == user.TenantId);
            if (userLdap == null)
            {
                VerifyPassword(password, keyPasswordErrorTimes, passwordErrorTimes, user);
            }
            else if (!await App.GetRequiredService<SysLdapService>().AuthAccount(user.TenantId!.Value, userLdap.Account, CryptogramHelper.Decrypt(password)))
            {
                _sysCacheService.Set(keyPasswordErrorTimes, ++passwordErrorTimes, TimeSpan.FromMinutes(30));
                throw Oops.Oh(ErrorCodeEnum.D1000);
            }
        }
        else
            VerifyPassword(password, keyPasswordErrorTimes, passwordErrorTimes, user);

        return true;
    }

    /// <summary>
    /// 手机号登录 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("手机号登录")]
    [AllowAnonymous]
    public virtual async Task<LoginOutput> LoginPhone([Required] LoginPhoneInput input)
    {
        // 校验短信验证码
        App.GetRequiredService<SysSmsService>().VerifyCode(new SmsVerifyCodeInput { Phone = input.Phone, Code = input.Code });

        // 获取并验证账号
        var user = await GetLoginUser(input.TenantId, phone: input.Phone);

        return await CompleteTotpOrChallenge(user, input.TotpCode);
    }

    /// <summary>
    /// 获取 TOTP 绑定二维码（持登录中间票）🔖
    /// </summary>
    [AllowAnonymous]
    [DisplayName("TOTP绑定二维码")]
    public async Task<TotpSetupOutput> TotpSetup([Required] TotpTicketInput input)
    {
        var userId = _sysTotpService.RequireUserId(input.TotpTicket);
        var user = await _sysUserRep.GetByIdAsync(userId) ?? throw Oops.Oh(ErrorCodeEnum.D0009);
        if (user.TotpEnabled)
            throw Oops.Bah("该账号已绑定动态口令，请直接输入验证码");

        var secret = _sysTotpService.GetOrCreateSetupSecret(input.TotpTicket);
        var url = _sysTotpService.BuildOtpAuthUrl(user.Account, secret);
        return new TotpSetupOutput
        {
            Account = user.Account,
            ManualKey = secret,
            QrCodeImage = _sysTotpService.BuildQrPngBase64(url),
            Issuer = SysTotpService.Issuer
        };
    }

    /// <summary>
    /// 确认 TOTP 绑定（扫码后输入首个 6 位码）🔖
    /// </summary>
    [AllowAnonymous]
    [DisplayName("确认TOTP绑定")]
    public async Task<LoginOutput> TotpConfirmSetup([Required] TotpCodeInput input)
    {
        var maxTimes = await _sysConfigService.GetConfigValueByCode<int>(ConfigConst.SysPasswordMaxErrorTimes);
        _sysTotpService.EnsureNotLocked(input.TotpTicket, maxTimes);

        var userId = _sysTotpService.RequireUserId(input.TotpTicket);
        var user = await _sysUserRep.AsQueryable().Includes(u => u.SysOrg).FirstAsync(u => u.Id == userId)
            ?? throw Oops.Oh(ErrorCodeEnum.D0009);
        if (user.TotpEnabled)
            throw Oops.Bah("该账号已绑定动态口令");

        var setupSecret = _sysCacheService.Get<string>($"{CacheConst.KeyTotpSetupSecret}{input.TotpTicket}");
        if (string.IsNullOrWhiteSpace(setupSecret))
            throw Oops.Bah("请先获取绑定二维码");

        if (!_sysTotpService.VerifyCode(setupSecret, input.Code))
        {
            _sysTotpService.RecordError(input.TotpTicket);
            throw Oops.Bah("动态码不正确");
        }

        user.TotpSecret = _sysTotpService.EncryptSecret(setupSecret);
        user.TotpEnabled = true;
        await _sysUserRep.AsUpdateable(user).UpdateColumns(u => new { u.TotpSecret, u.TotpEnabled }).ExecuteCommandAsync();

        _sysTotpService.ClearError(input.TotpTicket);
        _sysTotpService.RemoveTicket(input.TotpTicket);
        return await CreateToken(user);
    }

    /// <summary>
    /// 登录 TOTP 验码 🔖
    /// </summary>
    [AllowAnonymous]
    [DisplayName("TOTP验码登录")]
    public async Task<LoginOutput> TotpVerify([Required] TotpCodeInput input)
    {
        var maxTimes = await _sysConfigService.GetConfigValueByCode<int>(ConfigConst.SysPasswordMaxErrorTimes);
        _sysTotpService.EnsureNotLocked(input.TotpTicket, maxTimes);

        var userId = _sysTotpService.RequireUserId(input.TotpTicket);
        var user = await _sysUserRep.AsQueryable().Includes(u => u.SysOrg).FirstAsync(u => u.Id == userId)
            ?? throw Oops.Oh(ErrorCodeEnum.D0009);
        if (!user.TotpEnabled || string.IsNullOrWhiteSpace(user.TotpSecret))
            throw Oops.Bah("该账号尚未绑定动态口令");

        string plain;
        try
        {
            plain = _sysTotpService.DecryptSecret(user.TotpSecret);
        }
        catch
        {
            throw Oops.Bah("动态口令数据异常，请联系管理员重置");
        }

        if (!_sysTotpService.VerifyCode(plain, input.Code))
        {
            _sysTotpService.RecordError(input.TotpTicket);
            throw Oops.Bah("动态码不正确");
        }

        _sysTotpService.ClearError(input.TotpTicket);
        _sysTotpService.RemoveTicket(input.TotpTicket);
        return await CreateToken(user);
    }

    /// <summary>
    /// 密码/短信校验通过后：
    /// - 未绑定 → 发票走 Setup；
    /// - 已绑定且带了动态码 → 同屏验码发 JWT；
    /// - 已绑定未带码 → 发票走 Verify（前端聚焦验码框）。
    /// </summary>
    [NonAction]
    public async Task<LoginOutput> CompleteTotpOrChallenge(SysUser user, string totpCode)
    {
        if (!user.TotpEnabled)
        {
            return new LoginOutput
            {
                NeedTotpSetup = true,
                TotpTicket = _sysTotpService.IssueTicket(user.Id)
            };
        }

        var code = (totpCode ?? "").Trim();
        if (string.IsNullOrEmpty(code))
        {
            return new LoginOutput
            {
                NeedTotpVerify = true,
                TotpTicket = _sysTotpService.IssueTicket(user.Id)
            };
        }

        if (string.IsNullOrWhiteSpace(user.TotpSecret))
            throw Oops.Bah("动态口令数据异常，请联系管理员重置");

        string plain;
        try
        {
            plain = _sysTotpService.DecryptSecret(user.TotpSecret);
        }
        catch
        {
            throw Oops.Bah("动态口令数据异常，请联系管理员重置");
        }

        if (!_sysTotpService.VerifyCode(plain, code))
            throw Oops.Bah("动态码不正确");

        return await CreateToken(user);
    }

    /// <summary>兼容旧调用：仅发挑战票</summary>
    [NonAction]
    public LoginOutput BuildTotpChallenge(SysUser user)
        => CompleteTotpOrChallenge(user, null).GetAwaiter().GetResult();

    /// <summary>
    /// 生成Token令牌 🔖
    /// </summary>
    /// <param name="user"></param>
    /// <param name="loginMode"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<LoginOutput> CreateToken(SysUser user, LoginModeEnum loginMode = LoginModeEnum.PC)
    {
        // 单用户登录
        await App.GetRequiredService<SysOnlineUserService>().SingleLogin(user.Id, loginMode);

        // 生成Token令牌
        var tokenExpire = await _sysConfigService.GetTokenExpire();
        var accessToken = JWTEncryption.Encrypt(new Dictionary<string, object>
        {
            { ClaimConst.UserId, user.Id },
            { ClaimConst.TenantId, user.TenantId },
            { ClaimConst.Account, user.Account },
            { ClaimConst.RealName, user.RealName },
            { ClaimConst.AccountType, user.AccountType },
            { ClaimConst.OrgId, user.OrgId },
            { ClaimConst.OrgName, user.SysOrg?.Name },
            { ClaimConst.OrgType, user.SysOrg?.Type },
            { ClaimConst.OrgLevel, user.SysOrg?.Level },
            { ClaimConst.TokenVersion, user.TokenVersion },
        }, tokenExpire);

        // 生成刷新Token令牌
        var refreshTokenExpire = await _sysConfigService.GetRefreshTokenExpire();
        var refreshToken = JWTEncryption.GenerateRefreshToken(accessToken, refreshTokenExpire);

        // 设置响应报文头
        _httpContextAccessor.HttpContext.SetTokensOfResponseHeaders(accessToken, refreshToken);

        // Swagger Knife4UI-AfterScript登录脚本
        // ke.global.setAllHeader('Authorization', 'Bearer ' + ke.response.headers['access-token']);

        // 更新用户登录信息
        user.LastLoginIp = _httpContextAccessor.HttpContext.GetRemoteIpAddressToIPv4(true);
        (user.LastLoginAddress, double? _, double? _) = CommonHelper.GetIpAddress(user.LastLoginIp);
        user.LastLoginTime = DateTime.Now;
        user.LastLoginDevice = CommonHelper.GetClientDeviceInfo(_httpContextAccessor.HttpContext?.Request.Headers.UserAgent);
        await _sysUserRep.AsUpdateable(user).UpdateColumns(u => new
        {
            u.LastLoginIp,
            u.LastLoginAddress,
            u.LastLoginTime,
            u.LastLoginDevice,
        }).ExecuteCommandAsync();

        // 缓存用户Token版本
        _sysCacheService.Set($"{CacheConst.KeyUserToken}{user.Id}", $"{user.TokenVersion}");

        // 发布系统登录事件
        await _eventPublisher.PublishAsync(UserEventTypeEnum.Login, user);

        return new LoginOutput
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    /// <summary>
    /// 获取登录账号 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取登录账号")]
    public virtual async Task<LoginUserOutput> GetUserInfo()
    {
        var user = await _sysUserRep.AsQueryable().Includes(u => u.SysOrg).FirstAsync(u => u.Id == _userManager.UserId) ?? throw Oops.Oh(ErrorCodeEnum.D1011).StatusCode(401);
        // 角色集合
        var roles = await App.GetRequiredService<SysUserRoleService>().GetUserRoleInfoList(user.Id);
        // 接口集合
        var apis = (await App.GetRequiredService<SysRoleService>().GetUserApiList())[0];
        // 水印文字（若为空则不显示）
        var watermarkText = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant)?.FirstOrDefault(u => u.Id == user.TenantId).Watermark;
        return new LoginUserOutput
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Account = user.Account,
            RealName = user.RealName,
            Phone = user.Phone,
            IdCardNum = user.IdCardNum,
            Email = user.Email,
            AccountType = user.AccountType,
            Avatar = user.Avatar,
            Address = user.Address,
            Signature = user.Signature,
            OrgId = user.OrgId,
            OrgName = user.SysOrg?.Name,
            OrgType = user.SysOrg?.Type,
            Apis = apis,
            Roles = roles,
            WatermarkText = string.IsNullOrWhiteSpace(watermarkText) ? watermarkText : watermarkText + $"-{user.RealName}",
            LastChangePasswordTime = user.LastChangePasswordTime
        };
    }

    /// <summary>
    /// 退出系统 🔖
    /// </summary>
    [DisplayName("退出系统")]
    public async Task Logout()
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw Oops.Oh(ErrorCodeEnum.D1016);

        var accessToken = httpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(_userManager.Account))
            throw Oops.Oh(ErrorCodeEnum.D1011);

        // 写入Token黑名单
        var tokenExpire = await _sysConfigService.GetTokenExpire();
        _sysCacheService.Set($"{CacheConst.KeyTokenBlacklist}:{accessToken}", _userManager.Account, TimeSpan.FromMinutes(tokenExpire));

        // 发布系统退出事件
        await _eventPublisher.PublishAsync(UserEventTypeEnum.Logout, _userManager);

        // 退出Swagger/设置无效Token响应头
        _httpContextAccessor.HttpContext.SignoutToSwagger();
    }

    /// <summary>
    /// 获取验证码 🔖
    /// </summary>
    /// <returns></returns>
    [SuppressMonitor]
    [DisplayName("获取验证码")]
    [AllowAnonymous]
    public CaptchaOutput GetCaptcha()
    {
        var codeId = YitIdHelper.NextId().ToString();
        var captcha = _captcha.Generate(codeId);
        var expirySeconds = App.GetOptions<CaptchaOptions>()?.ExpirySeconds ?? 60;
        return new CaptchaOutput { Id = codeId, Img = captcha.Base64, ExpirySeconds = expirySeconds };
    }

    /// <summary>
    /// Swagger登录检查 🔖
    /// </summary>
    /// <returns></returns>
    [Route("/api/swagger/checkUrl")]
    [ApiDescriptionSettings(Description = "Swagger登录检查", DisableInherite = true)]
    [AllowAnonymous]
    public int SwaggerCheckUrl()
    {
        return _httpContextAccessor.HttpContext!.User.Identity!.IsAuthenticated ? 200 : 401;
    }

    /// <summary>
    /// Swagger登录提交 🔖
    /// </summary>
    /// <param name="auth"></param>
    /// <returns></returns>
    [Route("/api/swagger/submitUrl"), NonUnify]
    [ApiDescriptionSettings(Description = "Swagger登录提交", DisableInherite = true)]
    [AllowAnonymous]
    public async Task<int> SwaggerSubmitUrl([FromForm] SpecificationAuth auth)
    {
        try
        {
            // 关闭默认租户验证码验证
            var tenantList = _sysCacheService.Get<List<SysTenant>>(CacheConst.KeyTenant);
            var tenant = tenantList.FirstOrDefault(u => u.Id == SqlSugarConst.DefaultTenantId);
            var tmpCaptcha = tenant!.Captcha;
            tenant.Captcha = false;
            _sysCacheService.Set(CacheConst.KeyTenant, tenantList);

            await Login(new LoginInput
            {
                Account = auth.UserName,
                Password = CryptogramHelper.SM2Encrypt(auth.Password),
                TenantId = SqlSugarConst.DefaultTenantId
            });

            // 恢复默认租户验证码状态
            tenant.Captcha = tmpCaptcha;
            _sysCacheService.Set(CacheConst.KeyTenant, tenantList);

            return 200;
        }
        catch (Exception)
        {
            return 401;
        }
    }
}