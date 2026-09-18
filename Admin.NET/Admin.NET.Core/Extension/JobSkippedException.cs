namespace Admin.NET.Core.Extension;

/// <summary>
/// 作业跳过异常（表示任务被正常跳过，而非失败）
/// </summary>
public class JobSkippedException : Exception
{
    public JobSkippedException(string message) : base(message) { }
}