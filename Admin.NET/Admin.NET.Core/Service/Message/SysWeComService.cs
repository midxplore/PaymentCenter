// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

/// <summary>
/// 企业微信消息服务 🧩
/// </summary>
public class SysWeComService : IDynamicApiController, ITransient
{
    private readonly UserManager _userManager;
    private readonly WeComOptions _weComOptions;
    private readonly SysConfigService _sysConfigService;
    private readonly IHttpRemoteService _httpRemoteService;
    private readonly IReadOnlyDictionary<string, string> _templateMessages;

    public SysWeComService(SysConfigService sysConfigService, IOptions<WeComOptions> weComOptions, UserManager userManager, IHttpRemoteService httpRemoteService)
    {
        _userManager = userManager;
        _weComOptions = weComOptions.Value;
        _sysConfigService = sysConfigService;
        _httpRemoteService = httpRemoteService;
        _templateMessages = App.GetConfig<Dictionary<string, string>>("MessageTemplates");
    }

    /// <summary>
    /// 是否启用推送 📨
    /// </summary>
    /// <returns></returns>
    private async Task<bool> EnablePush()
    {
        return await _sysConfigService.GetConfigValueByCode<bool>(ConfigConst.SysErrorWeCom);
    }

    /// <summary>
    /// 发送企业微信群消息 📨
    /// </summary>
    /// <param name="key"></param>
    /// <param name="content"></param>
    private async Task SendMessage(string key, string content)
    {
        await _httpRemoteService.PostAsync($"https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key={key}",
            builder => builder
                .SetContent(new StringContent(new
                {
                    msgtype = "markdown",
                    markdown = new { content }
                }.ToJson(), Encoding.UTF8, "application/json")));
    }

    /// <summary>
    /// 发送企业微信群消息 📨
    /// </summary>
    /// <param name="type"></param>
    /// <param name="theme"></param>
    /// <param name="templateId"></param>
    /// <param name="paramMap"></param>
    private async Task SendMessage(WeComPushTypeEnum type, WeComPushThemeEnum theme, string templateId, object paramMap)
    {
        if (!_templateMessages.TryGetValue(templateId ?? "", out string message)) throw Oops.Oh("未找到对应的消息模板！");

        // 获取机器人key列表
        var botKeys = type switch
        {
            WeComPushTypeEnum.ScheduledTask => _weComOptions.WeComRobotKeysScheduledTask,
            WeComPushTypeEnum.Alter => _weComOptions.WeComBotKeysAlert,
            _ => throw Oops.Oh("不支持的推送类型！")
        };

        // 添加通用参数
        var now = DateTime.Now;
        var param = paramMap.Adapt<Dictionary<string, string>>() ?? [];

        // 主题颜色
        switch (theme)
        {
            case WeComPushThemeEnum.Info:
                param.Add("theme", "info");
                break;

            case WeComPushThemeEnum.Warning:
                param.Add("theme", "warning");
                break;

            case WeComPushThemeEnum.Comment:
            default:
                param.Add("theme", "comment");
                break;
        }

        param.Add("realName", _userManager.RealName);
        param.Add("account", _userManager.Account);
        param.Add("nowYear", now.ToString("yyyy"));
        param.Add("nowYearMonth", now.ToString("yyyy-MM"));
        param.Add("nowDate", now.ToString("yyyy-MM-dd"));
        param.Add("nowDateTime", now.ToString("yyyy-MM-dd HH:mm:ss"));
        param = param.Adapt<Dictionary<string, string>>();

        string format = RenderTemplate(message, param);

        // 推送到企业微信群
        foreach (var key in botKeys) await SendMessage(key, format);
    }

    /// <summary>
    /// 渲染模板 📨
    /// </summary>
    /// <param name="template"></param>
    /// <param name="parameters"></param>
    /// <returns></returns>
    private static string RenderTemplate(string template, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(template) || parameters == null || parameters.Count == 0) return template;

        // 匹配 {key} 格式的占位符（非贪婪）
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value.ToLower();
            return parameters.TryGetValue(key, out var value) ? value : match.Value; // 未找到则保留原样
        });
    }

    /// <summary>
    /// 推送消息 📨
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("推送消息")]
    public async Task PushMessage(PushMessageInput input)
    {
        await SendMessage(input.Type, input.Theme, input.TemplateId, input.ParamMap);
    }

    /// <summary>
    /// 推送异常消息 📨
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    [NonAction]
    public async Task PushErrorMessage(Exception exception)
    {
        if (App.HostEnvironment.IsDevelopment()) return;
        await SendMessage(WeComPushTypeEnum.Alter, WeComPushThemeEnum.Warning, "ER00001", new
        {
            source = exception.Source,
            name = exception.GetType().Name,
            message = exception.Message,
            targetSite = exception.TargetSite + "",
            stackTrace = exception.StackTrace.Truncate(256).Trim(),
        });
    }

    /// <summary>
    /// 推送异常消息 📨
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [NonAction]
    public async Task PushScheduledTask(PushScheduledTaskInput input)
    {
        if (App.HostEnvironment.IsDevelopment()) return;
        var isSuccess = string.IsNullOrWhiteSpace(input.Exception);
        await SendMessage(WeComPushTypeEnum.ScheduledTask, isSuccess ? WeComPushThemeEnum.Info : WeComPushThemeEnum.Warning, isSuccess ? "ST00001" : "ST00002", input);
    }
}