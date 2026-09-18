// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 开放接口密钥掩码（列表回显脱敏 + 「掩码即不变」判定）
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要</b>：<see cref="OpenAccessOutput"/> 继承 <see cref="SysOpenAccess"/>，
/// 而分页查询用的是 SqlSugar 的 <c>Select(..., true)</c>（映射全列），
/// 于是 <c>/api/sysOpenAccess/page</c> 会把 <see cref="SysOpenAccess.AccessSecret"/>
/// **明文**吐给前端，前端还把它渲染成一列。
/// </para>
/// <para>
/// 后果被放大的原因：这些密钥是**调用资金类接口的唯一凭据**（HMAC 认证的密钥），
/// 于是一个只有「查询」按钮权限的只读运维账号，就能把**全部接入方**的密钥导走，
/// 再拿去冒充任意接入方调用 allocate / notify。这是典型的「权限设计只做到按钮级、
/// 敏感数据却跟着列表一起下发」的越权。
/// </para>
/// <para>
/// <b>做法</b>：列表只回显**掩码**（保留首尾各 4 位便于人工辨认「是哪一把」），
/// 编辑时若原样提交掩码则视为「不修改密钥」，由服务端保留原值。
/// 这样既堵住了批量导出，又不牺牲「新增/轮换密钥」的可用性。
/// </para>
/// <para>
/// <b>为什么不用「干脆不回显」</b>：接入方排障时需要确认「当前生效的是哪一把密钥」，
/// 掩码保留了可辨识性而零信息泄漏（真密钥是 Base64，不含 <c>*</c>，掩码不可能与真值混淆）。
/// </para>
/// </remarks>
public static class OpenAccessSecretMask
{
    /// <summary>
    /// 掩码中段占位符
    /// </summary>
    /// <remarks>
    /// 选用 <c>*</c> 是因为它**不在 Base64 字符集内**（Base64 为 A-Z a-z 0-9 + / =），
    /// 所以「含 <c>*</c>」可以无歧义地表示「这是掩码，不是真密钥」。
    /// </remarks>
    public const string Marker = "****";

    /// <summary>
    /// 首尾各保留的明文字符数
    /// </summary>
    public const int KeepLength = 4;

    /// <summary>
    /// 生成掩码
    /// </summary>
    /// <param name="secret">真实密钥</param>
    /// <returns>掩码；密钥过短时只返回占位符</returns>
    public static string Mask(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;

        // 太短的密钥不保留任何明文位（保留首尾就等于把整把密钥说出来了）
        if (secret.Length <= KeepLength * 2) return Marker;

        return string.Concat(secret.AsSpan(0, KeepLength), Marker, secret.AsSpan(secret.Length - KeepLength));
    }

    /// <summary>
    /// 判断提交值是否表示「保持原密钥不变」
    /// </summary>
    /// <remarks>
    /// 判据是**精确相等**而不是「包含 <c>*</c>」：避免接入方真的填了一个含 <c>*</c> 的
    /// 非法密钥时被静默忽略（那种情况应该由格式校验挡下，而不是被当成「不变」）。
    /// </remarks>
    /// <param name="incoming">本次提交的密钥值</param>
    /// <param name="existing">库中现有密钥</param>
    /// <returns></returns>
    public static bool MeansUnchanged(string incoming, string existing)
    {
        if (string.IsNullOrWhiteSpace(incoming)) return true;
        return incoming == Mask(existing);
    }

    /// <summary>
    /// 判断某个值「长得像掩码」（含 <c>*</c>）
    /// </summary>
    /// <remarks>
    /// 用于**拒绝**把掩码当成真密钥存库：真密钥是 Base64，字符集里没有 <c>*</c>，
    /// 所以含 <c>*</c> 的值一定是掩码或脏数据。若不拦，一旦有人把列表里的掩码
    /// 复制去当密钥保存，这条凭证会永久无法通过签名校验，而且现象很隐蔽
    /// （接入方报「签名一直失败」，查库却看到一串「像密钥」的值）。
    /// </remarks>
    public static bool LooksLikeMask(string value)
        => !string.IsNullOrEmpty(value) && value.Contains('*');
}
