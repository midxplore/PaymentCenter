// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core.Service;

public class GenerateSQLInput
{
    public List<SelectConfig> SelectConfigs { get; set; }

    public List<JoinConfig> JoinConfigs { get; set; }

    public List<WhereConfig> WhereConfigs { get; set; }

    public List<OrderConfig> OrderConfigs { get; set; }
}

public class SelectConfig
{
    public string Table { get; set; }

    public string Column { get; set; }
}

public class JoinConfig
{
    public string JoinType { get; set; }

    public string LeftTable { get; set; }

    public string RightTable { get; set; }

    public List<JoinConfig_TableColumn> TableColumns { get; set; }
}

public class JoinConfig_TableColumn
{
    public string LeftTableColumn { get; set; }

    public string RightTableColumn { get; set; }
}

public class WhereConfig
{
    public string Table { get; set; }

    public string Column { get; set; }

    public string Operator { get; set; }

    public string Value { get; set; }

    public string DataType { get; set; }

    public string JoinType { get; set; }
}

public class OrderConfig
{
    public string Table { get; set; }

    public string Column { get; set; }

    public int Sort { get; set; }
}