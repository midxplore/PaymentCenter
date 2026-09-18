// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class OpenAccessOutput : SysOpenAccess
{
    /// <summary>
    /// 绑定用户账号
    /// </summary>
    public string BindUserAccount { get; set; }

    /// <summary>
    /// 绑定租户名称
    /// </summary>
    public string BindTenantName { get; set; }

    private string _accessSecret;

    /// <summary>
    /// 密钥（**仅回显掩码，不回显明文**）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 覆盖基类的虚拟属性，在 <b>getter</b> 上做脱敏。这样无论 SqlSugar 用
    /// <c>Select(..., true)</c> 映射全列、还是将来有人改成别的投影方式，
    /// 只要数据是经本 DTO 序列化出去的，密钥就一定是掩码 ——
    /// 把「不能泄漏密钥」收敛到一个**无法绕过**的点上，
    /// 而不是依赖每个查询各自记得排除该列（那种约定迟早会被新查询漏掉）。
    /// </para>
    /// <para>
    /// setter 保留原值到私有字段，供 <see cref="OpenAccessSecretMask.MeansUnchanged"/>
    /// 在编辑场景下判断「提交的是掩码还是新密钥」。
    /// </para>
    /// <para>
    /// 需要向接入方交付真实密钥的场景（首次开通 / 轮换）走「新增」或「编辑时点『生成密钥』」，
    /// 由服务端生成后**当次**返回，之后不再回显。
    /// </para>
    /// </remarks>
    public override string AccessSecret
    {
        get => OpenAccessSecretMask.Mask(_accessSecret);
        set => _accessSecret = value;
    }
}