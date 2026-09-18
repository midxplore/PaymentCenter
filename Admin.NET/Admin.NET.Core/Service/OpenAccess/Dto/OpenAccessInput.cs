// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 开放接口身份输入参数
/// </summary>
public class PageOpenAccessInput : BasePageInput
{
    /// <summary>
    /// 身份标识
    /// </summary>
    public string AccessKey { get; set; }
}

/// <remarks>
/// <para>
/// 本类继承 <see cref="SysOpenAccess"/>。**只有在本类字段全部必填时才允许这样做** ——
/// <c>DataAnnotations</c> 的特性是 <c>Inherited = true</c>，<c>override</c> 会沿重写链把
/// 实体上同名属性的校验特性一并继承过来（完整踩坑记录见 <see cref="UpdateOpenAccessInput"/>）。
/// 将来若要新增**可选**字段，必须像 <see cref="UpdateOpenAccessInput"/> 那样平铺声明，不要靠继承。
/// </para>
/// </remarks>
public class AddOpenAccessInput : SysOpenAccess
{
    /// <summary>
    /// 身份标识
    /// </summary>
    [Required(ErrorMessage = "身份标识不能为空")]
    public override string AccessKey { get; set; }

    /// <summary>
    /// 密钥
    /// </summary>
    [Required(ErrorMessage = "密钥不能为空")]
    public override string AccessSecret { get; set; }

    /// <summary>
    /// 绑定租户Id
    /// </summary>
    /// <remarks>
    /// ★ 用 <c>[Range(1, ...)]</c> 而不是 <c>[Required]</c>：<c>long</c> 是非空值类型，
    /// 装箱后永远不为 <c>null</c>，<c>RequiredAttribute</c> 对它的校验**恒为通过**（等于没写）。
    /// 要真正拦住「没选绑定租户」，只能限定取值范围。
    /// </remarks>
    [Range(1, long.MaxValue, ErrorMessage = "绑定租户不能为空")]
    public override long BindTenantId { get; set; }

    /// <summary>
    /// 绑定用户Id
    /// </summary>
    /// <remarks>
    /// ★ 同上：<c>[Required]</c> 对 <c>long</c> 是空操作，改用 <c>[Range]</c>。
    /// 放行的后果不只是「少一道校验」——<c>BindUserId = 0</c> 的凭证在
    /// <c>OnValidated</c> 里会因 <c>BindUser</c> 为 <c>null</c> 抛 NRE，
    /// 让该 accessKey 的每次调用都变成 500。
    /// </remarks>
    [Range(1, long.MaxValue, ErrorMessage = "绑定用户不能为空")]
    public override long BindUserId { get; set; }
}

/// <summary>
/// 更新开放接口身份输入
/// </summary>
/// <remarks>
/// <para>
/// ★★ 本类**刻意不继承任何实体 / DTO**，字段全部平铺自己声明。这不是啰嗦，是踩过两次坑后的结论。
/// </para>
/// <para>
/// <b>坑一</b>：曾继承 <see cref="AddOpenAccessInput"/>。那个类上挂着
/// <c>[Required(ErrorMessage = "密钥不能为空")]</c>，于是「编辑时不填密钥」被参数校验挡下，
/// 「掩码 / 留空即不变」的语义永远走不到服务端。
/// </para>
/// <para>
/// <b>坑二</b>：改成继承 <see cref="SysOpenAccess"/> 后**依然被挡下**。
/// 因为 <c>DataAnnotations</c> 的特性是 <c>Inherited = true</c>，<c>override</c> 属性会
/// **沿重写链继承基类同名属性上的特性**（<c>TypeDescriptor</c> 会把基类属性的特性合并进来）。
/// 实体上的 <c>[Required]</c> 又漏了过来。识别特征很明显：报的是默认英文文案
/// <c>The AccessSecret field is required.</c> —— 本类并没有写过这条中文以外的消息，
/// 看到英文默认文案就说明它来自继承链上游，而不是本类。
/// </para>
/// <para>
/// <b>结论</b>：只要继承，就总有一处校验特性会顺着继承链漏进来，而且
/// 「漏了哪一条」是隐式的、改一个基类属性就可能悄悄多出一条。
/// 所以这里平铺声明，让「哪个字段必填」在**一个类里一眼可见**。
/// 新增字段时请同样平铺，不要为了少写几行去继承实体。
/// </para>
/// <para>
/// <b>密钥语义</b>：<see cref="AccessSecret"/> 是**可选**的。留空、或原样回传列表下发的掩码
/// （见 <see cref="OpenAccessSecretMask"/>）都表示「不修改密钥」。
/// </para>
/// <para>
/// <b>状态语义</b>：<see cref="Status"/> 是**可空**的，<c>null</c> 表示「不修改」。
/// 刻意不给它 <c>= StatusEnum.Enable</c> 这种默认值 —— 那会让「只改 scopes、没带 status」
/// 的请求**静默把一把已停用的凭证重新启用**。停用是密钥泄漏时的止血手段，
/// 任何「不显式声明就能把它关掉」的路径都必须堵住。
/// </para>
/// </remarks>
public class UpdateOpenAccessInput
{
    /// <summary>
    /// 主键
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 身份标识
    /// </summary>
    [Required(ErrorMessage = "身份标识不能为空")]
    [MaxLength(128)]
    public string AccessKey { get; set; }

    /// <summary>
    /// 密钥（**可选**：留空或提交掩码表示保持原密钥不变）
    /// </summary>
    [MaxLength(256)]
    public string AccessSecret { get; set; }

    /// <summary>
    /// 权限范围（逗号分隔，如 allocate,notify；留空表示不限制）
    /// </summary>
    [MaxLength(128)]
    public string Scopes { get; set; }

    /// <summary>
    /// 绑定租户Id
    /// </summary>
    /// <remarks>
    /// ★ <c>[Range(1, ...)]</c> 而非 <c>[Required]</c>：<c>long</c> 是非空值类型，
    /// <c>RequiredAttribute</c> 对它恒为通过，等于没写。详见 <see cref="AddOpenAccessInput"/>。
    /// </remarks>
    [Range(1, long.MaxValue, ErrorMessage = "绑定租户不能为空")]
    public long BindTenantId { get; set; }

    /// <summary>
    /// 绑定用户Id
    /// </summary>
    /// <remarks>
    /// ★ 同上，用 <c>[Range]</c> 才能真正拦住 0。
    /// </remarks>
    [Range(1, long.MaxValue, ErrorMessage = "绑定用户不能为空")]
    public long BindUserId { get; set; }

    /// <summary>
    /// 状态（启用 / 停用）；<c>null</c> = 不修改
    /// </summary>
    public StatusEnum? Status { get; set; }
}

public class DeleteOpenAccessInput : BaseIdInput
{
}

public class GenerateSignatureInput
{
    /// <summary>
    /// 身份标识
    /// </summary>
    [Required(ErrorMessage = "身份标识不能为空")]
    public string AccessKey { get; set; }

    /// <summary>
    /// 密钥
    /// </summary>
    [Required(ErrorMessage = "密钥不能为空")]
    public string AccessSecret { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public HttpMethodEnum Method { get; set; }

    /// <summary>
    /// 请求接口地址
    /// </summary>
    [Required(ErrorMessage = "请求接口地址不能为空")]
    public string Url { get; set; }

    /// <summary>
    /// 时间戳（秒级 Unix 时间戳）
    /// </summary>
    /// <remarks>
    /// ★ 用 <c>[Range(1, ...)]</c> 而不是 <c>[Required]</c>：<c>long</c> 是非空值类型，
    /// <c>RequiredAttribute</c> 对它恒为通过 —— 原来的 <c>[Required]</c> 等于没写，
    /// 传 <c>timestamp=0</c> 会被放行，然后算出一个永远不可能通过服务端校验的签名，
    /// 使用者只会看到「签名不对」而想不到是时间戳为 0。
    /// 前端本就要求「非空且为数字」（<c>generateSign.vue</c> 的 watch），这里补齐服务端一侧。
    /// </remarks>
    [Range(1, long.MaxValue, ErrorMessage = "时间戳不合法（应为秒级 Unix 时间戳）")]
    public long Timestamp { get; set; }

    /// <summary>
    /// 随机数
    /// </summary>
    [Required(ErrorMessage = "随机数不能为空")]
    public string Nonce { get; set; }
}