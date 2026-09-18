// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 业务操作审计表（F7.3）
/// </summary>
/// <remarks>
/// 只增不改：本表不提供任何 update / delete 接口，只在业务事务内 append。
/// <para>
/// 与骨架 <c>SysLogOp</c> 的分工：<c>SysLogOp</c> 是通用请求级日志，会被
/// <c>CleanSysLogJob</c> 按保留期清理；本表是业务级不可篡改审计，**永不清删**。
/// </para>
/// </remarks>
[SugarTable("pay_audit_log", "业务操作审计表")]
[SysTable]
[SugarIndex("i_{table}_target", nameof(TargetType), OrderByType.Asc, nameof(TargetId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
[SugarIndex("i_{table}_action", nameof(Action), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
public class PayAuditLog : EntityBase
{
    /// <summary>
    /// 审计动作
    /// </summary>
    [SugarColumn(ColumnDescription = "审计动作")]
    public virtual PayAuditActionEnum Action { get; set; }

    /// <summary>
    /// 目标类型（Account / Order / Abnormal）
    /// </summary>
    [SugarColumn(ColumnDescription = "目标类型", Length = 32)]
    [MaxLength(32)]
    public virtual string TargetType { get; set; }

    /// <summary>
    /// 目标主键
    /// </summary>
    [SugarColumn(ColumnDescription = "目标主键")]
    public virtual long TargetId { get; set; }

    /// <summary>
    /// 目标业务标识（账号类型 / 订单号等，便于检索）
    /// </summary>
    [SugarColumn(ColumnDescription = "目标业务标识", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string TargetNo { get; set; }

    /// <summary>
    /// 变更前快照（JSON）
    /// </summary>
    [SugarColumn(ColumnDescription = "变更前快照", ColumnDataType = "text", IsNullable = true)]
    public virtual string BeforeJson { get; set; }

    /// <summary>
    /// 变更后快照（JSON）
    /// </summary>
    [SugarColumn(ColumnDescription = "变更后快照", ColumnDataType = "text", IsNullable = true)]
    public virtual string AfterJson { get; set; }

    /// <summary>
    /// 说明
    /// </summary>
    [SugarColumn(ColumnDescription = "说明", Length = 512, IsNullable = true)]
    [MaxLength(512)]
    public virtual string Remark { get; set; }

    /// <summary>
    /// 操作人Id（系统动作为 0）
    /// </summary>
    [SugarColumn(ColumnDescription = "操作人Id")]
    public virtual long OperatorId { get; set; }

    /// <summary>
    /// 操作人姓名（系统动作为空）
    /// </summary>
    [SugarColumn(ColumnDescription = "操作人姓名", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string OperatorName { get; set; }

    /// <summary>
    /// 操作 IP
    /// </summary>
    [SugarColumn(ColumnDescription = "操作IP", Length = 64, IsNullable = true)]
    [MaxLength(64)]
    public virtual string OperatorIp { get; set; }

    /// <summary>
    /// 调用方Id（开放接口调用时 = <c>SysOpenAccess.Id</c>；后台操作时为空）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 审计表里必须能区分「这条记录是**后台某个人**做的」还是「**某个接入方**通过开放接口做的」。
    /// 只靠 <see cref="OperatorId"/> 区分不了：它是 <c>SysUser.Id</c>，
    /// 而开放接口请求根本没有登录用户（签名鉴权走的是 accessKey）。
    /// </para>
    /// <para>
    /// 单独建列而不是复用 <see cref="OperatorId"/> 的原因：两个 Id 属于**不同的命名空间**
    /// （<c>sysuser.id</c> 与 <c>sysopenaccess.id</c>），复用会让「按操作人查审计」
    /// 出现张冠李戴 —— 查用户 5 会连带查出接入方 5 的记录。
    /// </para>
    /// </remarks>
    [SugarColumn(ColumnDescription = "调用方Id", IsNullable = true)]
    public virtual long? ClientId { get; set; }

    /// <summary>
    /// 调用方身份标识（AccessKey 原文）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 存**字符串原文**而不是只存 <see cref="ClientId"/> 外键，是为了让审计行**自包含**：
    /// </para>
    /// <list type="bullet">
    /// <item>凭证行可能被删除（接入方终止合作），此时 <c>ClientId</c> 就指向了一个不存在的行，
    /// 事后追查「这笔记账是哪个 key 发起的」会断链；</item>
    /// <item>认证**失败**的场景下 accessKey 很可能压根不在 <c>sysopenaccess</c> 里
    /// （密钥被爆破、接入方配错环境），没有 <c>ClientId</c> 可存，
    /// 而「对方到底用了哪个 key」恰恰是最有价值的线索。</item>
    /// </list>
    /// <para>
    /// 长度 128 与 <c>sysopenaccess.accesskey</c> 对齐。
    /// </para>
    /// </remarks>
    [SugarColumn(ColumnDescription = "调用方身份标识", Length = 128, IsNullable = true)]
    [MaxLength(128)]
    public virtual string ClientKey { get; set; }
}
