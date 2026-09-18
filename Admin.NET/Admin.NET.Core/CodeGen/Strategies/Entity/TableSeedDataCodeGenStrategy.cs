// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 表种子数据代码生成策略类
/// </summary>
[CodeGenStrategy(CodeGenSceneEnum.TableSeedData)]
public class TableSeedDataCodeGenStrategy : CodeGenEntityStrategyBase<CreateSeedDataInput>, ISingleton
{
    private readonly TemplateContextOutput _template = new() { Name = "SeedData.cs.vm", OutPath = "SeedData/{ModuleName}SeedData.cs" };

    public override async Task<List<TemplateContextOutput>> GenerateCode(CreateSeedDataInput input)
    {
        var config = _dbConnectionOption.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == input.ConfigId) ?? throw Oops.Oh(ErrorCodeEnum.db1004);
        input.Position = string.IsNullOrWhiteSpace(input.Position) ? "X.Core" : input.Position;

        var db = _db.AsTenant().GetConnectionScope(input.ConfigId);
        var tableInfo = db.DbMaintenance.GetTableInfoList(false).First(u => u.Name == input.TableName); // 表名
        List<DbColumnInfo> dbColumnInfos = db.DbMaintenance.GetColumnInfosByTableName(input.TableName, false); // 所有字段
        IEnumerable<EntityInfo> entityInfos = await GetEntityInfos();
        Type entityType = null;
        foreach (var item in entityInfos)
        {
            if (tableInfo.Name.ToLower() != (config.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(item.DbTableName) : item.DbTableName).ToLower()) continue;
            entityType = item.Type;
            break;
        }
        if (entityType == null) throw Oops.Oh(ErrorCodeEnum.db1003);

        input.EntityName = entityType.Name;
        input.SeedDataName = entityType.Name + "SeedData";
        if (!string.IsNullOrWhiteSpace(input.Suffix)) input.SeedDataName += input.Suffix;

        // 查询所有数据
        var query = db.QueryableByObject(entityType);

        // 排除【字典表】中自动生成的枚举类型
        // 如果 entityType.Name=="SysDictType"或"SysDictData"或"SysDictDataTenant" 加入查询条件 u.IsEnum!=1
        if (input.FilterExistingData)
        {
            if (entityType.Name == "SysDictType" || entityType.Name == "SysDictData" || entityType.Name == "SysDictDataTenant")
            {
                if (config.DbSettings.EnableUnderLine)
                    query = query.Where("is_enum != 1");
                else
                    query = query.Where("IsEnum != 1");
            }
        }

        // 优先用创建时间排序
        DbColumnInfo orderField = dbColumnInfos.FirstOrDefault(u => u.DbColumnName.ToLower() is "create_time" or "createtime");
        if (orderField != null) query = query.OrderBy(orderField.DbColumnName);

        // 优先用创建时间排序,再使用第一个主键排序
        if (dbColumnInfos.Any(u => u.IsPrimarykey))
            query = query.OrderBy(dbColumnInfos.First(u => u.IsPrimarykey).DbColumnName);
        var records = ((IEnumerable)await query.ToListAsync()).ToDynamicList();

        // 过滤已存在的数据
        if (input.FilterExistingData && records.Any())
        {
            // 获取实体类型-所有种数据数据类型
            var entityTypes = App.EffectiveTypes.Where(u => u.FullName != null && !u.IsInterface && !u.IsAbstract && u.IsClass && u.IsDefined(typeof(SugarTable), false) && u.FullName.EndsWith("." + input.EntityName))
                .Where(u => !u.GetCustomAttributes<IgnoreTableAttribute>().Any())
                .ToList();
            if (entityTypes.Count == 1) // 只有一个实体匹配才能过滤
            {
                // 获取实体的主键对应的属性名称
                var pkInfo = entityTypes[0].GetProperties().FirstOrDefault(u => u.GetCustomAttribute<SugarColumn>()?.IsPrimaryKey == true);
                if (pkInfo != null)
                {
                    var seedDataTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces()
                        .Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarEntitySeedData<>)) && i.GenericTypeArguments[0] == entityTypes[0])).ToList();
                    // 可能会重名的种子数据不作为过滤项
                    string doNotFilterFullName1 = $"{input.Position}.SeedData.{input.SeedDataName}";
                    string doNotFilterFullName2 = $"{input.Position}.{input.SeedDataName}"; // Core中的命名空间没有SeedData

                    PropertyInfo idPropertySeedData = records[0].GetType().GetProperty("Id");
                    for (int i = seedDataTypes.Count - 1; i >= 0; i--)
                    {
                        string fullName = seedDataTypes[i].FullName;
                        if ((fullName == doNotFilterFullName1) || (fullName == doNotFilterFullName2)) continue;

                        // 删除重复数据
                        var instance = Activator.CreateInstance(seedDataTypes[i]);
                        var hasDataMethod = seedDataTypes[i].GetMethod("HasData");
                        var seedData = ((IEnumerable)hasDataMethod?.Invoke(instance, null))?.Cast<object>();
                        if (seedData == null) continue;

                        List<object> recordsToRemove = new();
                        foreach (var record in records)
                        {
                            object recordId = pkInfo.GetValue(record);
                            if (seedData.Select(d1 => idPropertySeedData.GetValue(d1)).Any(dataId => recordId != null && dataId != null && recordId.Equals(dataId))) recordsToRemove.Add(record);
                        }
                        foreach (var itemToRemove in recordsToRemove) records.Remove(itemToRemove);
                    }
                }
            }
        }

        // 获取所有字段信息
        var propertyList = entityType.GetProperties().Where(x => false == (x.GetCustomAttribute<SugarColumn>()?.IsIgnore ?? false)).ToList();
        for (var i = 0; i < propertyList.Count; i++)
        {
            if (propertyList[i].Name != nameof(EntityBaseId.Id) || !(propertyList[i].GetCustomAttribute<SugarColumn>()?.IsPrimaryKey ?? true)) continue;
            var temp = propertyList[i];
            for (var j = i; j > 0; j--) propertyList[j] = propertyList[j - 1];
            propertyList[0] = temp;
        }

        // 拼接种子数据
        var recordList = records.Select(obj => string.Join(", ", propertyList.Select(prop =>
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            object value = prop.GetValue(obj);
            if (value == null) value = "null";
            else if (propType == typeof(string))
            {
                value = (value as string)?.Replace("\"", "\"\"");
                value = $"@\"{value}\"";
            }
            else if (propType.IsEnum)
            {
                value = $"{propType.Name}.{value}";
            }
            else if (propType == typeof(bool))
            {
                value = (bool)value ? "true" : "false";
            }
            else if (propType == typeof(DateTime))
            {
                value = $"DateTime.Parse(\"{((DateTime)value):yyyy-MM-dd HH:mm:ss.fff}\")";
            }
            return $"{prop.Name}={value}";
        }))).ToList();

        var engine = new TableSeedDataEngine
        {
            NameSpace = $"{input.Position}.SeedData",
            EntityName = input.EntityName,
            SeedDataName = input.SeedDataName,
            Description = tableInfo.Description,
            RecordList = recordList
        };
        var result = await RenderAsync(_template.DeepCopy(), (context, builder) => _viewEngine.RunCompile(context, engine, builder));
        result.OutPath = Path.Combine(Path.Combine(new DirectoryInfo(App.WebHostEnvironment.ContentRootPath).Parent!.FullName, input.Position), result.OutPath.Replace("{ModuleName}SeedData", input.SeedDataName));
        return [result];
    }

    /// <summary>
    /// 获取库表信息
    /// </summary>
    /// <returns></returns>
    private async Task<IEnumerable<EntityInfo>> GetEntityInfos()
    {
        var type = typeof(SugarTable);
        var types = new List<Type>();
        var entityInfos = new List<EntityInfo>();
        if (_codeGenOption.EntityAssemblyNames != null)
            foreach (var asm in _codeGenOption.EntityAssemblyNames.Select(Assembly.Load))
                types.AddRange(asm.GetExportedTypes().ToList());

        Type[] cosType = types.Where(u => IsMyAttribute(Attribute.GetCustomAttributes(u, true))).ToArray();
        foreach (var c in cosType)
        {
            var sugarAttribute = c.GetCustomAttributes(type, true).FirstOrDefault();
            var des = c.GetCustomAttributes(typeof(DescriptionAttribute), true);
            var description = des.Length > 0 ? ((DescriptionAttribute)des[0]).Description : "";
            entityInfos.Add(new EntityInfo
            {
                EntityName = c.Name,
                DbTableName = sugarAttribute == null ? c.Name : ((SugarTable)sugarAttribute).TableName,
                TableDescription = description,
                Type = c
            });
        }
        return await Task.FromResult(entityInfos);

        bool IsMyAttribute(Attribute[] o)
        {
            return o.Any(a => a.GetType() == type);
        }
    }
}