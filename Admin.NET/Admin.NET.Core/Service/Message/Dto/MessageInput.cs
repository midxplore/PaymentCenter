// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

public class MessageInput
{
    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageTypeEnum MessageType { get; set; }

    /// <summary>
    /// 消息标题
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 发送者Id
    /// </summary>
    public long SendUserId { get; set; }

    /// <summary>
    /// 接收者Id集合
    /// </summary>
    public List<long> ReceiveUserIds { get; set; }
}

/// <summary>
/// 发送消息输入参数
/// </summary>
public class PushMessageInput
{
    /// <summary>
    /// 推送主题
    /// </summary>
    [Enum]
    public WeComPushThemeEnum Theme { get; set; }

    /// <summary>
    /// 推送类型
    /// </summary>
    [Enum]
    public WeComPushTypeEnum Type { get; set; }

    /// <summary>
    /// 模板ID
    /// </summary>
    [Required(ErrorMessage = "模板ID不能为空")]
    public string TemplateId { get; set; }

    /// <summary>
    /// 模板参数
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> ParamMap { get; set; }
}

/// <summary>
/// 定时任务推送输入参数
/// </summary>
public class PushScheduledTaskInput
{
    /// <summary>
    /// 任务详情
    /// </summary>
    public string TaskDesc { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime BeginTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// 执行时长(秒)
    /// </summary>
    public double Duration => Math.Round((EndTime - BeginTime).TotalSeconds, 2);

    /// <summary>
    /// 异常信息
    /// </summary>
    public string Exception { get; set; }
}