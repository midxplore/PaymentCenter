// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 单表代码生成策略类
/// </summary>
[CodeGenStrategy(CodeGenSceneEnum.SingleTable)]
public class SingleTableCodeGenStrategy : CodeGenTableStrategyBase<SysCodeGen>, ISingleton
{
    public override async Task<List<TemplateContextOutput>> GenerateCode(SysCodeGen input)
    {
        var tableInfo = input.TableList.FirstOrDefault() ?? throw Oops.Oh(ErrorCodeEnum.D1401);
        var columnList = tableInfo.ColumnList.Adapt<List<CodeGenColumnConfig>>();

        // 创建视图引擎数据
        var engine = new CustomTemplateEngine
        {
            PagePath = input.PagePath, // 前端目录
            NameSpace = input.NameSpace, // 命名空间
            ClassName = tableInfo.EntityName, // 类名称
            ModuleName = tableInfo.EntityName, // 模块名称
            LowerClassName = tableInfo.EntityName[..1].ToLower() + tableInfo.EntityName[1..], // 首字母小写的类名称
            TreeConfig = input.TreeConfig, // 树组件配置
            ConfigId = tableInfo.ConfigId, // 库定位器名
            AllFields = columnList, // 所有字段集
            LastLinkPropertyName = tableInfo.LastLinkPropertyName, // 上级关联字段名
            NextLinkPropertyName = tableInfo.NextLinkPropertyName, // 下级关联字段名
        };

        // 获取模板列表
        var templateList = GetTemplateList(input);

        // 处理模板输出路径
        templateList.ForEach(e => ReplaceTemplateOutput(e, input));

        // 渲染
        return await RenderList(input, templateList, engine);
    }
}