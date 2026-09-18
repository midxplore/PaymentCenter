// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 本地序列基础输入参数
/// </summary>
public class SerialBaseInput
{
    /// <summary>
    /// 流水号
    /// </summary>
    public virtual long? Seq { get; set; }

    ///// <summary>
    ///// 有效期
    ///// </summary>
    //public virtual DateTime? Expy { get; set; }

    /// <summary>
    /// 使用分类
    /// </summary>
    public virtual string Type { get; set; }

    /// <summary>
    /// 重置间隔
    /// </summary>
    public virtual ResetIntervalEnum? ResetInterval { get; set; }

    /// <summary>
    /// 表达式
    /// </summary>
    public virtual string Formater { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    public virtual long? Min { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public virtual int? OrderNo { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    public virtual long? Max { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public virtual StatusEnum? Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public virtual string Remark { get; set; }

    /// <summary>
    /// 租户Id
    /// </summary>
    public virtual long? TenantId { get; set; }
}

/// <summary>
/// 本地序列分页查询输入参数
/// </summary>
public class PageSerialInput : BasePageInput
{
    /// <summary>
    /// 关键字查询
    /// </summary>
    public string SearchKey { get; set; }

    /// <summary>
    /// 使用分类
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [Dict(nameof(StatusEnum))]
    public StatusEnum? Status { get; set; }
}

/// <summary>
/// 本地序列增加输入参数
/// </summary>
public class AddSerialInput : SerialBaseInput
{
    /// <summary>
    /// 使用分类
    /// </summary>
    [Required(ErrorMessage = "使用分类不能为空")]
    public override string Type { get; set; }

    /// <summary>
    /// 重置间隔
    /// </summary>
    [Required(ErrorMessage = "重置间隔不能为空")]
    [Dict(nameof(ResetIntervalEnum))]
    public override ResetIntervalEnum? ResetInterval { get; set; }

    /// <summary>
    /// 表达式
    /// </summary>
    [RegularExpression(@".*?\{SEQ\}.*?", ErrorMessage = "表达式必须包含插槽 {SEQ}")]
    [Required(ErrorMessage = "表达式不能为空")]
    public override string Formater { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    [Required(ErrorMessage = "最小值不能为空")]
    public override long? Min { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [Required(ErrorMessage = "排序不能为空")]
    public override int? OrderNo { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    [Required(ErrorMessage = "最大值不能为空")]
    public override long? Max { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public override string Remark { get; set; }
}

/// <summary>
/// 本地序列更新输入参数
/// </summary>
public class UpdateSerialInput : SerialBaseInput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    [Required(ErrorMessage = "主键Id不能为空")]
    public long? Id { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    [Required(ErrorMessage = "流水号不能为空")]
    public override long? Seq { get; set; }

    /// <summary>
    /// 使用分类
    /// </summary>
    [Required(ErrorMessage = "使用分类不能为空")]
    public override string Type { get; set; }

    /// <summary>
    /// 重置间隔
    /// </summary>
    [Required(ErrorMessage = "重置间隔不能为空")]
    [Dict(nameof(ResetIntervalEnum))]
    public override ResetIntervalEnum? ResetInterval { get; set; }

    /// <summary>
    /// 表达式
    /// </summary>
    [RegularExpression(@".*?\{SEQ\}.*?", ErrorMessage = "表达式必须包含插槽 {SEQ}")]
    [Required(ErrorMessage = "表达式不能为空")]
    public override string Formater { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    [Required(ErrorMessage = "最小值不能为空")]
    public override long? Min { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [Required(ErrorMessage = "排序不能为空")]
    public override int? OrderNo { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    [Required(ErrorMessage = "最大值不能为空")]
    public override long? Max { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [Required(ErrorMessage = "状态不能为空")]
    [Dict(nameof(StatusEnum))]
    public override StatusEnum? Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public override string Remark { get; set; }
}

public class PreviewSysSerialInput
{
    /// <summary>
    /// 生成表达式
    /// </summary>
    [Required(ErrorMessage = "生成表达式不能为空")]
    public string Formater { get; set; }

    /// <summary>
    /// 序号
    /// </summary>
    [Range(0, long.MaxValue, ErrorMessage = "流水号必须大于等于0")]
    public long Seq { get; set; }

    /// <summary>
    /// 最大序号
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "最大序号必须大于等于1")]
    public long Max { get; set; }
}

/// <summary>
/// 获取下一个输入参数
/// </summary>
public class GetNextSeqInput
{
    /// <summary>
    /// 使用分类
    /// </summary>
    public virtual string Type { get; set; }
}