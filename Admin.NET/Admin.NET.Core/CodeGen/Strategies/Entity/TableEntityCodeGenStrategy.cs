// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 表实体代码生成策略类
/// </summary>
[CodeGenStrategy(CodeGenSceneEnum.TableEntity)]
public class TableEntityCodeGenStrategy : CodeGenEntityStrategyBase<CreateEntityInput>, ISingleton
{
    private readonly TemplateContextOutput _template = new() { Name = "Entity.cs.vm", OutPath = "Entity/{ModuleName}.cs" };

    public override async Task<List<TemplateContextOutput>> GenerateCode(CreateEntityInput input)
    {
        var config = _dbConnectionOption.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == input.ConfigId) ?? throw Oops.Oh(ErrorCodeEnum.db1004);
        input.EntityName = string.IsNullOrWhiteSpace(input.EntityName) ? (config.DbSettings.EnableUnderLine ? CodeGenHelper.CamelColumnName(input.TableName, null) : input.TableName) : input.EntityName;
        input.Position = string.IsNullOrWhiteSpace(input.Position) ? "X.Application" : input.Position;

        // Entity.cs.vm中是允许创建没有基类的实体的，所以这里也要做出相同的判断
        string[] dbColumnNames = [];
        if (!string.IsNullOrWhiteSpace(input.BaseClassName))
        {
            _codeGenOption.EntityBaseColumn.TryGetValue(input.BaseClassName, out dbColumnNames);
            if (dbColumnNames is null or { Length: 0 }) throw Oops.Oh("基类配置文件不存在此类型");
        }

        var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
        var dbColumnInfos = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false);
        dbColumnInfos.ForEach(u =>
        {
            u.PropertyName = config.DbSettings.EnableUnderLine ? CodeGenHelper.CamelColumnName(u.DbColumnName, dbColumnNames) : u.DbColumnName; // 转下划线后的列名需要再转回来
            u.DataType = CodeGenHelper.ConvertDataType(u, config.DbType);
        });
        if (_codeGenOption.BaseEntityNames.Contains(input.BaseClassName, StringComparer.OrdinalIgnoreCase))
            dbColumnInfos = dbColumnInfos.Where(u => !dbColumnNames.Contains(u.PropertyName, StringComparer.OrdinalIgnoreCase)).ToList();

        var dbTableInfo = db.DbMaintenance.GetTableInfoList(false).FirstOrDefault(u => u.Name == input.TableName || u.Name == input.TableName.ToLower()) ?? throw Oops.Oh(ErrorCodeEnum.db1001);

        var engine = new TableEntityEngine
        {
            NameSpace = $"{input.Position}.Entity",
            TableName = input.TableName,
            EntityName = input.EntityName,
            BaseClassName = string.IsNullOrWhiteSpace(input.BaseClassName) ? "" : $": {input.BaseClassName}",
            ConfigId = input.ConfigId,
            Description = string.IsNullOrWhiteSpace(dbTableInfo.Description)
                ? input.EntityName + "业务表"
                : dbTableInfo.Description,
            TableFields = dbColumnInfos
        };
        var result = await RenderAsync(_template, (context, builder) => _viewEngine.RunCompile(context, engine, builder =>
        {
            builder.AddAssemblyReferenceByName("SqlSugar");
        }));
        result.OutPath = Path.Combine(Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.FullName, input.Position), result.OutPath.Replace("{ModuleName}", input.EntityName));
        return [result];
    }
}