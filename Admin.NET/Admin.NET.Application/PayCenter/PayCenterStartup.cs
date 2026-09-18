// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// PayCenter 模块启动自检
/// <para>
/// 放在<b>模块内</b>而不是 <c>Admin.NET.Web.Core/Startup.cs</c>：本模块要能整体拆出去独立部署
/// （与菜单种子放模块内是同一个理由）。Furion 会扫描全部 <see cref="AppStartup"/> 子类并逐个执行，
/// 所以这里不需要改任何框架文件。
/// </para>
/// <para>
/// ★ 只做「不符合约定就不让启动」的硬校验。这类校验必须放在启动期：
/// 时间语义错了不会立刻报错，而是几个月后在对账时才发现，那时已经无法区分哪些行是错的。
/// </para>
/// </summary>
public class PayCenterStartup : AppStartup
{
    /// <summary>
    /// 是否真的执行过 <see cref="ConfigureServices"/>。
    /// <para>
    /// ★ 存在的理由：Furion 的 <see cref="AppStartup"/> 是按<b>子类扫描 + 反射调用</b>执行的，
    /// 所以「类型能被扫到」和「方法真的被调用」是两件事 ——
    /// 基类方法若不是 <c>virtual</c>（或子类漏写 <c>override</c>），子类方法只会<b>隐藏</b>基类方法，
    /// 反射按基类方法调用时就会执行到空实现：守卫变成<b>静默空操作</b>，代码看起来完全正常。
    /// 这个标记让测试能直接断言「方法被调用过」，而不是只断言「类型存在」。
    /// </para>
    /// </summary>
    public static bool ConfigureServicesInvoked { get; private set; }

    public void ConfigureServices(IServiceCollection services)
    {
        ConfigureServicesInvoked = true;

        // 时间基准：pay_* 的时间列是 timestamp without time zone，语义为「+08 墙上时间」，
        // 进程时区不是 +08 会让新旧数据在同一列里混入两种含义且不报错 → 直接失败。
        PayTimeBasis.EnsureLocalTimeBasis();
    }
}
