// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 本地序列输出参数
/// </summary>
public class PageSerialOutput
{
    /// <summary>
    /// 主键Id
    /// </summary>
    public long? Id { get; set; }

    /// <summary>
    /// 流水号
    /// </summary>
    public long? Seq { get; set; }

    /// <summary>
    /// 有效期
    /// </summary>
    public DateTime? Expy { get; set; }

    /// <summary>
    /// 使用分类
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 重置间隔
    /// </summary>
    public ResetIntervalEnum? ResetInterval { get; set; }

    /// <summary>
    /// 表达式
    /// </summary>
    public string Formater { get; set; }

    /// <summary>
    /// 最小值
    /// </summary>
    public long? Min { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    public int? OrderNo { get; set; }

    /// <summary>
    /// 最大值
    /// </summary>
    public long? Max { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public StatusEnum? Status { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string Remark { get; set; }
}