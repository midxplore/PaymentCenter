// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.IO.Compression;

namespace Admin.NET.Core;

/// <summary>
/// 系统代码生成器服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 270, Description = "代码生成器")]
public class SysCodeGenService : IDynamicApiController, ITransient
{
    private readonly CodeGenStrategyFactory _codeGenStrategyFactory;
    private readonly SysCodeGenColumnService _codeGenColumnService;
    private readonly DbConnectionOptions _dbConnectionOptions;
    private readonly SysCacheService _sysCacheService;
    private readonly SysMenuService _sysMenuService;
    private readonly CodeGenOptions _codeGenOptions;
    private readonly ISqlSugarClient _db;

    public SysCodeGenService(
        IOptions<DbConnectionOptions> dbConnectionOptions,
        CodeGenStrategyFactory codeGenStrategyFactory,
        SysCodeGenColumnService codeGenColumnService,
        IOptions<CodeGenOptions> codeGenOptions,
        SysCacheService sysCacheService,
        SysMenuService sysMenuService,
        ISqlSugarClient db)
    {
        _db = db;
        _sysMenuService = sysMenuService;
        _sysCacheService = sysCacheService;
        _codeGenOptions = codeGenOptions.Value;
        _codeGenColumnService = codeGenColumnService;
        _codeGenStrategyFactory = codeGenStrategyFactory;
        _dbConnectionOptions = dbConnectionOptions.Value;
    }

    /// <summary>
    /// 获取代码生成分页列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成分页列表")]
    public async Task<SqlSugarPagedList<SysCodeGen>> Page(PageCodeGenInput input)
    {
        return await _db.Queryable<SysCodeGen>().Includes(u => u.TableList)
            .WhereIF(!string.IsNullOrWhiteSpace(input.TableName), u => u.TableList.Any(a => a.TableName.Contains(input.TableName.Trim()) || a.EntityName.Contains(input.TableName.Trim())))
            .WhereIF(!string.IsNullOrWhiteSpace(input.BusName), u => u.BusName.Contains(input.BusName.Trim()))
            .OrderBy(u => u.Id, OrderByType.Desc)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>
    /// 增加代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("增加代码生成")]
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    public async Task AddCodeGen(AddCodeGenInput input)
    {
        try
        {
            // 验证参数
            ValidateTemplateParameters(input.Adapt<UpdateCodeGenInput>());

            await _db.Ado.BeginTranAsync();

            // 保存代码生成记录
            var codegen = input.Adapt<SysCodeGen>();
            await _db.Insertable(codegen).ExecuteCommandAsync();

            // 保存关联表
            codegen.TableList.ForEach(e => e.CodeGenId = codegen.Id);
            await _db.Insertable(codegen.TableList).ExecuteCommandAsync();

            // 处理并保存表字段配置
            codegen.TableList.ForEach(e =>
            {
                int orderNo = 1;
                e.ColumnList = e.ColumnList.DistinctBy(u => u.PropertyName).ToList(); // 去重
                e.ColumnList.ForEach(a =>
                {
                    a.CodeGenTableId = e.Id; // 关联表id
                    a.OrderNo = orderNo++; // 排序
                });
            });
            await _db.Storageable(codegen.TableList.SelectMany(e => e.ColumnList).ToList()).ExecuteCommandAsync();
            await _db.Ado.CommitTranAsync();
        }
        catch (Exception)
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 更新代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("更新代码生成")]
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    public async Task UpdateCodeGen(UpdateCodeGenInput input)
    {
        try
        {
            // 验证参数
            ValidateTemplateParameters(input);

            await _db.Ado.BeginTranAsync();

            // 保存代码生成记录
            var codegen = input.Adapt<SysCodeGen>();
            await _db.Updateable(codegen).ExecuteCommandAsync();

            // 保存关联表
            codegen.TableList.ForEach(e => e.CodeGenId = codegen.Id);
            await _db.Storageable(codegen.TableList).ExecuteCommandAsync();

            // 更新配置表
            var tableIds = codegen.TableList.Select(w => w.Id).ToList();
            await _db.Deleteable<SysCodeGenColumn>().Where(u => tableIds.Contains(u.CodeGenTableId)).ExecuteCommandAsync();

            // 处理并保存表字段配置
            codegen.TableList.ForEach(e =>
            {
                int orderNo = 1;
                e.ColumnList = e.ColumnList.DistinctBy(u => u.PropertyName).ToList(); // 去重
                e.ColumnList.ForEach(a =>
                {
                    a.CodeGenTableId = e.Id; // 关联表id
                    a.OrderNo = orderNo++; // 排序
                });
            });
            await _db.Storageable(codegen.TableList.SelectMany(e => e.ColumnList).ToList()).ExecuteCommandAsync();
            await _db.Ado.CommitTranAsync();
        }
        catch (Exception)
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 删除代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("删除代码生成")]
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    public async Task DeleteCodeGen(BaseIdInput input)
    {
        try
        {
            await _db.Ado.BeginTranAsync();
            var entity = await GetDetail(input.Id);
            if (entity == null) return;

            await _db.Deleteable(entity).ExecuteCommandAsync();

            var tableList = entity.TableList;
            await _db.Deleteable(tableList).ExecuteCommandAsync();

            // 删除表字段配置
            var tableIds = tableList.Select(u => u.Id).ToList();
            await _db.Deleteable<SysCodeGenColumn>().Where(u => tableIds.Contains(u.CodeGenTableId))
                .ExecuteCommandAsync();
            await _db.Ado.CommitTranAsync();
        }
        catch (Exception)
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 同步代码生成 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("同步代码生成")]
    [ApiDescriptionSettings(Name = "Sync"), HttpPost]
    public async Task SyncCodeGen(BaseIdInput input)
    {
        try
        {
            await _db.Ado.BeginTranAsync();
            var entity = await GetDetail(input.Id);
            if (entity == null) return;

            var addColumns = new List<SysCodeGenColumn>();
            var updateColumns = new List<SysCodeGenColumn>();
            var deleteColumns = new List<SysCodeGenColumn>();
            foreach (var tableConfig in entity.TableList)
            {
                // 获取默认字段配置
                var columns = await GetDefaultColumnConfigList(new DefaultColumnConfigInput { ConfigId = tableConfig.ConfigId, TableName = tableConfig.TableName });

                // 计算新增字段
                var addList = columns.Where(u => tableConfig.ColumnList.All(a => a.PropertyName != u.PropertyName))
                    .Select(u =>
                    {
                        u.CodeGenTableId = tableConfig.Id;
                        return u;
                    }).ToList();

                // 计算更新字段
                var updateList = columns.Join(tableConfig.ColumnList,
                        c => c.PropertyName,
                        t => t.PropertyName,
                        (c, t) => new { Column = c, OldColumn = t })
                    .Where(x => x.Column.NetType != x.OldColumn.NetType ||
                                x.Column.DataType != x.OldColumn.DataType ||
                                x.Column.IsPrimarykey != x.OldColumn.IsPrimarykey ||
                                x.Column.IsRequired != x.OldColumn.IsRequired ||
                                x.Column.ColumnLength != x.OldColumn.ColumnLength)
                    .Select(x =>
                    {
                        x.Column.Id = x.OldColumn.Id;
                        x.Column.CodeGenTableId = x.OldColumn.CodeGenTableId;
                        return x.Column;
                    }).ToList();

                // 计算删除字段
                var deleteList = tableConfig.ColumnList.Where(u => columns.All(c => c.PropertyName != u.PropertyName)).ToList();

                // 追加到集合
                addColumns.AddRange(addList);
                updateColumns.AddRange(updateList);
                deleteColumns.AddRange(deleteList);
            }

            // 保存新增、更新、删除字段
            if (addColumns.Count > 0) await _db.Insertable(addColumns).ExecuteCommandAsync();
            if (updateColumns.Count > 0) await _db.Updateable(updateColumns).ExecuteCommandAsync();
            if (deleteColumns.Count > 0) await _db.Deleteable(deleteColumns).ExecuteCommandAsync();

            await _db.Ado.CommitTranAsync();
        }
        catch (Exception)
        {
            await _db.Ado.RollbackTranAsync();
            throw;
        }
    }

    /// <summary>
    /// 获取代码生成详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成详情")]
    public async Task<SysCodeGen> GetDetail([FromQuery] BaseIdInput input)
    {
        return await GetDetail(input.Id);
    }

    /// <summary>
    /// 获取代码生成详情 🔖
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [NonAction]
    private async Task<SysCodeGen> GetDetail(long id)
    {
        return await _db.Queryable<SysCodeGen>().Includes(u => u.TableList, w => w.ColumnList).FirstAsync(u => u.Id == id);
    }

    /// <summary>
    /// 获取代码生成表详情 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取代码生成表详情")]
    public async Task<SysCodeGenTable> GetTableDetail([FromQuery] BaseIdInput input)
    {
        return await _db.Queryable<SysCodeGenTable>().Includes(u => u.ColumnList).FirstAsync(u => u.Id == input.Id);
    }

    /// <summary>
    /// 获取默认表字段配置列表 🔖
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [DisplayName("获取默认表字段配置列表")]
    [ApiDescriptionSettings, HttpPost]
    public async Task<List<SysCodeGenColumn>> GetDefaultColumnConfigList(DefaultColumnConfigInput input)
    {
        var list = await GetColumnListByTableName(input.TableName, input.ConfigId);
        return _codeGenColumnService.GetDefaultColumnConfigList(list, 0);
    }

    /// <summary>
    /// 获取数据库集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取数据库集合")]
    public async Task<List<DatabaseOutput>> GetDatabaseList()
    {
        var dbConfigs = _dbConnectionOptions.ConnectionConfigs;
        return await Task.FromResult(dbConfigs.Adapt<List<DatabaseOutput>>());
    }

    /// <summary>
    /// 获取数据库表(实体)集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取数据库表(实体)集合")]
    public async Task<List<TableOutput>> GetTableList(string configId = SqlSugarConst.MainConfigId)
    {
        var provider = _db.AsTenant().GetConnectionScope(configId);
        var dbTableInfos = provider.DbMaintenance.GetTableInfoList(false);

        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => configId.Equals(u.ConfigId));

        IEnumerable<EntityInfo> entityInfos = await _codeGenColumnService.GetEntityInfos(); // 获取所有实体定义
        entityInfos = entityInfos.OrderBy(u => u.EntityName.StartsWith("Sys") ? 1 : 0).ThenBy(u => u.EntityName);

        var tableOutputList = new List<TableOutput>();
        foreach (var item in entityInfos)
        {
            var tbConfigId = item.Type.GetCustomAttribute<TenantAttribute>()?.configId as string ?? SqlSugarConst.MainConfigId;
            if (item.Type.IsDefined(typeof(LogTableAttribute))) tbConfigId = SqlSugarConst.LogConfigId;
            if (tbConfigId != configId) continue;

            var dbTableName = item.DbTableName;
            int bracketIndex = dbTableName.IndexOf('{');
            if (bracketIndex != -1)
                dbTableName = dbTableName.Substring(0, bracketIndex);

            var table = dbTableInfos.FirstOrDefault(u => u.Name.ToLower().Equals((config != null && config.DbSettings.EnableUnderLine ? UtilMethods.ToUnderLine(dbTableName) : dbTableName).ToLower()));
            if (table == null) continue;

            // 计算字段个数
            var columnCount = 0;
            try
            {
                var columns = await GetColumnListByTableName(table.Name, configId);
                columnCount = columns?.Count ?? 0;
            }
            catch (Exception)
            {
                // 如果获取字段失败，设为0，不影响主流程
                columnCount = 0;
            }

            // 获取程序集名称
            var assemblyName = item.Type.Assembly.ManifestModule.Name ?? "";

            tableOutputList.Add(new TableOutput
            {
                ConfigId = configId,
                EntityName = item.EntityName,
                TableName = table.Name,
                TableComment = item.TableDescription,
                ColumnCount = columnCount,
                AssemblyName = assemblyName
            });
        }
        return tableOutputList;
    }

    /// <summary>
    /// 根据表名获取列集合 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("根据表名获取列集合")]
    public async Task<List<ColumnOutput>> GetColumnListByTableName([Required] string tableName, string configId = SqlSugarConst.MainConfigId)
    {
        // 切库---多库代码生成用
        var provider = _db.AsTenant().GetConnectionScope(configId);

        var config = _dbConnectionOptions.ConnectionConfigs.FirstOrDefault(u => u.ConfigId.ToString() == configId);
        // 获取实体类型属性
        tableName = CodeGenHelper.GetRealName(tableName, config);
        var entityType = provider.DbMaintenance.GetTableInfoList(false).FirstOrDefault(u => u.Name == tableName);
        if (entityType == null) return null;
        //var entityBasePropertyNames = _codeGenOptions.EntityBaseColumn[nameof(EntityTenantBaseData)];

        var entityList = await _codeGenColumnService.GetEntityInfos();
        var entityInfo = entityList.FirstOrDefault(u => CodeGenHelper.GetRealName(u.DbTableName, config).EqualIgnoreCase(tableName));
        if (entityInfo == null) throw new Exception($"未找到实体类型：{tableName}");

        var entityProps = entityInfo.Type.GetProperties();
        var sugarProperties = entityProps.Where(u => u.GetCustomAttribute<SugarColumn>()?.IsIgnore == false).Select(u => new
        {
            PropertyInfo = u,
            PropertyName = u.Name,
            NetType = u.PropertyType,
            EntityInfo = entityInfo,
            ColumnComment = u.GetCustomAttribute<SugarColumn>()?.ColumnDescription,
            ColumnName = CodeGenHelper.GetRealName(u.GetCustomAttribute<SugarColumn>()?.ColumnName ?? u.Name, config),
            ForeignProperty = entityProps.FirstOrDefault(w => w.GetCustomAttribute<Navigate>()?.GetName() == u.Name)
        }).ToList();

        // 按原始类型的顺序获取所有实体类型属性（不包含导航属性，会返回null）
        var columnList = provider.DbMaintenance.GetColumnInfosByTableName(entityType.Name).Select(u => new ColumnOutput
        {
            IsNullable = u.IsNullable,
            IsPrimarykey = u.IsPrimarykey,
            DataType = u.DataType.ToString(),
            ColumnComment = u.ColumnDescription,
            ColumnName = CodeGenHelper.GetRealName(u.DbColumnName, config),
            NetType = CodeGenHelper.ConvertDataType(u, provider.CurrentConnectionConfig.DbType)
        }).ToList();

        foreach (var property in sugarProperties)
        {
            var column = columnList.FirstOrDefault(u => u.ColumnName?.ToUpper() == property.ColumnName?.ToUpper());// Oracle 全部字段大小，与实体不一置
            if (column == null) continue;

            column.ColumnComment ??= property.ColumnComment;
            column.PropertyName = property.PropertyName;
            column.PropertyType = property.NetType;
            column.PropertyInfo = property.PropertyInfo;

            // 设置外键配置
            if (property.ForeignProperty != null)
            {
                column.IsForeignKey = true;
                column.ForeignProperty = property.ForeignProperty;
                column.ForeignEntityType = property.ForeignProperty.PropertyType.GetGenericArguments().FirstOrDefault() ?? property.ForeignProperty.PropertyType;
            }
        }
        return columnList.Where(u => !string.IsNullOrWhiteSpace(u.PropertyName)).ToList();
    }

    /// <summary>
    /// 获取程序保存位置 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取程序保存位置")]
    public List<string> GetApplicationNamespaces()
    {
        return _codeGenOptions.BackendApplicationNamespaces;
    }

    /// <summary>
    /// 获取快捷设置外键配置 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取快捷设置外键配置")]
    public async Task<dynamic> GetQuickConfigMap()
    {
        return new
        {
            sysUser = await GetEffectTreeConfig<SysUser>(u => u.Id, u => u.RealName, u => u.RealName),
            sysOrg = await GetEffectTreeConfig<SysOrg>(u => u.Id, u => u.Name, u => u.Name, u => u.Pid)
        };
    }

    /// <summary>
    /// 获取联表配置信息
    /// </summary>
    /// <param name="linkExp"></param>
    /// <param name="dispExp"></param>
    /// <param name="searchExp"></param>
    /// <param name="parentExp"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private async Task<EffectTreeConfigInput> GetEffectTreeConfig<T>(Expression<Func<T, object>> linkExp, Expression<Func<T, object>> dispExp, Expression<Func<T, object>> searchExp, Expression<Func<T, object>> parentExp = null)
    {
        var entity = _db.EntityMaintenance.GetEntityInfoNoCache(typeof(T));
        var configId = entity.Type.GetCustomAttribute<TenantAttribute>()?.configId?.ToString() ?? SqlSugarConst.MainConfigId;
        var provider = _db.AsTenant().GetConnectionScope(configId);
        var linkProperty = GetPropertyInfo(linkExp);
        var disProperty = GetPropertyInfo(dispExp);
        var searchProperty = GetPropertyInfo(searchExp);
        var parentProperty = GetPropertyInfo(parentExp);

        // 获取字段对应的 .NET 类型
        var columnNetTypeMap = (await GetColumnListByTableName(entity.Type.Name, configId))
            ?.ToDictionary(u => u.PropertyName, u => u.NetType) ?? new();

        return new()
        {
            ConfigId = configId,
            EntityName = entity.EntityName,
            TableName = entity.DbTableName,
            TableComment = entity.TableDescription,
            TreeTitle = entity.TableDescription,
            LinkPropertyName = linkProperty.Name,
            DisplayPropertyNames = disProperty.Name,
            SearchPropertyName = searchProperty.Name,
            ParentPropertyName = parentProperty?.Name,
            LinkPropertyType = columnNetTypeMap.GetValueOrDefault(linkProperty.Name),
            SearchPropertyType = columnNetTypeMap.GetValueOrDefault(searchProperty.Name),
            ParentPropertyType = columnNetTypeMap.GetValueOrDefault(parentProperty?.Name ?? ""),
        };
    }

    /// <summary>
    /// 获取表达式属性信息
    /// </summary>
    /// <param name="expression"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private PropertyInfo GetPropertyInfo<T>(Expression<Func<T, object>> expression)
    {
        if (expression == null) return null;
        if (expression.Body is UnaryExpression { Operand: MemberExpression member }) return (PropertyInfo)member.Member;
        if (expression.Body is MemberExpression memberExpression) return (PropertyInfo)memberExpression.Member;
        throw Oops.Oh("表达式必须是一个属性访问: " + expression);
    }

    /// <summary>
    /// 执行代码生成 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("执行代码生成")]
    public async Task<dynamic> Generate(BaseIdInput input)
    {
        var codeGen = await GetDetail(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        var codeGenStrategy = _codeGenStrategyFactory.GetStrategy<SysCodeGen>(codeGen.Scene);
        var templateList = await codeGenStrategy.GenerateCode(codeGen);

        // 删除旧目录
        string outputPath = Path.Combine(App.WebHostEnvironment.WebRootPath, "codeGen", codeGen.ModuleName);
        if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);

        // 输出文件
        foreach (var template in templateList)
        {
            var dirPath = new DirectoryInfo(template.OutPath).Parent!.FullName;
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            await File.WriteAllTextAsync(template.OutPath, template.Context);
        }

        // 生成菜单
        if (codeGen.GenerateMenu)
        {
            var menuList = await GetMenusByCodeGen(codeGen);
            await AddMenu(menuList, codeGen.MenuPid);
        }

        // 非下载模式则返回空
        if (!codeGen.GenerateMethod.ToString().StartsWith("DownloadZip")) return null;

        // 删除同名文件
        var downloadPath = outputPath + ".zip";
        if (File.Exists(downloadPath)) File.Delete(downloadPath);

        // 创建压缩文件并下载
        ZipFile.CreateFromDirectory(outputPath, downloadPath);
        return new { url = $"{App.HttpContext.Request.Scheme}://{App.HttpContext.Request.Host.Value}/codeGen/{codeGen.ModuleName}.zip" };
    }

    /// <summary>
    /// 获取代码生成预览 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取代码生成预览")]
    public async Task<Dictionary<string, string>> Preview(BaseIdInput input)
    {
        var codeGen = await GetDetail(input.Id) ?? throw Oops.Oh(ErrorCodeEnum.D1002);
        var codeGenStrategy = _codeGenStrategyFactory.GetStrategy<SysCodeGen>(codeGen.Scene);
        return (await codeGenStrategy.GenerateCode(codeGen)).ToDictionary(u => u.Name, u => u.Context);
    }

    /// <summary>
    /// 增加菜单
    /// </summary>
    /// <param name="menus"></param>
    /// <param name="pid"></param>
    /// <returns></returns>
    private async Task AddMenu(List<SysMenu> menus, long pid)
    {
        var first = menus.First();
        var menu = await _db.Queryable<SysMenu>().FirstAsync(u => u.Type == first.Type && u.Name == first.Name && u.Pid == first.Pid);
        if (menu != null) await _sysMenuService.DeleteMenu(new() { Id = menu.Id });

        await _db.Insertable(menus).ExecuteCommandAsync();

        // 删除角色菜单按钮缓存
        _sysCacheService.RemoveByPrefixKey(CacheConst.KeyUserApi);
    }

    /// <summary>
    /// 获取菜单列表
    /// </summary>
    /// <param name="codeGen"></param>
    /// <returns></returns>
    private async Task<List<SysMenu>> GetMenusByCodeGen(SysCodeGen codeGen)
    {
        string pPath;
        // 若 pid=0 为顶级则创建菜单目录
        SysMenu menuType0 = null;
        long tempPid = codeGen.MenuPid;
        var menuList = new List<SysMenu>();
        var classNameLower = codeGen.ModuleName.ToLower();
        var classNameFirstLower = codeGen.ModuleName.ToFirstLetterLowerCase();
        if (codeGen.MenuPid == 0)
        {
            // 目录
            menuType0 = new SysMenu
            {
                Id = YitIdHelper.NextId(),
                Pid = 0,
                Title = codeGen.BusName + "管理",
                Type = MenuTypeEnum.Dir,
                Icon = "ele-Menu",
                Path = $"/{classNameLower}Manage",
                Name = classNameFirstLower + "Manage",
                Component = "Layout",
                OrderNo = 100,
                CreateTime = DateTime.Now
            };
            codeGen.MenuPid = menuType0.Id;
            pPath = menuType0.Path;
        }
        else
        {
            var pMenu = await _db.Queryable<SysMenu>().FirstAsync(u => u.Id == codeGen.MenuPid) ?? throw Oops.Oh(ErrorCodeEnum.D1505);
            pPath = pMenu.Path;
        }

        // 菜单
        var menuType = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = codeGen.MenuPid,
            Title = codeGen.BusName + "管理",
            Name = classNameFirstLower,
            Type = MenuTypeEnum.Menu,
            Icon = codeGen.MenuIcon,
            Path = pPath + "/" + classNameLower,
            Component = $"/{codeGen.PagePath}/{classNameFirstLower}/index"
        };

        var menuPid = menuType.Id;
        int menuOrder = 100;
        // 按钮-page
        var menuTypePage = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = menuPid,
            Title = "查询",
            Type = MenuTypeEnum.Btn,
            Permission = classNameFirstLower + "/page",
            OrderNo = menuOrder,
            CreateTime = DateTime.Now
        };
        menuOrder += 10;
        menuList.Add(menuTypePage);

        // 按钮-detail
        var menuTypeDetail = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = menuPid,
            Title = "详情",
            Type = MenuTypeEnum.Btn,
            Permission = classNameFirstLower + "/detail",
            OrderNo = menuOrder,
            CreateTime = DateTime.Now
        };
        menuOrder += 10;
        menuList.Add(menuTypeDetail);

        // 按钮-add
        var menuTypeAdd = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = menuPid,
            Title = "增加",
            Type = MenuTypeEnum.Btn,
            Permission = classNameFirstLower + "/add",
            OrderNo = menuOrder,
            CreateTime = DateTime.Now
        };
        menuOrder += 10;
        menuList.Add(menuTypeAdd);

        // 按钮-delete
        var menuTypeDelete = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = menuPid,
            Title = "删除",
            Type = MenuTypeEnum.Btn,
            Permission = classNameFirstLower + "/delete",
            OrderNo = menuOrder,
            CreateTime = DateTime.Now
        };
        menuOrder += 10;
        menuList.Add(menuTypeDelete);

        // 按钮-update
        var menuTypeUpdate = new SysMenu
        {
            Id = YitIdHelper.NextId(),
            Pid = menuPid,
            Title = "编辑",
            Type = MenuTypeEnum.Btn,
            Permission = classNameFirstLower + "/update",
            OrderNo = menuOrder,
            CreateTime = DateTime.Now
        };
        menuList.Add(menuTypeUpdate);
        menuList.Insert(0, menuType);
        if (tempPid == 0) menuList.Insert(0, menuType0); // 顶级目录需要添加目录本身
        return menuList;
    }

    /// <summary>
    /// 验证模板参数
    /// </summary>
    /// <param name="input"></param>
    [NonAction]
    public void ValidateTemplateParameters(UpdateCodeGenInput input)
    {
        switch (input.Scene)
        {
            case CodeGenSceneEnum.SingleTable: // 单表操作
                break;

            case CodeGenSceneEnum.TreeSingleTable: // 树单表操作
                if (input.TreeConfig == null) throw Oops.Oh(ErrorCodeEnum.D1402);
                input.TreeConfig.Adapt<TreeWithTableConfigInput>().Validate();
                input.Validate();
                break;
        }
        // 字段组件配置参数校验
        foreach (var column in input.TableList.SelectMany(x => x.ColumnList))
        {
            if (column.IsCommon) return;
            if (column.EffectType is CodeGenEffectTypeEnum.DictSelector or
                    CodeGenEffectTypeEnum.EnumSelector or CodeGenEffectTypeEnum.ConstSelector
                    or CodeGenEffectTypeEnum.ApiTreeSelector or CodeGenEffectTypeEnum.ForeignKey
                    or CodeGenEffectTypeEnum.Upload or CodeGenEffectTypeEnum.DatePicker && column.Config == null) throw Oops.Oh(ErrorCodeEnum.D1403);
            switch (column.EffectType)
            {
                case CodeGenEffectTypeEnum.DictSelector or CodeGenEffectTypeEnum.EnumSelector or CodeGenEffectTypeEnum.ConstSelector:
                    JSON.Deserialize<EffectDictConfigInput>(column.Config)?.Validate();
                    break;

                case CodeGenEffectTypeEnum.ApiTreeSelector:
                    JSON.Deserialize<EffectTreeConfigInput>(column.Config)?.Validate();
                    break;

                case CodeGenEffectTypeEnum.ForeignKey:
                    JSON.Deserialize<EffectForeignKeyConfigInput>(column.Config)?.Validate();
                    break;

                case CodeGenEffectTypeEnum.Upload:
                    JSON.Deserialize<EffectFileConfigInput>(column.Config)?.Validate();
                    break;

                case CodeGenEffectTypeEnum.DatePicker:
                    JSON.Deserialize<EffectFileConfigInput>(column.Config)?.Validate();
                    break;

                default:
                    column.Config = null;
                    break;
            }
        }
    }
}