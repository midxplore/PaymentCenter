// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// SqlSugar 实体仓储
/// </summary>
/// <typeparam name="T"></typeparam>
public class SqlSugarRepository<T> : SimpleClient<T>, ISqlSugarRepository<T> where T : class, new()
{
    public SqlSugarRepository()
    {
        var iTenant = SqlSugarSetup.ITenant;
        base.Context = iTenant.GetConnectionScope(SqlSugarConst.MainConfigId);

        // 若实体贴有多库特性，则返回指定库连接
        if (typeof(T).IsDefined(typeof(TenantAttribute), false))
        {
            base.Context = iTenant.GetConnectionScopeWithAttr<T>();
            return;
        }

        // 若实体贴有日志表特性，则返回日志库连接
        if (typeof(T).IsDefined(typeof(LogTableAttribute), false))
        {
            if (iTenant.IsAnyConnection(SqlSugarConst.LogConfigId))
                base.Context = iTenant.GetConnectionScope(SqlSugarConst.LogConfigId);
            return;
        }

        // 若实体贴有系统表特性，则返回默认库连接
        if (typeof(T).IsDefined(typeof(SysTableAttribute), false))
            return;

        // 若未贴任何表特性或当前未登录或是默认租户Id，则返回默认库连接
        var tenantId = App.User?.FindFirst(ClaimConst.TenantId)?.Value;
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId == SqlSugarConst.MainConfigId) return;

        // 根据租户Id切换库连接, 为空则返回默认库连接
        var sqlSugarScopeProviderTenant = App.GetRequiredService<SysTenantService>().GetTenantDbConnectionScope(long.Parse(tenantId));
        if (sqlSugarScopeProviderTenant == null) return;
        base.Context = sqlSugarScopeProviderTenant;
    }

    #region 分表操作

    public async Task<bool> SplitTableInsertAsync(T input)
    {
        return await base.AsInsertable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableInsertAsync(List<T> input)
    {
        return await base.AsInsertable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableUpdateAsync(T input)
    {
        return await base.AsUpdateable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableUpdateAsync(List<T> input)
    {
        return await base.AsUpdateable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableDeleteableAsync(T input)
    {
        return await base.Context.Deleteable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public async Task<bool> SplitTableDeleteableAsync(List<T> input)
    {
        return await base.Context.Deleteable(input).SplitTable().ExecuteCommandAsync() > 0;
    }

    public Task<T> SplitTableGetFirstAsync(Expression<Func<T, bool>> whereExpression)
    {
        return base.AsQueryable().SplitTable().FirstAsync(whereExpression);
    }

    public Task<bool> SplitTableIsAnyAsync(Expression<Func<T, bool>> whereExpression)
    {
        return base.Context.Queryable<T>().Where(whereExpression).SplitTable().AnyAsync();
    }

    public Task<List<T>> SplitTableGetListAsync()
    {
        return Context.Queryable<T>().SplitTable().ToListAsync();
    }

    public Task<List<T>> SplitTableGetListAsync(Expression<Func<T, bool>> whereExpression)
    {
        return Context.Queryable<T>().Where(whereExpression).SplitTable().ToListAsync();
    }

    public Task<List<T>> SplitTableGetListAsync(Expression<Func<T, bool>> whereExpression, string[] tableNames)
    {
        return Context.Queryable<T>().Where(whereExpression).SplitTable(t => t.InTableNames(tableNames)).ToListAsync();
    }

    #endregion 分表操作

    #region 乐观锁更新

    private PropertyInfo GetVerProperty(string verFieldName)
    {
        var prop = typeof(T).GetProperty(verFieldName);
        if (prop == null) return null;

        var type = prop.PropertyType;
        if (type == typeof(int) || type == typeof(int?))
            return prop;

        return null;
    }

    /// <summary>
    /// 并发安全地更新实体：如果实体包含名为 Ver 的 int 或 int? 类型属性，
    /// 则自动执行“版本号自增 + 条件更新”（乐观锁）；
    /// 否则执行普通更新。
    /// </summary>
    /// <param name="input">要更新的实体对象，必须包含主键和当前 Ver 值（若存在 Ver 字段）</param>
    /// <param name="verFieldName">Ver 字段名称</param>
    /// <returns>返回 true 表示更新成功（匹配到记录），false 表示并发冲突或记录不存在</returns>
    /// <exception cref="ArgumentNullException">当 input 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当 Ver 字段存在但值为 null 时抛出（无法进行并发校验）</exception>
    public virtual async Task<bool> UpdateWithVerAsync(T input, string verFieldName = "Ver")
    {
        PropertyInfo VerProperty = GetVerProperty(verFieldName);
        ArgumentNullException.ThrowIfNull(input);

        if (VerProperty == null)
        {
            int result = await base.AsUpdateable(input).ExecuteCommandAsync();
            return result > 0;
        }

        object originalVerObject = VerProperty.GetValue(input);
        object originalVer;

        if (originalVerObject == null && VerProperty.PropertyType == typeof(int))
        {
            originalVer = 0;
        }
        else if (originalVerObject == null)
        {
            throw new ArgumentException(string.Format("执行并发安全更新时{0}不能为空。", verFieldName));
        }
        else
        {
            originalVer = originalVerObject;
        }

        ParameterExpression param = Expression.Parameter(typeof(T), "it");
        MemberExpression verMember = Expression.Property(param, VerProperty);

        // ========== 左表达式：it => (object)it.Ver ==========
        Expression leftBody = Expression.Convert(verMember, typeof(object));
        Expression<Func<T, object>> leftExpr = Expression.Lambda<Func<T, object>>(leftBody, param);

        // ========== 右表达式：it => (object)(it.Ver + 1) ==========
        Expression rightBody;
        if (VerProperty.PropertyType == typeof(int?))
        {
            // it.Ver.Value + 1 → int
            MemberExpression valueAccess = Expression.Property(verMember, "Value");
            BinaryExpression added = Expression.Add(valueAccess, Expression.Constant(1, typeof(int)));
            // 转为 int?
            Expression nullableResult = Expression.Convert(added, typeof(int?));
            // 装箱为 object
            rightBody = Expression.Convert(nullableResult, typeof(object));
        }
        else
        {
            // it.Ver + 1 → int → 装箱为 object
            BinaryExpression added = Expression.Add(verMember, Expression.Constant(1, typeof(int)));
            rightBody = Expression.Convert(added, typeof(object));
        }
        Expression<Func<T, object>> rightExpr = Expression.Lambda<Func<T, object>>(rightBody, param);

        // ========== Where 条件：it.Ver == originalVer ==========
        ConstantExpression constExpr = Expression.Constant(originalVer, verMember.Type);
        BinaryExpression whereBody = Expression.Equal(verMember, constExpr);
        Expression<Func<T, bool>> whereLambda = Expression.Lambda<Func<T, bool>>(whereBody, param);

        int updateResult = await base.AsUpdateable(input)
                                        .PublicSetColumns(leftExpr, rightExpr)
                                        .Where(whereLambda)
                                        .ExecuteCommandAsync();

        return updateResult > 0;
    }

    #endregion 乐观锁更新
}