// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.SignalR;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统消息发送服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 370, Description = "消息发送")]
public class SysMessageService : IDynamicApiController, ITransient
{
    private readonly IEventPublisher _eventPublisher;
    private readonly IHubContext<OnlineUserHub, IOnlineUserHub> _chatHubContext;

    public SysMessageService(IEventPublisher eventPublisher,
        IHubContext<OnlineUserHub, IOnlineUserHub> chatHubContext)
    {
        _eventPublisher = eventPublisher;
        _chatHubContext = chatHubContext;
    }

    /// <summary>
    /// 发送消息给所有人 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("发送消息给所有人")]
    public async Task SendAllUser(MessageInput input)
    {
        await _chatHubContext.Clients.All.ReceiveMessage(input);
    }

    /// <summary>
    /// 发送消息给某人 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("发送消息给某人")]
    public async Task SendUser(MessageInput input)
    {
        var hashKey = SysCacheService.HashGetAll<OnlineUser>(CacheConst.KeyUserOnline);
        var sendUser = hashKey.Where(u => u.Value.UserId == input.SendUserId).Select(u => u.Value).FirstOrDefault();
        var receiveUsers = hashKey.Where(u => input.ReceiveUserIds.Any(a => a == u.Value.UserId)).Select(u => u.Value).ToList();
        foreach (var receiveUser in receiveUsers)
        {
            var logMsg = new SysLogMsg
            {
                MessageType = input.MessageType.ToString(),
                Title = input.Title,
                Message = input.Message,
                ReceiveUserId = receiveUser.UserId,
                ReceiveUserName = receiveUser.RealName,
                ReceiveIp = receiveUser.Ip,
                ReceiveBrowser = receiveUser.Browser,
                ReceiveOs = receiveUser.Os,
                ReceiveDevice = receiveUser.Device,
                SendUserId = sendUser.UserId,
                SendUserName = sendUser.RealName,
                SendIp = sendUser.Ip,
                SendBrowser = sendUser.Browser,
                SendOs = sendUser.Os,
                SendDevice = sendUser.Device,
                SendTime = DateTime.Now
            };

            // 发送消息
            await _chatHubContext.Clients.Client(receiveUser.ConnectionId ?? "").ReceiveMessage(logMsg);

            // 保存消息日志
            await _eventPublisher.PublishAsync(nameof(AppEventSubscriber.CreateMsgLog), logMsg);
        }
    }
}