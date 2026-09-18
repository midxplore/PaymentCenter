// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 开放接口身份表
/// </summary>
[SugarTable(null, "开放接口身份表")]
[SysTable]
[SugarIndex("i_{table}_a", nameof(AccessKey), OrderByType.Asc)]
public partial class SysOpenAccess : EntityBase
{
    /// <summary>
    /// 身份标识
    /// </summary>
    [SugarColumn(ColumnDescription = "身份标识", Length = 128)]
    [Required, MaxLength(128)]
    public virtual string AccessKey { get; set; }

    /// <summary>
    /// 密钥
    /// </summary>
    [SugarColumn(ColumnDescription = "密钥", Length = 256)]
    [Required, MaxLength(256)]
    public virtual string AccessSecret { get; set; }

    /// <summary>
    /// 权限范围（逗号分隔，如 allocate,notify；留空表示不限制）
    /// </summary>
    /// <remarks>
    /// 用于 F6.2 的「接口权限分离」：同一个接入方的密钥可以只被授权访问部分开放接口。
    /// 由 <see cref="IOpenAccessScopeResolver"/> 声明「当前请求需要什么 scope」，
    /// 在 <c>GetSignatureAuthenticationEventImpl</c> 的 <c>OnValidated</c> 里比对本字段。
    /// </remarks>
    [SugarColumn(ColumnDescription = "权限范围", Length = 128, IsNullable = true)]
    [MaxLength(128)]
    public virtual string Scopes { get; set; }

    /// <summary>
    /// 状态（启用 / 停用）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于**停用而不删除**：密钥泄漏或接入方暂停合作时，把状态置为
    /// <see cref="StatusEnum.Disable"/> 即可立即拒绝该 accessKey 的所有请求，
    /// 而凭证行仍在库里 —— 这样 <c>pay_order.ClientId</c> / <c>pay_audit_log.ClientId</c>
    /// 的引用不会悬空，审计链条完整。
    /// </para>
    /// <para>
    /// 对比：直接删除凭证行会让历史订单/审计记录指向一个不存在的 Id，
    /// 事后追查「这笔钱是哪个接入方发起的」就断了。
    /// </para>
    /// <para>
    /// 校验位置在 <c>SysOpenAccessService.GetSignatureAuthenticationEventImpl()</c> 的
    /// <c>OnGetAccessSecret</c>：停用即返回空密钥，等价于「accessKey 无效」，
    /// 签名校验直接失败，不会进入任何业务逻辑。
    /// </para>
    /// </remarks>
    [SugarColumn(ColumnDescription = "状态")]
    public virtual StatusEnum Status { get; set; } = StatusEnum.Enable;

    /// <summary>
    /// 绑定租户Id
    /// </summary>
    /// <remarks>
    /// ★ 与 <see cref="BindUserId"/> 一样标 <c>virtual</c>：DTO 要能在派生类里
    /// <c>override</c> 它并挂上校验特性。不标 <c>virtual</c> 就只能用 <c>new</c> 遮蔽，
    /// 而 <c>new</c> 会生成**两个各自独立的自动属性后备字段**，
    /// 一旦有人按基类类型访问就会读到另一个字段（值恒为 0），是个隐蔽的坑。
    /// </remarks>
    [SugarColumn(ColumnDescription = "绑定租户Id")]
    public virtual long BindTenantId { get; set; }

    /// <summary>
    /// 绑定租户
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [Navigate(NavigateType.OneToOne, nameof(BindTenantId))]
    public SysTenant BindTenant { get; set; }

    /// <summary>
    /// 绑定用户Id
    /// </summary>
    [SugarColumn(ColumnDescription = "绑定用户Id")]
    public virtual long BindUserId { get; set; }

    /// <summary>
    /// 绑定用户
    /// </summary>
    //[Newtonsoft.Json.JsonIgnore]
    //[System.Text.Json.Serialization.JsonIgnore]
    [Navigate(NavigateType.OneToOne, nameof(BindUserId))]
    public SysUser BindUser { get; set; }
}