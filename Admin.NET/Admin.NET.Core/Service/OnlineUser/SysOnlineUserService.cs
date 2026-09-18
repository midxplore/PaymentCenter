// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.SignalR;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统在线用户服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 300, Description = "在线用户")]
public class SysOnlineUserService : IDynamicApiController, ITransient
{
    private readonly UserManager _userManager;
    private readonly SysConfigService _sysConfigService;
    private readonly IHubContext<OnlineUserHub, IOnlineUserHub> _onlineUserHubContext;

    public SysOnlineUserService(UserManager userManager,
        SysConfigService sysConfigService,
        IHubContext<OnlineUserHub, IOnlineUserHub> onlineUserHubContext)
    {
        _userManager = userManager;
        _sysConfigService = sysConfigService;
        _onlineUserHubContext = onlineUserHubContext;
    }

    /// <summary>
    /// 获取在线用户列表 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取在线用户列表")]
    public async Task<List<OnlineUser>> GetOnlineUserList()
    {
        var userList = SysCacheService.HashGetAll<OnlineUser>(CacheConst.KeyUserOnline).Values.Where(u => u.TenantId == _userManager.TenantId).ToList();
        return await Task.FromResult(userList);
    }

    /// <summary>
    /// 强制下线 🔖
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    [NonValidation]
    [DisplayName("强制下线")]
    public async Task ForceOffline(OnlineUser user)
    {
        await _onlineUserHubContext.Clients.Client(user.ConnectionId ?? "").ForceOffline("强制下线");
    }

    /// <summary>
    /// 通过用户Id踢掉在线用户
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task ForceOfflineByUserId(long userId)
    {
        var users = SysCacheService.HashGetAll<OnlineUser>(CacheConst.KeyUserOnline).Values.Where(u => u.UserId == userId).ToList();
        foreach (var user in users)
        {
            await ForceOffline(user);
        }
    }

    /// <summary>
    /// 发布站内消息
    /// </summary>
    /// <param name="notice"></param>
    /// <param name="userIds"></param>
    /// <returns></returns>
    [NonAction]
    public async Task PublicNotice(SysNotice notice, List<long> userIds)
    {
        var userList = SysCacheService.HashGetAll<OnlineUser>(CacheConst.KeyUserOnline).Values.Where(u => userIds.Contains(u.UserId)).ToList();
        if (userList.Count == 0) return;

        foreach (var item in userList)
        {
            await _onlineUserHubContext.Clients.Client(item.ConnectionId ?? "").PublicNotice(notice);
        }
    }

    /// <summary>
    /// 单用户登录
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="loginMode"></param>
    /// <returns></returns>
    [NonAction]
    public async Task SingleLogin(long userId, LoginModeEnum loginMode)
    {
        if (!await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysSingleLogin)) return;

        var users = SysCacheService.HashGetAll<OnlineUser>(CacheConst.KeyUserOnline).Values.Where(u => u.UserId == userId).ToList();
        foreach (var user in users)
        {
            if (user.LoginMode == loginMode)
            {
                await ForceOffline(user);
            }
        }
    }
}