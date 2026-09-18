// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Hardware.Info;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Xml.Linq;

namespace Admin.NET.Core.Service;

/// <summary>
/// 系统通用服务 🧩
/// </summary>
[ApiDescriptionSettings(Order = 101, Description = "通用接口")]
public class SysCommonService : IDynamicApiController, ITransient
{
    private readonly IApiDescriptionGroupCollectionProvider _apiProvider;
    private readonly SysCacheService _sysCacheService;
    private readonly IHttpRemoteService _httpRemoteService;

    public SysCommonService(IApiDescriptionGroupCollectionProvider apiProvider,
        SysCacheService sysCacheService,
        IHttpRemoteService httpRemoteService)
    {
        _apiProvider = apiProvider;
        _sysCacheService = sysCacheService;
        _httpRemoteService = httpRemoteService;
    }

    /// <summary>
    /// 获取国密公钥私钥对 🏆
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取国密公钥私钥对")]
    public SmKeyPairOutput GetSmKeyPair()
    {
        return CryptogramHelper.GetSmKeyPair();
    }

    /// <summary>
    /// 获取MD5加密字符串 🏆
    /// </summary>
    /// <param name="text"></param>
    /// <param name="uppercase"></param>
    /// <returns></returns>
    [DisplayName("获取MD5加密字符串")]
    public string GetMD5Encrypt(string text, bool uppercase = false)
    {
        return MD5Encryption.Encrypt(text, uppercase, is16: false);
    }

    /// <summary>
    /// 国密SM2加密字符串 🔖
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    [DisplayName("国密SM2加密字符串")]
    public string SM2Encrypt([Required] string plainText)
    {
        return CryptogramHelper.SM2Encrypt(plainText);
    }

    /// <summary>
    /// 国密SM2解密字符串 🔖
    /// </summary>
    /// <param name="cipherText"></param>
    /// <returns></returns>
    [DisplayName("国密SM2解密字符串")]
    public string SM2Decrypt([Required] string cipherText)
    {
        return CryptogramHelper.SM2Decrypt(cipherText);
    }

    /// <summary>
    /// 获取所有接口/动态API 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取所有接口/动态API")]
    public List<ApiOutput> GetAllSysApiList()
    {
        return _sysCacheService.GetOrAdd(CacheConst.KeyAllApi, _ =>
        {
            var apiList = new List<ApiOutput>();

            //// 路由前缀
            //var defaultRoutePrefix = App.GetOptions<DynamicApiControllerSettingsOptions>().DefaultRoutePrefix;

            // 获取所有接口分组
            var apiDescriptionGroups = _apiProvider.ApiDescriptionGroups.Items;
            foreach (ApiDescriptionGroup group in apiDescriptionGroups)
            {
                //if (!string.IsNullOrWhiteSpace(groupName) && group.GroupName != groupName)
                //    continue;
                /*
                 * 你在 SysCommonService.cs 里用的是 ApiExplorer 提供的分组信息：group.GroupName。这个值只会在使用内置的 [ApiExplorerSettings(GroupName = "...")] 标注时由 ASP.NET Core 的 ApiExplorer 填充；你现在用的是自定义的 [ApiDescriptionSettings(...)]（Furion 扩展），它并不会自动映射到 ApiExplorer 的 GroupName。因此，遍历到的 ApiDescriptionGroup.GroupName 就都是空的，即使你的控制器上配置了 ApiDescriptionSettings 的 GroupName。
                 * 所以目前框架会一直取不到值
                 * */
                var apiOuput = new ApiOutput
                {
                    Name = "",
                    Desc = string.IsNullOrWhiteSpace(group.GroupName) ? "系统接口" : group.GroupName,
                    Route = "",
                };
                // 获取分组的所有接口
                var actions = group.Items;
                foreach (ApiDescription action in actions)
                {
                    // 路由
                    var route = action.RelativePath.Contains('{') ? action.RelativePath[..(action.RelativePath.IndexOf('{') - 1)] : action.RelativePath; // 去掉路由参数
                    route = route[(route.IndexOf('/') + 1)..]; // 去掉路由前缀

                    // 接口分组/控制器信息
                    if (action.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor) continue;
                    var apiDescription = controllerActionDescriptor.ControllerTypeInfo.GetCustomAttribute<ApiDescriptionSettingsAttribute>(true);
                    var controllerName = controllerActionDescriptor.ControllerName;
                    var actionName = controllerActionDescriptor.ActionName;
                    var controllerText = apiDescription?.Description;
                    if (!apiOuput.Children.Exists(u => u.Name == controllerName))
                    {
                        apiOuput.Children.Add(new ApiOutput
                        {
                            Name = controllerName,
                            Desc = string.IsNullOrWhiteSpace(controllerText) ? controllerName : controllerText,
                            Route = "",
                            Order = apiDescription?.Order ?? 0,
                        });
                    }

                    // 接口信息
                    var apiController = apiOuput.Children.FirstOrDefault(u => u.Name.Equals(controllerName));
                    apiDescription = controllerActionDescriptor.MethodInfo.GetCustomAttribute<ApiDescriptionSettingsAttribute>(true);
                    var apiText = apiDescription?.Description;
                    if (string.IsNullOrWhiteSpace(apiText))
                        apiText = controllerActionDescriptor.MethodInfo.GetCustomAttribute<DisplayNameAttribute>(true)?.DisplayName;
                    // 如果反射取不到信息就到xml中试取一下
                    if (string.IsNullOrWhiteSpace(apiText))
                        apiText = TryGetXmlSummary(controllerActionDescriptor.MethodInfo) ?? apiText;
                    // 判断接口有没有 IFormFile 的参数，如果有就是上传接口
                    // 仅第一层检测：参数是否为 IFormFile/集合，或参数类型的顶层属性为 IFormFile/集合
                    var isUploadFileMethod = controllerActionDescriptor.MethodInfo.GetParameters().Any(p =>
                        IsIFormFileType(p.ParameterType) || HasIFormFileDirectMember(p.ParameterType)
                    );

                    apiController.Children.Add(new ApiOutput
                    {
                        Name = "",
                        Desc = apiText,
                        Route = route,
                        Action = actionName,
                        HttpMethod = action.HttpMethod,
                        Order = apiDescription?.Order ?? 0,
                        GroupName = group.GroupName,
                        IsAppApi = controllerActionDescriptor.ControllerTypeInfo.GetCustomAttribute<AppApiDescriptionAttribute>(true) != null,
                        IsUploadFileMethod = isUploadFileMethod,
                    });

                    // 接口分组/控制器排序
                    apiOuput.Children = [.. apiOuput.Children.OrderByDescending(u => u.Order)];
                }

                apiList.Add(apiOuput);
            }
            return apiList;
        });
    }

    /// <summary>
    /// 获取所有移动端接口 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取所有移动端接口")]
    public List<string> GetAppApiList()
    {
        return _sysCacheService.GetOrAdd(CacheConst.KeyAppApi, _ =>
        {
            List<string> appApiList = [];
            var allApiList = GetAllSysApiList();
            foreach (var apiOutput in allApiList)
            {
                foreach (var controller in apiOutput.Children)
                    appApiList.AddRange(controller.Children.Where(u => u.IsAppApi).Select(u => u.Route));
            }
            return appApiList;
        });
    }

    /// <summary>
    /// 生成所有移动端接口文件 🔖
    /// </summary>
    /// <param name="groupName"></param>
    /// <param name="isAppApi"></param>
    [HttpGet]
    [DisplayName("生成所有移动端接口文件")]
    public void GenerateAppApi([FromQuery] string groupName = "", [FromQuery] bool isAppApi = true)
    {
        var defaultRoutePrefix = App.GetOptions<DynamicApiControllerSettingsOptions>().DefaultRoutePrefix;
        var apiPath = Path.Combine(App.WebHostEnvironment.ContentRootPath, @"App\api");

        var allApiList = GetAllSysApiList();
        foreach (var apiOutput in allApiList)
        {
            foreach (var controller in apiOutput.Children)
            {
                // 以controller.Name为控制器名称，创建js文件.js
                var controllerName = controller.Name;
                var filePath = Path.Combine(apiPath, $"{controllerName}.js");
                StringBuilder stringBuilder = new();
                stringBuilder.Append(@"import { http } from 'uview-plus'");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine();
                foreach (var item in controller.Children)
                {
                    if (!item.IsAppApi) continue;

                    var value = item.HttpMethod.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? "params" : "data";
                    stringBuilder.Append($@"// {item.Desc}");
                    stringBuilder.AppendLine();
                    stringBuilder.Append($@"export const {item.Action}Api = ({value}) => http.{item.HttpMethod.ToLower()}('/{defaultRoutePrefix}/{item.Route}', {value})");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine();
                }
                // 如果或文件夹文件不存在则创建，存在则覆盖
                if (!Directory.Exists(apiPath))
                    Directory.CreateDirectory(apiPath);
                File.WriteAllText(filePath, stringBuilder.ToString());
            }
        }
    }

    /// <summary>
    /// 下载标记错误的临时 Excel（全局） 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("下载标记错误的临时 Excel")]
    public async Task<IActionResult> DownloadErrorExcelTemp([FromQuery] string fileName = null)
    {
        var userId = App.User?.FindFirst(ClaimConst.UserId)?.Value;
        var resultStream = _sysCacheService.Get<MemoryStream>(CacheConst.KeyExcelTemp + userId) ?? throw Oops.Oh("错误标记文件已过期。");

        return await Task.FromResult(new FileStreamResult(resultStream, "application/octet-stream")
        {
            FileDownloadName = $"{(string.IsNullOrEmpty(fileName) ? "错误标记＿" + DateTime.Now.ToString("yyyyMMddhhmmss") : fileName)}.xlsx"
        });
    }

    /// <summary>
    /// 获取机器序列号 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("获取机器序列号")]
    public string GetMachineSerialKey()
    {
        try
        {
            HardwareInfo hardwareInfo = new();
            hardwareInfo.RefreshBIOSList(); // 刷新 BIOS 信息
            hardwareInfo.RefreshMotherboardList(); // 刷新主板信息
            hardwareInfo.RefreshCPUList(false); // 刷新 CPU 信息

            var biosSerialNumber = hardwareInfo.BiosList.MinBy(u => u.SerialNumber)?.SerialNumber;
            var mbSerialNumber = hardwareInfo.MotherboardList.MinBy(u => u.SerialNumber)?.SerialNumber;
            var cpuProcessorId = hardwareInfo.CpuList.MinBy(u => u.ProcessorId)?.ProcessorId;
            // 根据 BIOS、主板和 CPU 信息生成 MD5 摘要
            var md5Data = MD5Encryption.Encrypt($"{biosSerialNumber}_{mbSerialNumber}_{cpuProcessorId}", true);
            var serialKey = $"{md5Data[..8]}-{md5Data[8..16]}-{md5Data[16..24]}-{md5Data[24..]}";
            return serialKey;
        }
        catch (Exception ex)
        {
            throw Oops.Oh(string.Format("获取机器码失败：{0}", ex.Message));
        }
    }

    /// <summary>
    /// 性能压力测试 🔖
    /// </summary>
    /// <returns></returns>
    [DisplayName("性能压力测试")]
    public async Task<StressTestHarnessResult> StressTest(StressTestInput input)
    {
        var stressTestHarnessResult = await _httpRemoteService.SendAsync(HttpRequestBuilder.StressTestHarness(input.RequestUri)
            .SetNumberOfRequests(input.NumberOfRequests) // 并发请求数量
            .SetNumberOfRounds(input.NumberOfRounds) // 压测轮次
            .SetMaxDegreeOfParallelism(input.MaxDegreeOfParallelism) // 最大并发度
            .WithRequest(builder => builder.WithHeaders(input.Headers)
                            .WithQueryParameters(input.QueryParameters)
                            .WithPathParameters(input.PathParameters)
                            .SetJsonContent(input.JsonContent)));
        return stressTestHarnessResult;
    }

    /// <summary>
    /// 生成所有终端接口文件 🔖
    /// </summary>
    /// <param name="groupName"></param>
    /// <param name="isAppApi"></param>
    /// <param name="generateToWeb">生成到Web目录</param>
    /// <param name="controllerName">指定只生成这个控制器</param>
    [HttpGet]
    [DisplayName("生成所有移动端接口文件")]
    public void GenerateAllApi([FromQuery] string groupName = "", [FromQuery] bool isAppApi = true, [FromQuery] bool generateToWeb = true, [FromQuery] string controllerName = "")
    {
        var defaultRoutePrefix = App.GetOptions<DynamicApiControllerSettingsOptions>().DefaultRoutePrefix;
        Dictionary<string, int> lowerControllerNameCreateTime = []; // 由于输出文件以类名保存，所以不同的命令空间可能有重复，这里就用来记数
        var apiPath = Path.Combine(App.WebHostEnvironment.ContentRootPath, @"../../Web/src/api_all");
        if (!generateToWeb)
            apiPath = Path.Combine(App.WebHostEnvironment.ContentRootPath, @"../../App/api_all");
        // baseApi 中已有的标准方法集合（大小写不敏感）
        var baseApiMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Page", "List", "Detail", "Add", "Update", "SetStatus", "Delete", "BatchDelete", "ExportData", "ImportData", "DownloadTemplate"
        };

        var allApiList = GetAllSysApiList(); // 此处暂时获取全部
        foreach (var apiOutput in allApiList)
        {
            foreach (var controller in apiOutput.Children)
            {
                // 控制器名称及文件命名
                var className = controller.Name;
                if (string.IsNullOrWhiteSpace(className)) continue;
                var lowerClassName = ToCamelCase(className);
                if (!string.IsNullOrEmpty(controllerName) && !controllerName.Equals(lowerClassName, StringComparison.CurrentCultureIgnoreCase))
                    continue;
                var filePath = Path.Combine(apiPath, $"{lowerClassName}.ts");
                if (lowerControllerNameCreateTime.ContainsKey(lowerClassName.ToLower()))
                {
                    filePath = Path.Combine(apiPath, $"{lowerClassName}_{lowerControllerNameCreateTime[lowerClassName.ToLower()]}.ts");
                    lowerControllerNameCreateTime[lowerClassName.ToLower()] += 1;
                }
                else
                {
                    lowerControllerNameCreateTime[lowerClassName.ToLower()] = 1;
                }

                var sb = new StringBuilder();
                if (generateToWeb)
                    sb.AppendLine("import {useBaseApi} from '/@/api/base';");
                else
                    sb.AppendLine("import {useBaseApi} from '@/api/base';");
                sb.AppendLine();
                sb.AppendLine($"// {controller.Desc}接口服务");
                sb.AppendLine($"export const use{className}Api = () => {{");
                sb.AppendLine($"    const baseApi = useBaseApi(\"{lowerClassName}\");");
                sb.AppendLine("    return {");

                // 遍历控制器下的每个接口，生成对应条目
                var orderMethods = controller.Children.OrderBy(u => u.Action).ToList();
                //var orderMethods = controller.Children;
                foreach (var item in orderMethods)
                {
                    var actionName = item.Action;
                    if (string.IsNullOrWhiteSpace(actionName)) continue;

                    var propName = ToCamelCase(actionName);
                    var httpMethod = string.IsNullOrWhiteSpace(item.HttpMethod) ? "GET" : item.HttpMethod.ToUpperInvariant();

                    // 注释：接口说明
                    sb.AppendLine($"        // {item.Desc}");

                    // 如果存在于 baseApi 标准方法，则直接使用 baseApi.<method>
                    if (baseApiMethods.Contains(actionName))
                    {
                        sb.AppendLine($"        {propName}: baseApi.{propName},");
                    }
                    else
                    {
                        // 否则使用 baseApi.custom("ActionName", "HTTP_METHOD")
                        sb.AppendLine($"        {propName.Replace("/", "_")}: baseApi.custom(\"{actionName}\", \"{httpMethod}\"),");
                    }
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                // 写入文件（不存在则创建目录）
                if (!Directory.Exists(apiPath))
                {
                    Directory.CreateDirectory(apiPath);
                }
                File.WriteAllText(filePath, sb.ToString());
            }
        }
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    // 判断类型是否为 IFormFile 或其数组/常见集合（IEnumerable/IList/ICollection）
    private static bool IsIFormFileType(Type type)
    {
        if (type == null) return false;

        if (typeof(IFormFile).IsAssignableFrom(type))
            return true;

        // 数组
        if (type.IsArray)
            return typeof(IFormFile).IsAssignableFrom(type.GetElementType());

        // 常见泛型集合
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            if (genDef == typeof(IEnumerable<>) ||
                genDef == typeof(IList<>) ||
                genDef == typeof(ICollection<>))
            {
                var elemType = type.GetGenericArguments()[0];
                return typeof(IFormFile).IsAssignableFrom(elemType);
            }
        }

        return false;
    }

    // 仅检测一层：类型的顶层公共实例属性是否为 IFormFile 或其集合
    private static bool HasIFormFileDirectMember(Type type)
    {
        if (type == null) return false;

        // 参数类型本身就是 IFormFile 或其集合
        if (IsIFormFileType(type)) return true;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var propType = prop.PropertyType;
            if (IsIFormFileType(propType)) return true;
        }

        return false;
    }

    private static string TryGetXmlSummary(MethodInfo methodInfo)
    {
        try
        {
            var assembly = methodInfo.DeclaringType?.Assembly;
            var asmLocation = assembly?.Location;
            if (string.IsNullOrWhiteSpace(asmLocation)) return null;

            var xmlPath = Path.ChangeExtension(asmLocation, ".xml");
            if (!File.Exists(xmlPath)) return null;

            var doc = XDocument.Load(xmlPath);
            var memberName = GetXmlMemberName(methodInfo);
            if (string.IsNullOrWhiteSpace(memberName)) return null;

            var member = doc.Descendants("member").FirstOrDefault(m => (string)m.Attribute("name") == memberName);
            var summaryRaw = member?.Element("summary")?.Value;
            var summary = string.IsNullOrWhiteSpace(summaryRaw) ? null : Regex.Replace(summaryRaw, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(summary) ? null : summary;
        }
        catch
        {
            return null;
        }
    }

    // 生成 XML 文档成员名，例如：M:Namespace.Type.Method(Type1,Type2)
    private static string GetXmlMemberName(MethodInfo methodInfo)
    {
        var typeFullName = methodInfo.DeclaringType?.FullName;
        if (string.IsNullOrWhiteSpace(typeFullName)) return null;

        var name = $"M:{typeFullName}.{methodInfo.Name}";
        var parameters = methodInfo.GetParameters();
        if (parameters.Length == 0) return name;

        var paramTypeNames = parameters.Select(p => GetXmlTypeName(p.ParameterType));
        return $"{name}({string.Join(",", paramTypeNames)})";
    }

    // 将 System.Type 转换为 XML 文档中的类型表示（处理常见场景）
    private static string GetXmlTypeName(Type type)
    {
        if (type.IsByRef)
            return GetXmlTypeName(type.GetElementType()) + "&";

        if (type.IsArray)
            return GetXmlTypeName(type.GetElementType()) + "[]";

        if (type.IsGenericType)
        {
            var baseName = type.GetGenericTypeDefinition().FullName;
            // 去掉泛型后缀，如 `1
            baseName = baseName?.Split('`')[0];
            var args = type.GetGenericArguments().Select(GetXmlTypeName);
            return $"{baseName}{{{string.Join(",", args)}}}";
        }

        // 嵌套类型用 + 连接
        if (type.IsNested)
            return $"{type.Namespace}.{type.DeclaringType.Name}+{type.Name}";

        // 普通类型直接 FullName
        return type.FullName ?? type.Name;
    }
}