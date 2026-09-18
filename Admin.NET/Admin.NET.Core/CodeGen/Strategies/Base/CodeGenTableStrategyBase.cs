// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 表策略基类（处理SysCodeGen类型）
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class CodeGenTableStrategyBase<T> : CodeGenStrategy<T> where T : SysCodeGen
{
    protected IViewEngine ViewEngine => App.GetRequiredService<IViewEngine>();
    protected CodeGenOptions CodeGenOption => App.GetOptions<CodeGenOptions>();

    /// <summary>
    /// 模板上下文列表
    /// </summary>
    protected static List<TemplateContextOutput> TemplateList => new()
    {
        new() { Type = 900, Name = "web_api.ts.vm", OutPath = "api/{PagePath}/{ModuleNameLower}.ts" },
        new() { Type = 1000, Name = "web_views_index.vue.vm", OutPath = "views/{PagePath}/{ModuleNameLower}/index.vue" },
        new() { Type = 1010, Name = "web_views_editDialog.vue.vm", OutPath = "views/{PagePath}/{ModuleNameLower}/component/edit{ModuleName}Dialog.vue" },
        new() { Type = 1020, Name = "web_views_Tree.vue.vm", OutPath = "views/{PagePath}/{ModuleNameLower}/component/{LowerTreeEntityName}Tree.vue" },

        new() { Type = 2000, Name = "service_Service.cs.vm", OutPath = "Service/{ModuleName}/{ModuleName}Service.cs" },
        new() { Type = 2010, Name = "service_OutputDto.cs.vm", OutPath = "Service/{ModuleName}/Dto/{ModuleName}Output.cs" },
        new() { Type = 2020, Name = "service_InputDto.cs.vm", OutPath = "Service/{ModuleName}/Dto/{ModuleName}Input.cs" },
    };

    /// <summary>
    /// 根据生成方式获取模板路径列表
    /// </summary>
    /// <returns></returns>
    protected List<TemplateContextOutput> GetTemplateList(SysCodeGen codeGen)
    {
        var templateList = codeGen.GenerateMethod switch
        {
            CodeGenMethodEnum.DownloadZipFrontend or CodeGenMethodEnum.GenerateToProjectFrontend => TemplateList.Where(u => u.Type < 2000).ToList(),
            CodeGenMethodEnum.DownloadZipBackend or CodeGenMethodEnum.GenerateToProjectBackend => TemplateList.Where(u => u.Type >= 2000).ToList(),
            _ => TemplateList
        };
        // 传统接口模式去掉 web_api 模板
        if (codeGen.IsApiService) templateList = templateList.Where(e => e.Type != 900).ToList();
        // 非树组件场景去掉树组件模板
        if (codeGen.Scene.ToInt() % 100 != 10) templateList = templateList.Where(e => e.Type != 1020).ToList();
        return templateList;
    }

    /// <summary>
    /// 处理模板输出路径
    /// </summary>
    /// <param name="template"></param>
    /// <param name="codeGen"></param>
    protected void ReplaceTemplateOutput(TemplateContextOutput template, SysCodeGen codeGen)
    {
        // 处理模板输出路径
        string outputPath = Path.Combine(App.WebHostEnvironment.WebRootPath, "codeGen", codeGen.ModuleName);
        template.OutPath = template.OutPath.Replace("{PagePath}", codeGen.PagePath)
            .Replace("{ModuleName}", codeGen.ModuleName)
            .Replace("{ModuleNameLower}", codeGen.ModuleName.ToFirstLetterLowerCase())
            .Replace("{LowerTreeEntityName}", codeGen.TreeConfig?.EntityName.ToFirstLetterLowerCase());

        // 替换从表模板输出路径占位符
        if (codeGen.TableList.Count > 1) template.OutPath = template.OutPath.Replace("{SlaveModuleName}", codeGen.TableList.Skip(1).First().ModuleName);

        string tempPath;
        if ((int)codeGen.GenerateMethod < 200)
        {
            // 下载模式下的路径处理
            tempPath = !template.OutPath.EndsWith(".cs")
                ? Path.Combine(outputPath, CodeGenOption.FrontRootPath, "src")
                : Path.Combine(outputPath, codeGen.NameSpace);
        }
        else
        {
            // 生成到本项目下的路径处理
            tempPath = !template.OutPath.EndsWith(".cs")
                ? Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.Parent!.FullName, CodeGenOption.FrontRootPath, "src")
                : Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.FullName, codeGen.NameSpace!);
        }
        template.OutPath = Path.Combine(tempPath, template.OutPath);
    }

    /// <summary>
    /// 批量渲染模板
    /// </summary>
    /// <param name="codeGen"></param>
    /// <param name="templateList"></param>
    /// <param name="engine"></param>
    /// <returns></returns>
    protected async Task<List<TemplateContextOutput>> RenderList(SysCodeGen codeGen, List<TemplateContextOutput> templateList, CustomTemplateEngine engine)
    {
        void SetEnginePropertyValue(CustomTemplateEngine input)
        {
            input.ModuleName ??= codeGen.ModuleName; // 模块名称
            input.BusName ??= codeGen.BusName; // 业务名
            input.AuthorName ??= codeGen.AuthorName; // 作者
            input.Email ??= codeGen.Email; // 作者邮箱
            input.NameSpace = codeGen.NameSpace; // 命名空间
            input.PagePath = codeGen.PagePath; // 页面目录
            input.Scene = codeGen.Scene.ToInt(); // 场景
            input.ProjectLastName = codeGen.NameSpace!.Split('.').Last(); // 项目最后个名称，生成的时候赋值
            input.IsApiService = codeGen.IsApiService; // 是否使用 Api Service
            input.PrintType = codeGen.PrintType.ToString(); // 支持打印类型
            input.PrintName = codeGen.PrintName; // 打印模板名称
            input.LastLinkPropertyName ??= input.LastLinkPropertyName; // 上级关联字段
            input.NextLinkPropertyName ??= input.NextLinkPropertyName; // 下级关联字段
            input.IsHorizontal = codeGen.IsHorizontal;

            input.QueryFields = input.AllFields.Where(e => e.IsQuery).ToList(); // 查询字段列表
            input.TableFields = input.AllFields.Where(e => e.IsTable).ToList(); // 表格字段列表
            input.ImportFields = input.AllFields.Where(e => e.IsImport).ToList(); // 导入导出字段列表
            input.PrimaryFields = input.AllFields.Where(e => e.IsPrimarykey).ToList(); // 主键字段列表
            input.AddUpdateFields = input.AllFields.Where(e => e.IsAddUpdate).ToList(); // 增改字段列表
            input.UploadFields = input.AllFields.Where(e => e.EffectType == CodeGenEffectTypeEnum.Upload).ToList(); // 上传文件字段列表
            input.DropdownFields = input.AllFields.Where(e => e.EffectType is CodeGenEffectTypeEnum.ApiTreeSelector or CodeGenEffectTypeEnum.ForeignKey or CodeGenEffectTypeEnum.DictSelector or CodeGenEffectTypeEnum.EnumSelector).ToList(); // 选择类字段列表
            var map = new Dictionary<string, int>();
            input.ApiFields = input.AllFields.Where(e => e.IsTree || e.IsForeignKey).Where(e => map.TryAdd(e.JoinConfig.EntityName + e.IsTree, 1)).ToList();

            // input.ColumnList = await CodeGenColumnService.GetColumnList(new BaseIdInput{ Id = codeGen.TableList.First().Id }); // 数据库字段列表,
            input.IsOnlyIdPrimary = input.AllFields.Where(u => u.IsPrimarykey).All(u => u.PropertyName == "Id"); // 是否主键只有Id
            input.HasStatus = input.AllFields.Any(u => u.PropertyName == nameof(BaseStatusInput.Status) && u.DictConfig?.Code?.Trim('?') == nameof(StatusEnum)); // 是否有启用禁用字段
            input.HasJoinTable = input.AllFields.Any(e => e.EffectType is CodeGenEffectTypeEnum.ForeignKey or CodeGenEffectTypeEnum.ApiTreeSelector); // 是否有联表
            input.HasUpload = input.UploadFields.Count != 0; // 是否有上传

            // 校验模板参数
            input.Validate();

            // 必须有主键
            if (!input.PrimaryFields.Any()) throw Oops.Oh(ErrorCodeEnum.D1405);
        }

        // 设置主表参数
        SetEnginePropertyValue(engine);

        // 设置从表参数
        if (engine.SlaveTable != null) SetEnginePropertyValue(engine.SlaveTable);

        var tasks = templateList.Select(template => Task.Run(() => RenderAsync(template, (context, builder) => _viewEngine.RunCompile(context, engine, builder)))).ToList();
        await Task.WhenAll(tasks);

        // 获取结果集
        var result = new List<TemplateContextOutput>();
        foreach (var task in tasks) result.Add(await task);

        // 处理输出路径
        return result;
    }
}