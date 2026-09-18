// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 基础策略接口
/// </summary>
public interface ICodeGenStrategy
{
}

/// <summary>
/// 泛型策略接口
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class CodeGenStrategy<T> : ICodeGenStrategy
{
    protected DbConnectionOptions _dbConnectionOption => App.GetOptions<DbConnectionOptions>();
    protected CodeGenOptions _codeGenOption => App.GetOptions<CodeGenOptions>();
    protected ISqlSugarClient _db => App.GetRequiredService<ISqlSugarClient>();
    protected IViewEngine _viewEngine => App.GetRequiredService<IViewEngine>();

    public abstract Task<List<TemplateContextOutput>> GenerateCode(T input);

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板上下文</param>
    /// <param name="action">渲染动作</param>
    /// <returns></returns>
    protected async Task<TemplateContextOutput> RenderAsync(TemplateContextOutput template, Func<string, Action<IViewEngineOptionsBuilder>, string> action)
    {
        try
        {
            var nameSpace = this.GetType().Namespace;
            string content = await File.ReadAllTextAsync(Path.Combine(Path.Combine(App.WebHostEnvironment.WebRootPath, "template"), template.Name));
            template.Context = action.Invoke(content, builder =>
            {
                builder.AddAssemblyReferenceByName("System.Text.RegularExpressions");
                builder.AddAssemblyReferenceByName("System.Collections");
                builder.AddAssemblyReferenceByName("System.Linq");
                builder.AddUsing("System.Text.RegularExpressions");
                builder.AddUsing("System.Collections.Generic");
                builder.AddUsing("System.Linq");
                builder.AddUsing(nameSpace);
            });
            template.Name = template.Name.Replace(".vm", "");
            return template;
        }
        catch (Exception ex)
        {
            try
            {
                var debugDir = Path.Combine(App.WebHostEnvironment.WebRootPath, "template_debug");
                Directory.CreateDirectory(debugDir);
                void Extract(Exception e, int level = 1)
                {
                    if (e == null) return;
                    var type = e.GetType();
                    var prop = type.GetProperty("GeneratedCode");
                    var code = prop?.GetValue(e) as string;
                    if (!string.IsNullOrEmpty(code))
                    {
                        var fileName = Path.Combine(debugDir, template.Name + $".{level}.GeneratedCode.cs.txt");
                        File.WriteAllText(fileName, code);
                    }
                    prop = type.GetProperty("SourceCode");
                    code = prop?.GetValue(e) as string;
                    if (!string.IsNullOrEmpty(code))
                    {
                        var fileName = Path.Combine(debugDir, template.Name + $".{level}.SourceCode.cs.txt");
                        File.WriteAllText(fileName, code);
                    }
                    Extract(e.InnerException, level + 1);
                }
                Extract(ex);
            }
            catch { }
            throw new Exception($"【{template.Name}】渲染失败,中间文件存入在template_debug目录，错误原因：{ex.Message}");
        }
    }
}