// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 种子数据帮助类
/// </summary>
public static class SeedDataHelper
{
    /// <summary>
    /// 获取初始化种子数据类型
    /// </summary>
    /// <param name="enableIncreSeed">增量种子</param>
    /// <returns></returns>
    public static List<Type> GetInitSeedDataTypeList(bool enableIncreSeed = false)
    {
        return GetAllSeedDataTypeList()
            .WhereIF(enableIncreSeed, u => u.IsDefined(typeof(IncreSeedAttribute), false))
            .Where(u => !u.IsDefined(typeof(TenantSeedAttribute), false))
            .SeedOrder()
            .ToList();
    }

    /// <summary>
    /// 根据表实体获取种子数据类型
    /// </summary>
    /// <returns></returns>
    public static List<Type> GetSeedDataTypeList<T>()
    {
        return GetAllSeedDataTypeList().Where(u => u.GetInterfaces().FirstOrDefault()
            ?.GetGenericArguments().FirstOrDefault() == typeof(T)).SeedOrder().ToList();
    }

    /// <summary>
    /// 根据表实体获取种子数据类型
    /// </summary>
    /// <param name="genericType">表实体类型</param>
    /// <returns></returns>
    public static List<Type> GetSeedDataTypeList(Type genericType)
    {
        return GetAllSeedDataTypeList().Where(u => u.GetInterfaces().FirstOrDefault()
            ?.GetGenericArguments().FirstOrDefault() == genericType).SeedOrder().ToList();
    }

    /// <summary>
    /// 根据委托方法获取种子数据类型
    /// </summary>
    /// <returns></returns>
    public static List<Type> GetSeedDataTypeList(Func<Type, Type, bool> func = null)
    {
        return GetAllSeedDataTypeList().WhereIF(func != null, u => func!(u, u.GetInterfaces().FirstOrDefault()
            ?.GetGenericArguments().FirstOrDefault())).SeedOrder().ToList();
    }

    /// <summary>
    /// 获取租户种子数据类型
    /// </summary>
    /// <returns></returns>
    public static List<Type> GetTenantSeedDataTypeList()
    {
        return GetAllSeedDataTypeList().Where(u => u.IsDefined(typeof(TenantSeedAttribute), false)).SeedOrder().ToList();
    }

    /// <summary>
    /// 获取所有继承了ISqlSugarEntitySeedData接口的类型
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<Type> GetAllSeedDataTypeList()
    {
        return App.EffectiveTypes.Where(u => !u.IsInterface && !u.IsAbstract && u.IsClass)
            .Where(u => u.GetInterfaces().Any(i => i.HasImplementedRawGeneric(typeof(ISqlSugarEntitySeedData<>))));
    }

    /// <summary>
    /// 种子类型排序
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<Type> SeedOrder(this IEnumerable<Type> types)
    {
        return types.OrderBy(u => u.GetCustomAttribute<SeedDataAttribute>(false)?.Order ?? 0);
    }
}