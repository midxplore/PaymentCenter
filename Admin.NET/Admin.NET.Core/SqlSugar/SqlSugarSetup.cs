// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

public static class SqlSugarSetup
{
    // 多租户实例
    public static ITenant ITenant { get; set; }

    /// <summary>
    /// 缓存实例
    /// </summary>
    private static readonly ICache _cache = Cache.Default;

    /// <summary>
    /// SqlSugar 上下文初始化
    /// </summary>
    /// <param name="services"></param>
    public static void AddSqlSugar(this IServiceCollection services)
    {
        // 注册雪花Id
        SnowIdOptions snowIdOpt = App.GetConfig<SnowIdOptions>("SnowId", true);
        services.AddYitIdHelper(snowIdOpt);

        // 自定义雪花Id算法
        StaticConfig.CustomSnowFlakeFunc = YitIdHelper.NextId;
        // 配置字符串表达式
        StaticConfig.DynamicExpressionParserType = typeof(DynamicExpressionParser);
        StaticConfig.DynamicExpressionParsingConfig = new ParsingConfig
        {
            CustomTypeProvider = new SqlSugarTypeProvider()
        };
        //// 配置库表差异日志(审计)
        //StaticConfig.CompleteInsertableFunc =
        //StaticConfig.CompleteUpdateableFunc =
        //StaticConfig.CompleteDeleteableFunc = u =>
        //{
        //    var method = u.GetType().GetMethod("EnableDiffLogEvent");
        //    method.Invoke(u, [null]);
        //};

        // 初始化所有库连接
        var dbOptions = App.GetConfig<DbConnectionOptions>("DbConnection", true);
        dbOptions.ConnectionConfigs.ForEach(SetDbConfig);

        // 初始化SqlSugar
        SqlSugarScope sqlSugar = new(dbOptions.ConnectionConfigs.Adapt<List<ConnectionConfig>>(), db =>
        {
            dbOptions.ConnectionConfigs.ForEach(dbConfig =>
            {
                var dbProvider = db.GetConnectionScope(dbConfig.ConfigId);
                SetDbAop(dbProvider, dbOptions.EnableConsoleSql);
                SetDbDiffLog(dbProvider, dbConfig);
            });
        });
        ITenant = sqlSugar;

        services.AddSingleton<ISqlSugarClient>(sqlSugar); // 单例注册
        services.AddScoped(typeof(SqlSugarRepository<>)); // 仓储注册
        services.AddUnitOfWork<SqlSugarUnitOfWork>(); // 事务与工作单元注册

        // 初始化数据库表结构及种子数据
        dbOptions.ConnectionConfigs.ForEach(config => { InitDatabase(sqlSugar, config); });
    }

    /// <summary>
    /// 配置连接属性
    /// </summary>
    /// <param name="config"></param>
    public static void SetDbConfig(DbConnectionConfig config)
    {
        // 注册 MongoDb
        if (config.DbType == SqlSugar.DbType.MongoDb)
            InstanceFactory.CustomAssemblies = [typeof(SqlSugar.MongoDb.MongoDbProvider).Assembly];

        // 解密数据库连接串
        if (config.DbSettings.EnableConnEncrypt)
            config.ConnectionString = CryptogramHelper.SM2Decrypt(config.ConnectionString);

        // SqlFunc 扩展函数
        var sqlFuncServices = new List<SqlFuncExternal>
        {
            // 密码解密
            new()
            {
                UniqueMethodName = "SqlFuncDecrypt",
                MethodValue = (expInfo, dbType, expContext) =>
                {
                    return CryptogramHelper.Decrypt(expInfo.Args[0].MemberName.ToString());
                }
            }
        };

        var configureExternalServices = new ConfigureExternalServices
        {
            EntityNameService = (type, entity) => // 处理表
            {
                entity.IsDisabledDelete = true; // 禁止删除非 sqlsugar 创建的列
                // 只处理贴了特性[SugarTable]表
                if (!type.GetCustomAttributes<SugarTable>().Any())
                    return;
                if (config.DbSettings.EnableUnderLine && !entity.DbTableName.Contains('_'))
                    entity.DbTableName = UtilMethods.ToUnderLine(entity.DbTableName); // 驼峰转下划线
            },
            EntityService = (type, column) => // 处理列
            {
                // 只处理贴了特性[SugarColumn]列
                if (!type.GetCustomAttributes<SugarColumn>().Any())
                    return;
                if (new NullabilityInfoContext().Create(type).WriteState is NullabilityState.Nullable)
                    column.IsNullable = true;
                if (config.DbSettings.EnableUnderLine && !column.IsIgnore && !column.DbColumnName.Contains('_'))
                    column.DbColumnName = UtilMethods.ToUnderLine(column.DbColumnName); // 驼峰转下划线
            },
            DataInfoCacheService = new SqlSugarCache(),
            SqlFuncServices = sqlFuncServices
        };
        config.ConfigureExternalServices = configureExternalServices;
        config.InitKeyType = InitKeyType.Attribute;
        config.IsAutoCloseConnection = true;
        config.MoreSettings = new ConnMoreSettings
        {
            IsAutoRemoveDataCache = true, // 启用自动删除缓存，所有增删改会自动调用.RemoveDataCache()
            IsAutoDeleteQueryFilter = true, // 启用删除查询过滤器
            IsAutoUpdateQueryFilter = true, // 启用更新查询过滤器
        };

        // 当库类型是人大金仓
        if (config.DbType == SqlSugar.DbType.Kdbndp)
            config.MoreSettings.DatabaseModel = SqlSugar.DbType.PostgreSQL; // 配置PG模式主要是兼容系统表差异

        // 当库类型是Oracle
        if (config.DbType == SqlSugar.DbType.Oracle)
            config.MoreSettings.MaxParameterNameLength = 30; // 默认主键名字和参数名字最大长度

        // 当库类型是SqlServer
        if (config.DbType == SqlSugar.DbType.SqlServer)
            config.MoreSettings.SqlServerCodeFirstNvarchar = true; // SqlServer默认使用Nvarchar
    }

    /// <summary>
    /// SqlFunc 扩展函数定义（密码解密）
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="password"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static string SqlFuncDecrypt<T>(T password)
    {
        throw new NotSupportedException("Can only be used in expressions");
    }

    /// <summary>
    /// 配置Aop
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="enableConsoleSql"></param>
    public static void SetDbAop(SqlSugarScopeProvider dbProvider, bool enableConsoleSql)
    {
        // 设置超时时间
        dbProvider.Ado.CommandTimeOut = 30;

        // 打印SQL语句
        dbProvider.Aop.OnError = ex =>
        {
            if (ex.Parametres == null) return;
            var log = $"【{DateTime.Now}——错误SQL】\r\n{UtilMethods.GetNativeSql(ex.Sql, (SugarParameter[])ex.Parametres)}\r\n";
            Log.Error(log, ex);
        };
        if (enableConsoleSql)
        {
            dbProvider.Aop.OnLogExecuting = (sql, pars) =>
            {
                //// 若参数值超过100个字符则进行截取
                //foreach (var par in pars)
                //{
                //    if (par.DbType != System.Data.DbType.String || par.Value == null) continue;
                //    if (par.Value.ToString().Length > 100)
                //        par.Value = string.Concat(par.Value.ToString()[..100], "......");
                //}

                var log = $"【{DateTime.Now}——执行SQL】\r\n{UtilMethods.GetNativeSql(sql, pars)}\r\n";
                var originColor = Console.ForegroundColor;
                if (sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    Console.ForegroundColor = ConsoleColor.Green;
                if (sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                    Console.ForegroundColor = ConsoleColor.Yellow;
                if (sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
                    Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(log);
                Console.ForegroundColor = originColor;
            };
        }
        dbProvider.Aop.OnLogExecuted = (sql, pars) =>
        {
            //// 若参数值超过100个字符则进行截取
            //foreach (var par in pars)
            //{
            //    if (par.DbType != System.Data.DbType.String || par.Value == null) continue;
            //    if (par.Value.ToString().Length > 100)
            //        par.Value = string.Concat(par.Value.ToString()[..100], "......");
            //}

            // 执行时间超过5秒时
            if (dbProvider.Ado.SqlExecutionTime.TotalSeconds <= 5) return;

            var fileName = dbProvider.Ado.SqlStackTrace.FirstFileName; // 文件名
            var fileLine = dbProvider.Ado.SqlStackTrace.FirstLine; // 行号
            var firstMethodName = dbProvider.Ado.SqlStackTrace.FirstMethodName; // 方法名
            var log = $"【{DateTime.Now}——超时SQL】\r\n【所在文件名】：{fileName}\r\n【代码行数】：{fileLine}\r\n【方法名】：{firstMethodName}\r\n" + $"【SQL语句】：{UtilMethods.GetNativeSql(sql, pars)}";
            Log.Warning(log);
        };

        // 数据审计
        dbProvider.Aop.DataExecuting = (oldValue, entityInfo) =>
        {
            // 新增/插入 操作
            if (entityInfo.OperationType == DataFilterType.InsertByObject)
            {
                // 若是主键且非自增类型
                if (entityInfo.EntityColumnInfo.IsPrimarykey && !entityInfo.EntityColumnInfo.IsIdentity)
                {
                    // 雪花Id类型（长整型且空则赋值）
                    if (entityInfo.EntityColumnInfo.PropertyInfo.PropertyType == typeof(long))
                    {
                        var id = entityInfo.EntityColumnInfo.PropertyInfo.GetValue(entityInfo.EntityValue);
                        if (id == null || (long)id == 0)
                            entityInfo.SetValue(YitIdHelper.NextId());
                    }
                    // Guid类型（空则赋值）
                    else if (entityInfo.EntityColumnInfo.PropertyInfo.PropertyType == typeof(Guid))
                    {
                        var id = entityInfo.EntityColumnInfo.PropertyInfo.GetValue(entityInfo.EntityValue);
                        if (id == null)
                            entityInfo.SetValue(Guid.NewGuid());
                    }
                }
                // 若创建时间为空则赋值当前时间
                else if (entityInfo.PropertyName == nameof(EntityBase.CreateTime))
                {
                    var createTime = entityInfo.EntityColumnInfo.PropertyInfo.GetValue(entityInfo.EntityValue)!;
                    if (createTime == null || createTime.Equals(DateTime.MinValue))
                        entityInfo.SetValue(DateTime.Now);
                }
                // 若当前用户非空（web线程时）
                if (App.User == null) return;

                dynamic entityValue = entityInfo.EntityValue;
                if (entityInfo.PropertyName == nameof(EntityTenantId.TenantId))
                {
                    if (entityValue.TenantId == null || entityValue.TenantId == 0)
                        entityInfo.SetValue(App.User.FindFirst(ClaimConst.TenantId)?.Value ?? SqlSugarConst.DefaultTenantId.ToString());
                }
                else if (entityInfo.PropertyName == nameof(EntityBase.CreateUserId))
                {
                    if (entityValue.CreateUserId == null || entityValue.CreateUserId == 0)
                        entityInfo.SetValue(App.User.FindFirst(ClaimConst.UserId)?.Value);
                }
                else if (entityInfo.PropertyName == nameof(EntityBase.CreateUserName))
                {
                    if (string.IsNullOrWhiteSpace(entityValue.CreateUserName))
                        entityInfo.SetValue(App.User.FindFirst(ClaimConst.RealName)?.Value);
                }
                else if (entityInfo.PropertyName == nameof(EntityBaseData.CreateOrgId))
                {
                    if (entityValue.CreateOrgId == null || entityValue.CreateOrgId == 0)
                        entityInfo.SetValue(App.User.FindFirst(ClaimConst.OrgId)?.Value);
                }
                else if (entityInfo.PropertyName == nameof(EntityBaseData.CreateOrgName))
                {
                    if (string.IsNullOrWhiteSpace(entityValue.CreateOrgName))
                        entityInfo.SetValue(App.User.FindFirst(ClaimConst.OrgName)?.Value);
                }
            }
            // 编辑/更新 操作
            else if (entityInfo.OperationType == DataFilterType.UpdateByObject)
            {
                if (entityInfo.PropertyName == nameof(EntityBase.UpdateTime))
                    entityInfo.SetValue(DateTime.Now);
                else if (entityInfo.PropertyName == nameof(EntityBase.UpdateUserId))
                    entityInfo.SetValue(App.User?.FindFirst(ClaimConst.UserId)?.Value);
                else if (entityInfo.PropertyName == nameof(EntityBase.UpdateUserName))
                    entityInfo.SetValue(App.User?.FindFirst(ClaimConst.RealName)?.Value);
            }

            // 设置绑定流水号字段数据
            entityInfo.SetSerialProperty();

            //// 自定义数据审计
            //SqlSugarDataExecuting.Execute(oldValue, entityInfo);
        };

        // 超管不受任何过滤器限制
        if (App.User?.FindFirst(ClaimConst.AccountType)?.Value == ((int)AccountTypeEnum.SuperAdmin).ToString())
            return;

        // 配置假删除过滤器
        dbProvider.QueryFilter.AddTableFilter<IDeletedFilter>(u => u.IsDelete == false);

        // 配置租户过滤器
        var tenantId = App.User?.FindFirst(ClaimConst.TenantId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
            dbProvider.QueryFilter.AddTableFilter<ITenantIdFilter>(u => u.TenantId == long.Parse(tenantId));

        // 配置用户机构（数据范围）过滤器
        SqlSugarFilter.SetOrgEntityFilter(dbProvider);

        // 配置自定义过滤器
        SqlSugarFilter.SetCustomEntityFilter(dbProvider);

        // 配置自定义Aop操作
        var aopList = _cache.GetOrAdd("SqlSugar_Aop", _ => App.EffectiveTypes
            .Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(i => i == typeof(ISqlSugarAop)))
            .Select(u => Activator.CreateInstance(u) as ISqlSugarAop).ToList()) ?? [];
        aopList.ForEach(u => u.HandleDatabaseAop(dbProvider));
    }

    /// <summary>
    /// 开启库表差异化日志
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="config"></param>
    private static void SetDbDiffLog(SqlSugarScopeProvider dbProvider, DbConnectionConfig config)
    {
        if (!config.DbSettings.EnableDiffLog) return;

        dbProvider.Aop.OnDiffLogEvent = async u =>
        {
            if (u.AfterData.FirstOrDefault()?.TableName == nameof(SysLogDiff)) return;

            var logDiff = new SysLogDiff
            {
                // 操作后记录（字段描述、列名、值、表名、表描述）
                AfterData = JSON.Serialize(u.AfterData),
                // 操作前记录（字段描述、列名、值、表名、表描述）
                BeforeData = JSON.Serialize(u.BeforeData),
                // 传进来的对象（如果对象为空，则使用首个数据的表名作为业务对象）
                BusinessData = u.BusinessData == null ? u.AfterData.FirstOrDefault()?.TableName : JSON.Serialize(u.BusinessData),
                // 枚举（insert、update、delete）
                DiffType = u.DiffType.ToString().ToUpper(),
                Sql = u.Sql,
                Parameters = JSON.Serialize(u.Parameters.Select(e => new { e.ParameterName, e.Value, TypeName = e.DbType.ToString() })),
                Elapsed = u.Time == null ? 0 : (long)u.Time.Value.TotalMilliseconds
            };
            var logDb = ITenant.IsAnyConnection(SqlSugarConst.LogConfigId) ? ITenant.GetConnectionScope(SqlSugarConst.LogConfigId) : ITenant.GetConnectionScope(SqlSugarConst.MainConfigId);
            await logDb.CopyNew().Insertable(logDiff).ExecuteCommandAsync();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(DateTime.Now + $"\r\n*****开始差异日志*****\r\n{Environment.NewLine}{JSON.Serialize(logDiff)}{Environment.NewLine}*****结束差异日志*****\r\n");
        };
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    /// <param name="db"></param>
    /// <param name="config"></param>
    private static void InitDatabase(SqlSugarScope db, DbConnectionConfig config)
    {
        var dbProvider = db.GetConnectionScope(config.ConfigId);

        // 初始化/创建数据库
        if (config.DbSettings.EnableInitDb)
        {
            Log.Information(string.Format("初始化数据库 {0} - {1} - {2}", config.DbType, config.DbType, config.ConnectionString));
            if (config.DbType != SqlSugar.DbType.Oracle) dbProvider.DbMaintenance.CreateDatabase();
        }

        // 初始化表结构
        if (config.TableSettings.EnableInitTable) InitTable(dbProvider, config.TableSettings.EnableIncreTable);

        // 初始化视图
        if (config.DbSettings.EnableInitView) InitView(dbProvider);

        // 初始化种子数据
        if (config.SeedSettings.EnableInitSeed) InitSeedData(dbProvider, config.SeedSettings.EnableIncreSeed);

        // 由于修改定时任务后数据库无法更新,暂时先清空内置的定时任务数据
        if (App.GetConfig<bool>("JobSchedule:Enabled", true) && config.ConfigId.ToString() == SqlSugarConst.MainConfigId)
        {
            var jobIds = dbProvider.Queryable<SysJobDetail>().Where(u => u.CreateType == JobCreateTypeEnum.BuiltIn).Select(u => u.JobId).ToList();
            dbProvider.Deleteable<SysJobDetail>().Where(u => jobIds.Contains(u.JobId)).ExecuteCommand();
            dbProvider.Deleteable<SysJobTrigger>().Where(u => jobIds.Contains(u.JobId)).ExecuteCommand();
        }
    }

    /// <summary>
    /// 初始化表结构
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="enableIncreTable"></param>
    /// <param name="entityNames"></param>
    public static void InitTable(SqlSugarScopeProvider dbProvider, bool enableIncreTable, List<string> entityNames = null)
    {
        var config = dbProvider.CurrentConnectionConfig;

        var totalWatch = Stopwatch.StartNew(); // 开始总计时
        Log.Information(string.Format("初始化表结构 {0} - {1}", config.DbType, config.ConfigId));
        var entityTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.IsDefined(typeof(SugarTable), false))
            .Where(u => !u.GetCustomAttributes<IgnoreTableAttribute>().Any())
            .WhereIF(enableIncreTable, u => u.IsDefined(typeof(IncreTableAttribute), false)).ToList();

        if (config.ConfigId.ToString() == SqlSugarConst.MainConfigId) // 默认库（有系统表特性、没有日志表和租户表特性）
            entityTypes = entityTypes.Where(u => u.GetCustomAttributes<SysTableAttribute>().Any() || (!u.GetCustomAttributes<LogTableAttribute>().Any() && !u.GetCustomAttributes<TenantAttribute>().Any())).ToList();
        else if (config.ConfigId.ToString() == SqlSugarConst.LogConfigId) // 日志库
            entityTypes = entityTypes.Where(u => u.GetCustomAttributes<LogTableAttribute>().Any()).ToList();
        else
            entityTypes = entityTypes.Where(u => u.GetCustomAttribute<TenantAttribute>()?.configId.ToString() == config.ConfigId.ToString()).ToList(); // 自定义的库

        // 过滤指定实体
        if (entityNames is { Count: > 0 })
            entityTypes = entityTypes.Where(u => entityNames.Contains(u.Name)).ToList();

        // 删除视图再初始化表结构，防止因为视图导致无法同步表结构
        var viewTypeList = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarView)))).ToList();
        foreach (var viewType in viewTypeList)
        {
            var entityInfo = dbProvider.EntityMaintenance.GetEntityInfo(viewType) ?? throw new Exception("获取视图实体配置有误");
            if (dbProvider.DbMaintenance.GetViewInfoList(false).Any(it => it.Name.EqualIgnoreCase(entityInfo.DbTableName)))
                dbProvider.DbMaintenance.DropView(entityInfo.DbTableName);
        }

        int taskIndex = 0, size = entityTypes.Count;
        var taskList = entityTypes.Select(entityType => Task.Run(() =>
        {
            var stopWatch = Stopwatch.StartNew(); // 开始计时
            dbProvider.InitTable(entityType); // 初始化表结构
            stopWatch.Stop(); // 停止计时

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("初始化表 {0,-64} ({1} - {2:D003}/{3:D003}) 耗时：{4} ms", entityType, config.ConfigId, Interlocked.Increment(ref taskIndex), size, stopWatch.ElapsedMilliseconds));
        }));
        Task.WaitAll(taskList.ToArray());

        totalWatch.Stop(); // 停止总计时
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(string.Format("初始化表结构 {0} - {1} 总耗时：{2} ms", config.DbType, config.ConfigId, totalWatch.ElapsedMilliseconds));
    }

    /// <summary>
    /// 初始化种子数据
    /// </summary>
    /// <param name="dbProvider"></param>
    /// <param name="enableIncreSeed"></param>
    /// <param name="seedTypes"></param>
    public static void InitSeedData(SqlSugarScopeProvider dbProvider, bool enableIncreSeed, List<SeedType> seedTypes = null)
    {
        var config = dbProvider.CurrentConnectionConfig;

        var totalWatch = Stopwatch.StartNew(); // 开始总计时
        Log.Information(string.Format("初始化种子数据 {0} - {1}", config.DbType, config.ConfigId));
        var seedDataTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarEntitySeedData<>))))
            .Where(u => !u.IsDefined(typeof(TenantSeedAttribute), false))
            .WhereIF(enableIncreSeed, u => u.IsDefined(typeof(IncreSeedAttribute), false))
            .OrderBy(u => u.GetCustomAttributes(typeof(SeedDataAttribute), false).Length > 0 ? ((SeedDataAttribute)u.GetCustomAttributes(typeof(SeedDataAttribute), false)[0]).Order : 0).ToList();

        // 过滤指定程序集种子
        if (seedTypes != null && seedTypes.Count > 0)
        {
            seedTypes = seedTypes.OrderBy(u => u.Order).ToList();
            var tmpSeedTypes = new List<Type>();
            foreach (var seedType in seedTypes)
            {
                var tmpSeedType = seedDataTypes.FirstOrDefault(u => u.Name == seedType.Name && u.Assembly.ManifestModule.Name == seedType.AssemblyName);
                if (tmpSeedType != null) tmpSeedTypes.Add(tmpSeedType);
            }
            if (tmpSeedTypes.Count > 0)
                seedDataTypes = tmpSeedTypes;
        }

        // 由于种子数据在应用层存在重写，必须保证应用层种子最后执行（多线程顺序会乱）
        int taskIndex = 0, size = seedDataTypes.Count;
        foreach (var seedType in seedDataTypes)
        {
            var stopWatch = Stopwatch.StartNew(); // 开始计时

            // 初始化种子数据
            var tuple = dbProvider.InitTableSeedData(seedType);
            if (tuple == null) continue;

            stopWatch.Stop(); // 停止计时

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("初始化种子数据 {0,-58} ({1} - {2:D003}/{3:D003}，数据量：{4:D003}，新增 {5:D003} 条记录，更新 {6:D003} 条记录，耗时：{7:N0} ms)", seedType.FullName, config.ConfigId, Interlocked.Increment(ref taskIndex), size, tuple.Value.Item1, tuple.Value.Item2, tuple.Value.Item3, stopWatch.ElapsedMilliseconds));
        }

        totalWatch.Stop(); // 停止总计时
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(string.Format("初始化种子数据 {0} - {1} 总耗时：{2:N0} ms", config.DbType, config.ConfigId, totalWatch.ElapsedMilliseconds));
    }

    /// <summary>
    /// 初始化视图
    /// </summary>
    /// <param name="dbProvider"></param>
    public static void InitView(SqlSugarScopeProvider dbProvider)
    {
        var config = dbProvider.CurrentConnectionConfig;

        var totalWatch = Stopwatch.StartNew(); // 开始总计时
        Log.Information(string.Format("初始化视图 {0} - {1}", config.DbType, config.ConfigId));
        var viewTypeList = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarView)))).ToList();

        int taskIndex = 0, size = viewTypeList.Count;
        var taskList = viewTypeList.Select(viewType => Task.Run(() =>
        {
            // 开始计时
            var stopWatch = Stopwatch.StartNew();

            // 获取视图实体和配置信息
            var entityInfo = dbProvider.EntityMaintenance.GetEntityInfo(viewType) ?? throw new Exception("获取视图实体配置有误");

            // 如果视图存在，则删除视图
            if (dbProvider.DbMaintenance.GetViewInfoList(false).Any(it => it.Name.EqualIgnoreCase(entityInfo.DbTableName)))
                dbProvider.DbMaintenance.DropView(entityInfo.DbTableName);

            // 获取初始化视图查询SQL
            var sql = viewType.GetMethod(nameof(ISqlSugarView.GetQueryableSqlString))?.Invoke(Activator.CreateInstance(viewType), [dbProvider]) as string;
            if (string.IsNullOrWhiteSpace(sql)) throw new Exception("视图初始化Sql语句不能为空");

            try
            {
                // 创建视图
                dbProvider.Ado.ExecuteCommand($"CREATE VIEW {entityInfo.DbTableName} AS " + Environment.NewLine + " " + sql);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format("初始化视图 {0,-58} ({1} - 失败，{2})", viewType.FullName, config.ConfigId, ex.Message));
            }

            // 停止计时
            stopWatch.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("初始化视图 {0,-58} ({1} - {2:D003}/{3:D003}，耗时：{4:N0} ms)", viewType.FullName, config.ConfigId, Interlocked.Increment(ref taskIndex), size, stopWatch.ElapsedMilliseconds));
        }));
        Task.WaitAll(taskList.ToArray());

        totalWatch.Stop(); // 停止总计时
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(string.Format("初始化视图 {0} - {1} 总耗时：{2:N0} ms", config.DbType, config.ConfigId, totalWatch.ElapsedMilliseconds));
    }

    /// <summary>
    /// 初始化租户业务数据库
    /// </summary>
    /// <param name="config"></param>
    public static void InitTenantDatabase(DbConnectionConfig config)
    {
        SetDbConfig(config);

        var iTenant = App.GetRequiredService<ISqlSugarClient>().AsTenant();
        if (!iTenant.IsAnyConnection(config.ConfigId.ToString()))
            iTenant.AddConnection(config);
        var db = iTenant.GetConnectionScope(config.ConfigId.ToString());
        db.DbMaintenance.CreateDatabase();

        // 初始化租户库表结构-获取所有业务应用表（排除系统表、日志表、特定库表）
        var entityTypes = App.EffectiveTypes
            .Where(u => !u.GetCustomAttributes<IgnoreTableAttribute>().Any())
            .Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.IsDefined(typeof(SugarTable), false) &&
                !u.IsDefined(typeof(SysTableAttribute), false) && !u.IsDefined(typeof(LogTableAttribute), false) && !u.IsDefined(typeof(TenantAttribute), false)).ToList();
        if (entityTypes.Count == 0) return;

        foreach (var entityType in entityTypes)
        {
            var splitTable = entityType.GetCustomAttribute<SplitTableAttribute>();
            if (splitTable == null)
                db.CodeFirst.InitTables(entityType);
            else
                db.CodeFirst.SplitTables().InitTables(entityType);
        }

        // 初始化租户业务数据
        InitTenantData(iTenant, config.ConfigId.ToLong(), config.ConfigId.ToLong());
    }

    /// <summary>
    /// 初始化租户业务数据
    /// </summary>
    /// <param name="iTenant"></param>
    /// <param name="dbConfigId">库标识</param>
    /// <param name="tenantId">租户Id</param>
    public static void InitTenantData(ITenant iTenant, long dbConfigId, long tenantId)
    {
        var seedDataTypes = App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass && u.GetInterfaces().Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarEntitySeedData<>))))
            .Where(u => u.IsDefined(typeof(TenantSeedAttribute), false))
            .OrderBy(u => u.GetCustomAttributes(typeof(SeedDataAttribute), false).Length > 0 ? ((SeedDataAttribute)u.GetCustomAttributes(typeof(SeedDataAttribute), false)[0]).Order : 0).ToList();
        if (seedDataTypes.Count < 1) return;

        var db = iTenant.GetConnectionScope(dbConfigId);
        foreach (var seedType in seedDataTypes)
        {
            var instance = Activator.CreateInstance(seedType);
            var hasDataMethod = seedType.GetMethod("HasData");
            var seedData = ((IEnumerable)hasDataMethod?.Invoke(instance, null))?.Cast<object>().ToList() ?? [];
            if (seedData.Count == 0) continue;

            var entityType = seedType.GetInterfaces().First().GetGenericArguments().First();
            var entityInfo = db.EntityMaintenance.GetEntityInfo(entityType);
            // var dbConfigId = config.ConfigId.ToLong();
            // 若实体包含租户Id字段，则设置为当前租户Id
            if (entityInfo.Columns.Any(u => u.PropertyName == nameof(EntityTenantId.TenantId)))
            {
                foreach (var sd in seedData)
                {
                    sd.GetType().GetProperty(nameof(EntityTenantId.TenantId))!.SetValue(sd, tenantId);
                }
            }
            // 若实体包含Pid字段，则设置为当前租户Id
            if (entityInfo.Columns.Any(u => u.PropertyName == nameof(SysOrg.Pid)))
            {
                foreach (var sd in seedData)
                {
                    sd.GetType().GetProperty(nameof(SysOrg.Pid))!.SetValue(sd, tenantId);
                }
            }
            // 若实体包含Id字段，则设置为当前租户Id递增1
            if (entityInfo.Columns.Any(u => u.PropertyName == nameof(EntityBaseId.Id)))
            {
                var seedId = tenantId;
                foreach (var sd in seedData)
                {
                    var id = sd.GetType().GetProperty(nameof(EntityBaseId.Id))!.GetValue(sd, null);
                    if (id != null && (id.ToString() == "0" || string.IsNullOrWhiteSpace(id.ToString())))
                        sd.GetType().GetProperty(nameof(EntityBaseId.Id))!.SetValue(sd, ++seedId);
                }
            }

            // 若实体是系统内置，则切换至默认库
            if (entityType.GetCustomAttribute<SysTableAttribute>() != null)
                db = iTenant.GetConnectionScope(SqlSugarConst.MainConfigId);

            // 按主键进行批量增加和更新
            if (entityInfo.Columns.Any(u => u.IsPrimarykey))
            {
                var storage = db.StorageableByObject(seedData).ToStorage();
                if (seedType.GetCustomAttribute<IgnoreUpdateSeedAttribute>() == null) // 有忽略更新种子特性时则不更新
                    storage.AsUpdateable.IgnoreColumns(entityInfo.Columns.Where(c => c.PropertyInfo.GetCustomAttribute<IgnoreUpdateSeedColumnAttribute>() != null)
                        .Select(c => c.PropertyName).ToArray()).ExecuteCommand();
                storage.AsInsertable.ExecuteCommand();
            }
            // 无主键则只进行插入
            else
            {
                if (!db.Queryable(entityInfo.DbTableName, entityInfo.DbTableName).Any())
                    db.InsertableByObject(seedData).ExecuteCommand();
            }
        }
    }
}