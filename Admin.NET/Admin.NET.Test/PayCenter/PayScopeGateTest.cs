// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Admin.NET.Core.Service;
using Furion;
using Microsoft.AspNetCore.Http;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 开放接口权限范围（scope）隔离（F6.2）
/// </summary>
/// <remarks>
/// <para>
/// 骨架负责「你是谁」（HMAC 签名 + 时间戳 + nonce），本类验证业务补的「你能调哪些接口」：
/// 规则声明（<see cref="PayScopeAttribute"/> / <see cref="PayScopeResolver"/>）与比对策略
/// （<see cref="OpenAccessScopeMatcher"/>），以及 <c>Scopes</c> 经管理接口落库后能否立刻生效。
/// </para>
/// <para>
/// 签名链路本身的失败矩阵（匿名 / 未知 key / 错签名 / nonce 重放）由 HTTP 实测覆盖，
/// 进程内不重复造 HTTP 管道。
/// </para>
/// </remarks>
public class PayScopeGateTest : IDisposable
{
    private readonly ISqlSugarClient _db;
    private readonly SysOpenAccessService _openAccessService;

    /// <summary>本类造数的身份标识前缀，析构时按前缀清理</summary>
    private const string AccessKeyPrefix = "unittest_scope_";

    /// <summary>默认租户 Id（与 <c>SqlSugarConst.DefaultTenantId</c> 一致）</summary>
    private const long DefaultTenantId = 1300000000001;

    /// <summary>已有管理员账号（<c>GetByKey</c> 会关联 <c>SysUser</c>，必须指向真实存在的用户）</summary>
    private const long SuperAdminUserId = 1300000000101;

    public PayScopeGateTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _openAccessService = App.GetRequiredService<SysOpenAccessService>();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        _db.Ado.ExecuteCommand("delete from sysopenaccess where accesskey like @prefix",
            new SugarParameter("@prefix", AccessKeyPrefix + "%"));
    }

    // ─────────────────────────── 比对策略（安全判定的唯一出口） ───────────────────────────

    /// <summary>
    /// scope 比对：逗号分隔、精确匹配（不做前缀/通配）、大小写不敏感、容忍空格；
    /// 未授权任何 scope 时一律拒绝。
    /// </summary>
    [Theory]
    // 未声明所需 scope → 不限制（非收款中心开放接口走这条）
    [InlineData("allocate", null, true)]
    [InlineData("allocate", "", true)]
    [InlineData(null, null, true)]
    // 正常授权
    [InlineData("allocate", "allocate", true)]
    [InlineData("allocate,notify", "notify", true)]
    [InlineData("notify,allocate", "allocate", true)]
    // 大小写与空格容忍
    [InlineData("ALLOCATE", "allocate", true)]
    [InlineData(" allocate , notify ", "notify", true)]
    // 越权拒绝
    [InlineData("allocate", "notify", false)]
    // fail-closed：未配置权限范围 = 未授权任何接口，而不是放行
    [InlineData(null, "allocate", false)]
    [InlineData("", "allocate", false)]
    [InlineData("   ", "allocate", false)]
    // 精确匹配，不做前缀
    [InlineData("alloc", "allocate", false)]
    [InlineData("allocate:extra", "allocate", false)]
    public void Scope比对_应按逗号精确匹配且未配置即拒绝(string grantedScopes, string requiredScope, bool expected)
    {
        Assert.Equal(expected, OpenAccessScopeMatcher.IsGranted(grantedScopes, requiredScope));
    }

    // ─────────────────────────── 规则声明（业务增量） ───────────────────────────

    /// <summary>
    /// 兜底路径判定：端点元数据取不到时，宁可多校验也不能静默放行。
    /// </summary>
    /// <remarks>
    /// ★ 2026-09-17 行为变更：`/api/pay/*` 下**未识别**的动作从返回 <c>null</c>（= 不限制）
    /// 改为返回 <see cref="PayConst.ScopeUnclassified"/>（fail-closed）。
    /// <para>
    /// 原因：返回 <c>null</c> 的语义是「不限制」，于是「新加一个开放接口、忘了写 <c>[PayScope]</c>」
    /// 会**默认敞开**，而且没有任何信号。改成哨兵值后，忘记声明 = 拒绝，
    /// 且 401 报文里会直接出现 `scope=unclassified`，一眼看出是漏了声明而不是密钥配错。
    /// </para>
    /// <para>
    /// 注意「非收款段」仍返回 <c>null</c>：本解析器只对 `/api/pay*` 负责，
    /// 不能把框架其他接口（如 `/api/sysAuth/login`）也一并锁死。
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("/api/pay/allocate", PayConst.ScopeAllocate)]
    [InlineData("/api/pay/status", PayConst.ScopeAllocate)]
    [InlineData("/api/pay/notify", PayConst.ScopeNotify)]
    [InlineData("/api/payNotify/notify", PayConst.ScopeNotify)] // 路由未收敛的历史形态，也要判到
    [InlineData("/api/sysAuth/login", null)] // 非收款中心开放接口，不限制
    [InlineData("/api/pay/unknown", PayConst.ScopeUnclassified)] // ★ 收款段下未识别的动作 → fail-closed
    public void 路径兜底_应按收款段与动作判定所需scope(string path, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        Assert.Equal(expected, new PayScopeResolver().ResolveRequiredScope(context));
    }

    /// <summary>
    /// 接口上的 <see cref="PayScopeAttribute"/> 优先于路径兜底——这样新增接口时
    /// 「权限规则跟着接口走」，不会因为路径规则没同步而漏判。
    /// </summary>
    [Fact]
    public void 接口特性声明_应优先于路径兜底()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/pay/notify"; // 路径兜底本应判为 notify
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new PayScopeAttribute(PayConst.ScopeAllocate)),
            "unittest"));

        Assert.Equal(PayConst.ScopeAllocate, new PayScopeResolver().ResolveRequiredScope(context));
    }

    // ─────────────────────────── Scopes 的维护闭环 ───────────────────────────

    /// <summary>
    /// 后台（前端「开放接口身份」页面所走的同一路径）新增/修改 <c>Scopes</c> 后，
    /// 应立刻落库、并刷新鉴权侧读取用的缓存——否则改了权限要等缓存过期才生效。
    /// </summary>
    [Fact]
    public async Task 开放身份Scopes_经管理接口增改后应落库并立即刷新缓存()
    {
        var accessKey = AccessKeyPrefix + Guid.NewGuid().ToString("N")[..8];

        await _openAccessService.AddOpenAccess(new AddOpenAccessInput
        {
            AccessKey = accessKey,
            AccessSecret = "unittest-secret",
            BindTenantId = DefaultTenantId,
            BindUserId = SuperAdminUserId,
            Scopes = PayConst.ScopeAllocate,
        });

        var added = _db.Queryable<SysOpenAccess>().First(u => u.AccessKey == accessKey);
        Assert.Equal(PayConst.ScopeAllocate, added.Scopes);

        // 首次读取会写入缓存
        Assert.Equal(PayConst.ScopeAllocate, (await _openAccessService.GetByKey(accessKey)).Scopes);

        await _openAccessService.UpdateOpenAccess(new UpdateOpenAccessInput
        {
            Id = added.Id,
            AccessKey = accessKey,
            AccessSecret = "unittest-secret",
            BindTenantId = DefaultTenantId,
            BindUserId = SuperAdminUserId,
            Scopes = $"{PayConst.ScopeAllocate},{PayConst.ScopeNotify}",
        });

        Assert.Equal($"{PayConst.ScopeAllocate},{PayConst.ScopeNotify}",
            _db.Queryable<SysOpenAccess>().First(u => u.Id == added.Id).Scopes);

        // 关键：缓存已被管理接口主动清掉，鉴权侧立刻看到新权限
        Assert.Equal($"{PayConst.ScopeAllocate},{PayConst.ScopeNotify}",
            (await _openAccessService.GetByKey(accessKey)).Scopes);

        // 后台列表页要能展示「权限范围」：Page 用 Select(..., true) 自动补全主表字段，
        // 新增列若没被带出来，前端那一列就会永远空白（且不会报错）
        var page = await _openAccessService.Page(new PageOpenAccessInput { AccessKey = accessKey });
        var listed = page.Items.First(u => u.AccessKey == accessKey);
        Assert.Equal($"{PayConst.ScopeAllocate},{PayConst.ScopeNotify}", listed.Scopes);
    }

    /// <summary>
    /// 清空 <c>Scopes</c> 后，该身份对收款接口应当变成「完全不可用」（fail-closed）。
    /// </summary>
    [Fact]
    public async Task 清空Scopes后_该身份应被拒绝访问收款接口()
    {
        var accessKey = AccessKeyPrefix + Guid.NewGuid().ToString("N")[..8];

        await _openAccessService.AddOpenAccess(new AddOpenAccessInput
        {
            AccessKey = accessKey,
            AccessSecret = "unittest-secret",
            BindTenantId = DefaultTenantId,
            BindUserId = SuperAdminUserId,
            Scopes = PayConst.ScopeAllocate,
        });

        var added = _db.Queryable<SysOpenAccess>().First(u => u.AccessKey == accessKey);
        Assert.True(OpenAccessScopeMatcher.IsGranted(added.Scopes, PayConst.ScopeAllocate));

        await _openAccessService.UpdateOpenAccess(new UpdateOpenAccessInput
        {
            Id = added.Id,
            AccessKey = accessKey,
            AccessSecret = "unittest-secret",
            BindTenantId = DefaultTenantId,
            BindUserId = SuperAdminUserId,
            Scopes = null,
        });

        var after = await _openAccessService.GetByKey(accessKey);
        Assert.Null(after.Scopes);
        Assert.False(OpenAccessScopeMatcher.IsGranted(after.Scopes, PayConst.ScopeAllocate));
    }
}
