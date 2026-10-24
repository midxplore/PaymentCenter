// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Excel;

namespace Admin.NET.Application;

/// <summary>
/// 运营汇总按日查询入参（自然日区间）
/// </summary>
public class PayOpsDailyInput
{
    /// <summary>开始业务日（含）</summary>
    [Required(ErrorMessage = "开始日期不能为空")]
    public DateTime StartDate { get; set; }

    /// <summary>结束业务日（含）</summary>
    [Required(ErrorMessage = "结束日期不能为空")]
    public DateTime EndDate { get; set; }
}

/// <summary>
/// 此刻额度池况
/// </summary>
public class PayOpsPoolNowOutput
{
    public decimal TotalQuotaSum { get; set; }
    public decimal UsedQuotaSum { get; set; }
    public decimal LockedQuotaSum { get; set; }
    public decimal RemainingQuotaSum { get; set; }
    public decimal UnpaidLockedSum { get; set; }
    public int AccountEnabledCount { get; set; }
    public int AccountCount { get; set; }
}

/// <summary>
/// 单日运营汇总行
/// </summary>
public class PayOpsDailyRowOutput
{
    /// <summary>业务日</summary>
    public DateTime BizDate { get; set; }

    /// <summary>额度投放（Total 净增部分，≥0）</summary>
    public decimal QuotaInject { get; set; }

    /// <summary>额度回收（Total 净减绝对值，≥0）</summary>
    public decimal QuotaReclaim { get; set; }

    /// <summary>投放净额 = 投放 − 回收</summary>
    public decimal QuotaNet { get; set; }

    /// <summary>预占发生（Created 合计）</summary>
    public decimal LockOccurred { get; set; }

    /// <summary>实收到账（Completed.ReceivedTotal 合计）</summary>
    public decimal ReceivedCompleted { get; set; }

    /// <summary>完成笔数</summary>
    public int CompletedCount { get; set; }

    /// <summary>过期释放 = Σ(Request − Received)</summary>
    public decimal ExpireReleased { get; set; }

    /// <summary>当日确认未到账笔数</summary>
    public int MarkUnpaidCount { get; set; }

    /// <summary>新建码数</summary>
    public int AccountAddCount { get; set; }

    /// <summary>额度调整次数</summary>
    public int QuotaAdjustCount { get; set; }

    /// <summary>编辑次数</summary>
    public int AccountUpdateCount { get; set; }

    /// <summary>是否有日终快照</summary>
    public bool HasSnapshot { get; set; }

    public decimal? EodTotalQuotaSum { get; set; }
    public decimal? EodUsedQuotaSum { get; set; }
    public decimal? EodLockedQuotaSum { get; set; }
    public decimal? EodRemainingQuotaSum { get; set; }
    public decimal? EodUnpaidLockedSum { get; set; }
    public int? EodAccountEnabledCount { get; set; }
}

/// <summary>
/// 运营汇总查询结果
/// </summary>
public class PayOpsSummaryOutput
{
    /// <summary>此刻池况</summary>
    public PayOpsPoolNowOutput Current { get; set; }

    /// <summary>按日行（新→旧）</summary>
    public List<PayOpsDailyRowOutput> Days { get; set; }

    /// <summary>预占发生不含过期重传 relock 的说明</summary>
    public string Footnote { get; set; }
}

/// <summary>
/// 按日导出行
/// </summary>
[ExcelExporter(Name = "运营汇总按日", AutoFitAllColumn = true)]
public class PayOpsDailyExportDto
{
    [ExporterHeader(DisplayName = "业务日", IsBold = true)]
    public string BizDate { get; set; }

    [ExporterHeader(DisplayName = "额度投放")]
    public decimal QuotaInject { get; set; }

    [ExporterHeader(DisplayName = "额度回收")]
    public decimal QuotaReclaim { get; set; }

    [ExporterHeader(DisplayName = "投放净额")]
    public decimal QuotaNet { get; set; }

    [ExporterHeader(DisplayName = "预占发生")]
    public decimal LockOccurred { get; set; }

    [ExporterHeader(DisplayName = "实收到账")]
    public decimal ReceivedCompleted { get; set; }

    [ExporterHeader(DisplayName = "完成笔数")]
    public int CompletedCount { get; set; }

    [ExporterHeader(DisplayName = "过期释放")]
    public decimal ExpireReleased { get; set; }

    [ExporterHeader(DisplayName = "未到账笔数")]
    public int MarkUnpaidCount { get; set; }

    [ExporterHeader(DisplayName = "新建码")]
    public int AccountAddCount { get; set; }

    [ExporterHeader(DisplayName = "额度调整次数")]
    public int QuotaAdjustCount { get; set; }

    [ExporterHeader(DisplayName = "编辑次数")]
    public int AccountUpdateCount { get; set; }

    [ExporterHeader(DisplayName = "日终总额")]
    public string EodTotalQuotaSum { get; set; }

    [ExporterHeader(DisplayName = "日终已用")]
    public string EodUsedQuotaSum { get; set; }

    [ExporterHeader(DisplayName = "日终在途预占")]
    public string EodLockedQuotaSum { get; set; }

    [ExporterHeader(DisplayName = "日终可用")]
    public string EodRemainingQuotaSum { get; set; }

    [ExporterHeader(DisplayName = "日终未到账占用")]
    public string EodUnpaidLockedSum { get; set; }

    [ExporterHeader(DisplayName = "日终启用码数")]
    public string EodAccountEnabledCount { get; set; }
}
