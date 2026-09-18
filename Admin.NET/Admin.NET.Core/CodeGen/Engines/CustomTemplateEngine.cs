// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 自定义模板引擎
/// </summary>
public class CustomTemplateEngine : ViewEngineModel
{
    /// <summary>
    /// 场景
    /// </summary>
    public int Scene { get; set; }

    /// <summary>
    /// 作者
    /// </summary>
    public string AuthorName { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    public string ModuleName { get; set; }

    /// <summary>
    /// 命名空间
    /// </summary>
    public string NameSpace { get; set; }

    /// <summary>
    /// 项目最后名称
    /// </summary>
    public string ProjectLastName { get; set; }

    /// <summary>
    /// 前端页面目录名
    /// </summary>
    public string PagePath { get; set; } = "main";

    /// <summary>
    /// 打印模板类型
    /// </summary>
    public string PrintType { get; set; }

    /// <summary>
    /// 自定义打印模板名
    /// </summary>
    public string PrintName { get; set; }

    /// <summary>
    /// 是否使用Swagger接口
    /// </summary>
    public bool IsApiService { get; set; }

    /// <summary>
    /// 库定位器
    /// </summary>
    public string ConfigId { get; set; }

    /// <summary>
    /// 主表类名
    /// </summary>
    public string ClassName { get; set; }

    /// <summary>
    /// 首字母小写主表类名
    /// </summary>
    public string LowerClassName { get; set; }

    /// <summary>
    /// 业务名
    /// </summary>
    public string BusName { get; set; }

    /// <summary>
    /// 是否有状态字段
    /// </summary>
    public bool HasStatus { get; set; }

    /// <summary>
    /// 是否有上传字段
    /// </summary>
    public bool HasUpload { get; set; }

    /// <summary>
    /// 是否有关联表
    /// </summary>
    public bool HasJoinTable { get; set; }

    /// <summary>
    /// 是否只有Id单主键
    /// </summary>
    public bool IsOnlyIdPrimary { get; set; }

    /// <summary>
    /// 上级联表字段
    /// </summary>
    public string? LastLinkPropertyName { get; set; }

    /// <summary>
    /// 下级联表字段
    /// </summary>
    public string? NextLinkPropertyName { get; set; }

    /// <summary>
    /// 树组件配置
    /// </summary>
    public TreeWithTableConfigInput TreeConfig { get; set; }

    /// <summary>
    /// 是否是横向布局
    /// </summary>
    public bool IsHorizontal { get; set; }

    /// <summary>
    /// 对照目标表配置
    /// </summary>
    public TableRelationshipConfigInput RelationshipTable { get; set; }

    /// <summary>
    /// 从表配置
    /// </summary>
    public CustomTemplateEngine SlaveTable { get; set; }

    /// <summary>
    /// 所有字段集
    /// </summary>
    public List<CodeGenColumnConfig> AllFields { get; set; }

    /// <summary>
    /// 查询字段集
    /// </summary>
    public List<CodeGenColumnConfig> QueryFields { get; set; }

    /// <summary>
    /// 表格字段集
    /// </summary>
    public List<CodeGenColumnConfig> TableFields { get; set; }

    /// <summary>
    /// 导入导出字段集
    /// </summary>
    public List<CodeGenColumnConfig> ImportFields { get; set; }

    /// <summary>
    /// 主键字段集
    /// </summary>
    public List<CodeGenColumnConfig> PrimaryFields { get; set; }

    /// <summary>
    /// 增改字段集
    /// </summary>
    public List<CodeGenColumnConfig> AddUpdateFields { get; set; }

    /// <summary>
    /// 上传字段集
    /// </summary>
    public List<CodeGenColumnConfig> UploadFields { get; set; }

    /// <summary>
    /// 下拉框字段集
    /// </summary>
    public List<CodeGenColumnConfig> DropdownFields { get; set; }

    /// <summary>
    /// 接口字段集
    /// </summary>
    public List<CodeGenColumnConfig> ApiFields { get; set; }

    /// <summary>
    /// 数据库字段集
    /// </summary>
    public List<ColumnOutput> ColumnList { get; set; }

    /// <summary>
    /// 获取首字母小写文本
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public string GetFirstLower(string text) => text?.ToFirstLetterLowerCase();

    /// <summary>
    /// 根据.NET类型获取TypeScript类型
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public string GetTypeScriptType(string type) => CodeGenHelper.GetTypeScriptType(type);

    /// <summary>
    /// 格式化主键查询条件
    /// 例： PrimaryKeysFormat(" || ", "u.{0} == input.{0}")
    /// 单主键返回 u.Id == input.Id
    /// 组合主键返回 u.Id == input.Id || u.FkId == input.FkId
    /// </summary>
    /// <param name="separator">分隔符</param>
    /// <param name="format">模板字符串</param>
    /// <param name="lowerFirstLetter">字段首字母小写</param>
    /// <returns></returns>
    public string PrimaryKeysFormat(string separator, string format, bool lowerFirstLetter = false) => string.Join(separator, PrimaryFields.Select(u => string.Format(format, lowerFirstLetter ? u.LowerPropertyName : u.PropertyName)));

    /// <summary>
    /// 根据配置获取前端HttpApi方法名
    /// </summary>
    /// <param name="column"></param>
    /// <param name="type"></param>
    /// <param name="engine"></param>
    /// <returns></returns>
    public string GetHttpApiMethodName(CodeGenColumnConfig column, string type, CustomTemplateEngine engine = null)
    {
        engine ??= this;
        var entityName = SlaveTable != null && SlaveTable.ApiFields.Any(u => u == column) ? SlaveTable.ClassName : engine.ClassName;
        var suffixName = type switch
        {
            "page" => IsApiService ? "PagePost" : type, // 分页查询
            "update" => IsApiService ? "UpdatePost" : type, // 更新记录
            "add" => IsApiService ? "AddPost" : type, // 新增记录
            "detail" => IsApiService ? "DetailGet" : type, // 详情
            "delete" => IsApiService ? "DeletePost" : type, // 删除
            "batchDelete" => IsApiService ? "BatchDeletePost" : type, // 批量删除
            "setStatus" => IsApiService ? "SetStatusPost" : type, // 设置状态
            "exportData" => IsApiService ? "ExportPost" : type, // 数据导出
            "importData" => IsApiService ? "ImportPostForm" : type, // 数据导入
            "downloadTemplate" => IsApiService ? "ImportGet" : type, // 下载模板
            "fkTable" => IsApiService ? $"{column.JoinConfig.EntityName}PagePost" : "page", // 外键
            "fkTree" => IsApiService ? $"{column.JoinConfig.EntityName}TreePost" : $"get{column.JoinConfig.EntityName}Tree", // 树
            "upload" => IsApiService ? $"Upload{column?.PropertyName}PostForm" : $"upload{column?.PropertyName}", // 上传
            _ => throw new Exception("未知接口类型")
        };
        return IsApiService ? $"getAPI({entityName}Api).api{entityName}{suffixName}" : $"{GetFirstLower(entityName)}Api.{suffixName}";
    }
}