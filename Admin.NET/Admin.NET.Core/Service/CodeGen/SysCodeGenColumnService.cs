// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Admin.NET.Core;

/// <summary>
/// 代码生成表字段配置 🧩
/// </summary>
[ApiDescriptionSettings(Order = 260, Description = "代码生成表字段配置")]
public class SysCodeGenColumnService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarClient _db;
    private readonly CodeGenOptions _codeGenOptions;
    private readonly DbConnectionOptions _dbConnectionOptions;

    public SysCodeGenColumnService(ISqlSugarClient db,
        IOptions<DbConnectionOptions> dbConnectionOptions,
        IOptions<CodeGenOptions> codeGenOptions)
    {
        _db = db;
        _dbConnectionOptions = dbConnectionOptions.Value;
        _codeGenOptions = codeGenOptions.Value;
    }

    /// <summary>
    /// 获取数据表列（实体属性）集合
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<ColumnOutput>> GetColumnList([FromQuery] BaseIdInput input)
    {
        var genTable = await _db.Queryable<SysCodeGenTable>().FirstAsync(u => u.Id == input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);

        var entityType = GetEntityInfos().ConfigureAwait(false).GetAwaiter().GetResult().FirstOrDefault(u => u.EntityName == genTable.EntityName);
        if (entityType == null) return null;

        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == genTable.ConfigId);
        var dbTableName = genTable.TableName;

        int bracketIndex = dbTableName.IndexOf('{');
        if (bracketIndex != -1)
        {
            dbTableName = dbTableName[..bracketIndex];
            var dbTableInfos = _db.AsTenant().GetConnectionScope(genTable.ConfigId).DbMaintenance.GetTableInfoList(false);
            var table = dbTableInfos.FirstOrDefault(u => u.Name.StartsWith(config!.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(dbTableName) : dbTableName, StringComparison.CurrentCultureIgnoreCase));
            if (table != null) dbTableName = table.Name;
        }

        // 切库---多库代码生成用
        var provider = _db.AsTenant().GetConnectionScope(!string.IsNullOrEmpty(genTable.ConfigId) ? genTable.ConfigId : SqlSugarConst.MainConfigId);

        var entityBasePropertyNames = _codeGenOptions.EntityBaseColumn[nameof(EntityTenantBaseData)];
        var columnInfos = provider.DbMaintenance.GetColumnInfosByTableName(dbTableName, false);
        var result = columnInfos.Select(u => new ColumnOutput
        {
            ColumnName = u.DbColumnName,
            ColumnLength = u.Length,
            IsPrimarykey = u.IsPrimarykey,
            IsNullable = u.IsNullable,
            DataType = u.DataType,
            NetType = CodeGenHelper.ConvertDataType(u, provider.CurrentConnectionConfig.DbType),
            ColumnComment = string.IsNullOrWhiteSpace(u.ColumnDescription) ? u.DbColumnName : u.ColumnDescription,
            DefaultValue = u.DefaultValue,
        }).ToList();

        // 获取实体的属性信息，赋值给PropertyName属性(CodeFirst模式应以PropertyName为实际使用名称)
        var entityProperties = entityType.Type.GetProperties();
        for (int i = result.Count - 1; i >= 0; i--)
        {
            var columnOutput = result[i];
            // 先找自定义字段名的，如果找不到就再找自动生成字段名的(并且过滤掉没有SugarColumn的属性)
            var propertyInfo = entityProperties.FirstOrDefault(u => string.Equals((u.GetCustomAttribute<SugarColumn>()?.ColumnName ?? ""), columnOutput.ColumnName, StringComparison.CurrentCultureIgnoreCase)) ??
                entityProperties.FirstOrDefault(u => u.GetCustomAttribute<SugarColumn>() != null && u.Name.Equals(config!.DbSettings.EnableUnderLine
                ? CodeGenHelper.CamelColumnName(columnOutput.ColumnName, entityBasePropertyNames).ToLower()
                : columnOutput.ColumnName.ToLower(), StringComparison.CurrentCultureIgnoreCase));
            if (propertyInfo != null)
            {
                columnOutput.PropertyName = propertyInfo.Name;
                columnOutput.ColumnComment = propertyInfo.GetCustomAttribute<SugarColumn>()!.ColumnDescription;
            }
            else
            {
                result.RemoveAt(i); // 移除没有定义此属性的字段
            }
        }
        return result;
    }

    /// <summary>
    /// 获取库表信息
    /// </summary>
    /// <param name="excludeSysTable">是否排除带SysTable属性的表</param>
    /// <returns></returns>
    public async Task<IEnumerable<EntityInfo>> GetEntityInfos(bool excludeSysTable = false)
    {
        var types = new List<Type>();
        if (_codeGenOptions.EntityAssemblyNames != null)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var assemblyName = assembly.GetName().Name!;
                if (_codeGenOptions.EntityAssemblyNames.Contains(assemblyName) || _codeGenOptions.EntityAssemblyNames.Any(name => assemblyName.Contains(name)))
                {
                    Assembly asm = Assembly.Load(assemblyName);
                    types.AddRange(asm.GetExportedTypes().ToList());
                }
            }
        }
        var sugarTableType = typeof(SugarTable);
        bool IsMyAttribute(Attribute[] o)
        {
            foreach (Attribute a in o)
            {
                if (a.GetType() == sugarTableType)
                    return true;
            }
            return false;
        }
        Type[] cosType = types.Where(u => IsMyAttribute(Attribute.GetCustomAttributes(u, false))).ToArray();

        var entityInfos = new List<EntityInfo>();
        foreach (var ct in cosType)
        {
            // 若实体贴[SysTable]特性，则禁止显示系统自带的
            if (excludeSysTable && ct.IsDefined(typeof(SysTableAttribute), false)) continue;

            var des = ct.GetCustomAttributes(typeof(DescriptionAttribute), false);
            var description = des.Length > 0 ? ((DescriptionAttribute)des[0]).Description : "";
            var sugarAttribute = ct.GetCustomAttributes(sugarTableType, true).FirstOrDefault();

            entityInfos.Add(new EntityInfo()
            {
                EntityName = ct.Name,
                DbTableName = sugarAttribute == null ? ct.Name : ((SugarTable)sugarAttribute).TableName,
                TableDescription = sugarAttribute == null ? description : ((SugarTable)sugarAttribute).TableDescription,
                Type = ct
            });
        }
        return await Task.FromResult(entityInfos);
    }

    /// <summary>
    /// 获取代码生成配置列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成配置列表")]
    public async Task<List<CodeGenColumnConfig>> GetList([FromQuery] BaseIdInput input)
    {
        // 获取主表
        var codeGenTable = await _db.Queryable<SysCodeGenTable>().FirstAsync(u => u.Id == input.Id);

        // 获取配置的字段
        var genConfigColumnList = await _db.Queryable<SysCodeGenColumn>().Where(u => u.CodeGenTableId == input.Id).ToListAsync();

        // 获取实体所有字段
        var tableColumnList = await GetColumnList(new BaseIdInput { Id = input.Id });

        // 获取新增的字段
        var addColumnList = tableColumnList.Where(u => !genConfigColumnList.Select(d => d.ColumnName).Contains(u.ColumnName)).ToList();

        // 获取删除的字段
        var delColumnList = genConfigColumnList.Where(u => !tableColumnList.Select(d => d.ColumnName).Contains(u.ColumnName)).ToList();

        // 获取更新的字段
        var updateColumnList = new List<SysCodeGenColumn>();
        foreach (var column in genConfigColumnList)
        {
            // 获取没有增减的
            if (tableColumnList.Any(u => u.ColumnName == column.ColumnName))
            {
                var nmd = tableColumnList.Single(u => u.ColumnName == column.ColumnName);
                // 如果数据库类型或者长度改变
                if (nmd.NetType != column.NetType || nmd.ColumnLength != column.ColumnLength || nmd.ColumnComment != column.ColumnComment)
                {
                    column.NetType = nmd.NetType;
                    column.ColumnLength = nmd.ColumnLength;
                    column.ColumnComment = nmd.ColumnComment;
                    updateColumnList.Add(column);
                }
            }
        }

        // 增加新增的
        if (addColumnList.Count > 0) AddList(addColumnList, codeGenTable);

        // 删除没有的
        if (delColumnList.Count > 0) await _db.Deleteable(delColumnList).ExecuteCommandAsync();

        // 更新配置
        if (updateColumnList.Count > 0) await _db.Updateable(updateColumnList).ExecuteCommandAsync();

        // 重新获取配置
        return await _db.Queryable<SysCodeGenColumn>()
            .Where(u => u.CodeGenTableId == input.Id)
            .Select<CodeGenColumnConfig>()
            .OrderBy(u => new { u.OrderNo, u.Id })
            .ToListAsync();
    }

    /// <summary>
    /// 更新代码生成配置 🔖
    /// </summary>
    /// <param name="inputList"></param>
    /// <returns></returns>
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    [DisplayName("更新代码生成配置")]
    public async Task UpdateCodeGenConfig(List<SysCodeGenColumn> inputList)
    {
        if (inputList == null || inputList.Count < 1) return;

        await _db.Updateable(inputList)
            .IgnoreColumns(u => new
            {
                u.CodeGenTableId,
                u.ColumnLength,
                u.ColumnName,
                u.PropertyName,
                u.IsPrimarykey,
                u.IsCommon,
            })
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除代码生成配置
    /// </summary>
    /// <param name="tableId"></param>
    /// <returns></returns>
    [NonAction]
    public async Task DeleteCodeGenConfig(long tableId)
    {
        await _db.Deleteable<SysCodeGenColumn>().Where(u => u.CodeGenTableId == tableId).ExecuteCommandAsync();
    }

    /// <summary>
    /// 获取代码生成配置详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成配置详情")]
    public async Task<SysCodeGenColumn> GetDetail([FromQuery] CodeGenColumnConfig input)
    {
        return await _db.Queryable<SysCodeGenColumn>().SingleAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 获取默认列配置
    /// </summary>
    /// <param name="tableColumnOutputList"></param>
    /// <param name="codeGenTableId"></param>
    [NonAction]
    public List<SysCodeGenColumn> GetDefaultColumnConfigList(List<ColumnOutput> tableColumnOutputList, long codeGenTableId)
    {
        if (tableColumnOutputList == null) return null;
        var settings = new JsonSerializer { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        var orderNo = 1;
        var columnConfigList = new List<SysCodeGenColumn>();
        foreach (var tableColumn in tableColumnOutputList)
        {
            if (codeGenTableId > 0 && _db.Queryable<SysCodeGenColumn>().Any(u => u.ColumnName == tableColumn.ColumnName && u.CodeGenTableId == codeGenTableId))
                continue;

            var columnConfig = new SysCodeGenColumn();
            var yesOrNo = !tableColumn.IsPrimarykey;
            if (CodeGenHelper.IsCommonColumn(tableColumn.PropertyName))
            {
                columnConfig.OrderNo = tableColumnOutputList.Count + orderNo;
                columnConfig.IsCommon = true;
                yesOrNo = false;
            }
            else
            {
                columnConfig.IsQuery = true;
                columnConfig.IsCommon = false;
                columnConfig.OrderNo = orderNo;
            }

            columnConfig.NetType = tableColumn.NetType;
            columnConfig.CodeGenTableId = codeGenTableId;
            columnConfig.ColumnName = tableColumn.ColumnName; // 字段名
            columnConfig.PropertyName = tableColumn.PropertyName; // 实体属性名
            columnConfig.ColumnLength = tableColumn.ColumnLength; // 长度
            columnConfig.IsPrimarykey = tableColumn.IsPrimarykey;
            columnConfig.ColumnComment = Regex.Replace(tableColumn.ColumnComment ?? "", @"(id|Id|ID)$", "");

            // 是否必填验证
            columnConfig.IsQuery = yesOrNo;
            columnConfig.IsTable = yesOrNo;
            columnConfig.IsImport = yesOrNo;
            columnConfig.IsSortable = yesOrNo;
            columnConfig.IsAddUpdate = yesOrNo;
            columnConfig.DataType = tableColumn.DataType;
            columnConfig.QueryType = GetDefaultQueryType(columnConfig);
            columnConfig.DefaultValue = GetDefaultValue(tableColumn.DefaultValue);
            columnConfig.EffectType = CodeGenHelper.DataTypeToEff(columnConfig.NetType);
            var propertyType = Nullable.GetUnderlyingType(tableColumn.PropertyType ?? typeof(object)) ?? tableColumn.PropertyType;
            if (propertyType is { IsEnum: true }) // 枚举类型
            {
                columnConfig.EffectType = CodeGenEffectTypeEnum.EnumSelector;
                columnConfig.Config = new { Code = propertyType.Name }.ToJson();
                columnConfig.QueryType = "==";
            }
            else if (tableColumn.PropertyInfo.IsDefined(typeof(DictAttribute), true)) // 字段特性
            {
                var attribute = tableColumn.PropertyInfo.GetCustomAttribute<DictAttribute>();
                columnConfig.EffectType = CodeGenEffectTypeEnum.DictSelector;
                columnConfig.Config = new { Code = attribute?.DictTypeCode }.ToJson();
                columnConfig.QueryType = "==";
            }
            else if (tableColumn.PropertyInfo.IsDefined(typeof(ConstAttribute), true)) // 常量特性
            {
                var attribute = tableColumn.PropertyInfo.GetCustomAttribute<ConstAttribute>();
                columnConfig.EffectType = CodeGenEffectTypeEnum.DictSelector;
                columnConfig.Config = new { Code = attribute?.Name }.ToJson();
                columnConfig.QueryType = "==";
            }
            else
            {
                columnConfig.Config = null;
            }

            if (columnConfig.EffectType == CodeGenEffectTypeEnum.DatePicker)
            {
                columnConfig.Config = new EffectDatePickerConfigInput { Format = "datetime" }.ToJson();
            }

            // 是否是外键
            if (tableColumn.IsForeignKey)
            {
                // 设置外键配置
                if (tableColumn.ForeignProperty != null)
                {
                    var fkEntityInfo = _db.EntityMaintenance.GetEntityInfoNoCache(tableColumn.ForeignEntityType);
                    var fkConfigId = tableColumn.ForeignEntityType.GetCustomAttribute<TenantAttribute>()?.configId as string ?? SqlSugarConst.MainConfigId;
                    var displayProperty = fkEntityInfo.Columns.FirstOrDefault(u => u.PropertyName.Contains("Name") && u.PropertyInfo.PropertyType.Name == "String");
                    var pidProperty = fkEntityInfo.Columns.FirstOrDefault(u => u.PropertyName is "Pis" or "pid");
                    columnConfig.EffectType = pidProperty != null ? CodeGenEffectTypeEnum.ApiTreeSelector : CodeGenEffectTypeEnum.ForeignKey;
                    columnConfig.Config = new EffectTreeConfigInput
                    {
                        ConfigId = fkConfigId,
                        LinkPropertyName = "Id",
                        LinkPropertyType = "long?",
                        EntityName = fkEntityInfo.EntityName,
                        TableName = fkEntityInfo.DbTableName,
                        TableComment = fkEntityInfo.TableDescription,
                        DisplayPropertyNames = displayProperty?.PropertyName,
                        ParentPropertyName = pidProperty?.PropertyName,
                        ParentPropertyType = pidProperty?.PropertyInfo.PropertyType.Name,
                        SearchPropertyName = displayProperty?.PropertyName,
                        SearchPropertyType = displayProperty?.PropertyInfo.PropertyType.Name,
                    }.ToJson();
                }
            }

            columnConfig.IsRequired = columnConfig.IsRequired || columnConfig.IsPrimarykey || !tableColumn.IsNullable;
            columnConfigList.Add(columnConfig);
            orderNo += 1; // 每个配置排序间隔1
        }
        return columnConfigList.OrderBy(u => u.OrderNo).ToList();
    }

    /// <summary>
    /// 批量增加代码生成配置
    /// </summary>
    /// <param name="tableColumnOutputList"></param>
    /// <param name="codeGenTable"></param>
    [NonAction]
    public void AddList(List<ColumnOutput> tableColumnOutputList, SysCodeGenTable codeGenTable)
    {
        if (tableColumnOutputList == null) return;

        var columnConfigList = new List<SysCodeGenColumn>();
        var orderNo = 1;
        foreach (var tableColumn in tableColumnOutputList)
        {
            if (_db.Queryable<SysCodeGenColumn>().Any(u => u.ColumnName == tableColumn.ColumnName && u.CodeGenTableId == codeGenTable.Id))
                continue;

            var columnConfig = new SysCodeGenColumn();

            var yesOrNo = !(tableColumn.IsPrimarykey || tableColumn.IsForeignKey);

            if (CodeGenHelper.IsCommonColumn(tableColumn.PropertyName))
            {
                columnConfig.IsCommon = true;
                yesOrNo = false;
            }
            else
            {
                columnConfig.IsCommon = false;
            }

            columnConfig.CodeGenTableId = codeGenTable.Id;
            columnConfig.ColumnName = tableColumn.ColumnName; // 字段名
            columnConfig.PropertyName = tableColumn.PropertyName;// 实体属性名
            columnConfig.ColumnLength = tableColumn.ColumnLength;// 长度
            columnConfig.ColumnComment = tableColumn.ColumnComment;
            columnConfig.NetType = tableColumn.NetType;

            // 是否必填验证
            columnConfig.IsQuery = false;
            columnConfig.IsTable = yesOrNo;
            columnConfig.IsAddUpdate = yesOrNo;

            columnConfig.DataType = tableColumn.DataType;
            columnConfig.EffectType = CodeGenHelper.DataTypeToEff(columnConfig.NetType);
            columnConfig.QueryType = GetDefaultQueryType(columnConfig); // QueryTypeEnum.eq.ToString();
            columnConfig.DefaultValue = GetDefaultValue(tableColumn.DefaultValue);
            columnConfig.OrderNo = orderNo;
            columnConfigList.Add(columnConfig);

            orderNo += 1; // 每个配置排序间隔1
        }
        // 多库代码生成---这里要切回主库
        var provider = _db.AsTenant().GetConnectionScope(SqlSugarConst.MainConfigId);
        provider.Insertable(columnConfigList).ExecuteCommand();
    }

    /// <summary>
    /// 默认查询类型
    /// </summary>
    /// <param name="columnConfig"></param>
    /// <returns></returns>
    private static string GetDefaultQueryType(SysCodeGenColumn columnConfig)
    {
        return (columnConfig.NetType?.TrimEnd('?')) switch
        {
            "string" => "like",
            "DateTime" => "~",
            _ => "==",
        };
    }

    /// <summary>
    /// 获取默认值
    /// </summary>
    /// <param name="dataValue"></param>
    /// <returns></returns>
    private static string? GetDefaultValue(string dataValue)
    {
        if (dataValue == null) return null;
        // 正则表达式模式
        // \( 和 \) 用来匹配字面量的括号
        // .+ 用来匹配一个或多个任意字符，但不包括换行符
        string pattern = @"\((.+)\)";//适合MSSQL其他数据库没有测试

        // 使用 Regex 类进行匹配
        Match match = Regex.Match(dataValue, pattern);

        string value;
        // 如果找到了匹配项
        if (match.Success)
        {
            // 提取括号内的值
            value = match.Groups[1].Value.Trim('\'');
        }
        else
        {
            value = dataValue;
        }
        return value;
    }
}