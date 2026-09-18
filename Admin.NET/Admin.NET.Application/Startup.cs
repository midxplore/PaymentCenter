// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Admin.NET.Application;

[AppStartup(100)]
public class Startup : AppStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // 后台服务异常，不影响主程序（配置的服务器连接异常）
        services.Configure<HostOptions>(options =>
        {
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

        // 注册RabbitMQ服务
        var option = App.GetConfig<RabbitMqConsumerOptions>("RabbitMqConsumerOptions");
        var consumerCount = option.ConsumerCount.ToIntOrDefault(1);
        services.AddRabbitMQ(consumerCount);

        // 添加远程请求服务配置
        services.AddConfigurableOptions<HttpRemoteOptions>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }

    /// <summary>
    /// 构建 WebApplication 对象过程中装载中间件
    /// </summary>
    /// <param name="application">WebApplication对象</param>
    /// <param name="env"></param>
    /// <param name="componentContext"></param>
    public void LoadAppComponent(object application, IWebHostEnvironment env, ComponentContext componentContext)
    {
    }
}