// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

// 配置启动类类型，第一个参数是 TestProgram 类完整限定名，第二个参数是当前项目程序集名称

using Furion.Xunit;
using Xunit;
using Xunit.Abstractions;

[assembly: TestFramework("Admin.NET.Test.TestProgram", "Admin.NET.Test")]

// 关闭程序集级并行：所有用例共享同一个应用宿主与同一个单例 SqlSugarScope。
// xUnit 默认让不同测试类并行执行，多个线程会落到同一个连接作用域上，
// 报「Connection open error . Connection already open」。
// 这类用例的价值在断言业务不变量（额度不超发、状态迁移、号段唯一），不在压测并发；
// 真实并发由 HTTP 压测脚本覆盖——SqlSugarScope 的连接按请求上下文隔离，进程内并发本来就测不出真并发。
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Admin.NET.Test;

/// <summary>
/// 单元测试启动类
/// </summary>
public class TestProgram : TestStartup
{
    public TestProgram(IMessageSink messageSink) : base(messageSink)
    {
        Serve.RunNative();
    }
}