// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 主从表代码生成策略类
/// </summary>
[CodeGenStrategy(CodeGenSceneEnum.MasterSlaveTables)]
public class MasterSlaveTablesCodeGenStrategy : CodeGenTableStrategyBase<SysCodeGen>, ISingleton
{
    public override async Task<List<TemplateContextOutput>> GenerateCode(SysCodeGen input)
    {
        var mainTableInfo = input.TableList.FirstOrDefault() ?? throw Oops.Oh(ErrorCodeEnum.D1401);
        var columnList = mainTableInfo.ColumnList.Adapt<List<CodeGenColumnConfig>>();

        var slaveTableInfo = input.TableList.Skip(1).FirstOrDefault() ?? throw Oops.Oh(ErrorCodeEnum.D1401);
        var slaveColumnList = slaveTableInfo.ColumnList.Adapt<List<CodeGenColumnConfig>>();

        // 创建视图引擎数据
        var engine = new CustomTemplateEngine
        {
            PagePath = input.PagePath, // 前端目录
            NameSpace = input.NameSpace, // 命名空间
            ClassName = mainTableInfo.EntityName, // 类名称
            ModuleName = mainTableInfo.EntityName, // 模块名称
            TreeConfig = input.TreeConfig, // 树组件配置
            LastLinkPropertyName = mainTableInfo.LastLinkPropertyName, // 上级联表字段
            NextLinkPropertyName = mainTableInfo.NextLinkPropertyName, // 下级联表字段
            LowerClassName = mainTableInfo.EntityName[..1].ToLower() + mainTableInfo.EntityName[1..], // 首字母小写的类名称
            ConfigId = mainTableInfo.ConfigId, // 库定位器名
            AllFields = columnList, // 所有字段集

            SlaveTable = new CustomTemplateEngine
            {
                PagePath = input.PagePath, // 前端目录
                NameSpace = input.NameSpace, // 命名空间
                BusName = slaveTableInfo.BusName,
                ClassName = slaveTableInfo.EntityName, // 类名称
                ModuleName = slaveTableInfo.EntityName, // 模块名称
                LastLinkPropertyName = slaveTableInfo.LastLinkPropertyName, // 上级联表字段
                NextLinkPropertyName = slaveTableInfo.NextLinkPropertyName, // 下级联表字段
                LowerClassName = slaveTableInfo.EntityName[..1].ToLower() + slaveTableInfo.EntityName[1..], // 首字母小写的类名称
                ConfigId = slaveTableInfo.ConfigId, // 库定位器名
                AllFields = slaveColumnList, // 所有字段集
            },
        };

        // 获取模板列表
        var templateList = GetTemplateList(input);

        // 处理模板输出路径
        templateList.ForEach(e => ReplaceTemplateOutput(e, input));

        // 渲染模板
        var result = await RenderList(input, templateList, engine);

        // 渲染从表编辑组件
        var slaveTemplate = GetTemplateList(input).FirstOrDefault(u => u.Type == 1010);
        if (slaveTemplate != null)
        {
            result.AddRange(await RenderList(input, [slaveTemplate], engine.SlaveTable));
            slaveTemplate.Name = slaveTemplate.Name.Replace("editDialog", "slave_editDialog");
            slaveTemplate.OutPath = slaveTemplate.OutPath.Replace("{ModuleName}", "{SlaveModuleName}");
            ReplaceTemplateOutput(slaveTemplate, input);
        }

        // 如果需要生成从表接口服务
        templateList = GetTemplateList(input).Where(u => u.Type >= 2000).ToList();
        templateList.ForEach(e =>
        {
            e.OutPath = e.OutPath.Replace("{ModuleName}", "{SlaveModuleName}");
            ReplaceTemplateOutput(e, input);
        });
        var slaveResult = await RenderList(input, templateList, engine.SlaveTable);
        slaveResult.ForEach(u => u.Name = u.Name.Replace("service_", "service_slave_"));
        result.AddRange(slaveResult);

        return result;
    }
}