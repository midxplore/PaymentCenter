// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

public class TableEntityEngine : ViewEngineModel
{
    /// <summary>
    /// 作者
    /// </summary>
    public string AuthorName { get; set; } = "Admin.NET";

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; } = "Admin.NET@qq.com";

    /// <summary>
    /// 命名空间
    /// </summary>
    public string NameSpace { get; set; }

    /// <summary>
    /// 库配置Id
    /// </summary>
    public string ConfigId { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// 实体名
    /// </summary>
    public string EntityName { get; set; }

    /// <summary>
    /// 表描述
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 基类名
    /// </summary>
    public string BaseClassName { get; set; }

    /// <summary>
    /// 字段集合
    /// </summary>
    public List<DbColumnInfo> TableFields { get; set; }
}