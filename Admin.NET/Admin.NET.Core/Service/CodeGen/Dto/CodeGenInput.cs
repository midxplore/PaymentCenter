// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 代码生成参数类
/// </summary>
public class PageCodeGenInput : BasePageInput
{
    /// <summary>
    /// 业务名
    /// </summary>
    public string BusName { get; set; }

    /// <summary>
    /// 表名
    /// </summary>
    public string TableName { get; set; }
}

/// <summary>
/// 代码生成参数类
/// </summary>
public class AddCodeGenInput : SysCodeGen
{
    /// <summary>
    /// 生成方式
    /// </summary>
    [Dict(nameof(CodeGenMethodEnum))]
    public override CodeGenMethodEnum GenerateMethod { get; set; }

    /// <summary>
    /// 生成场景
    /// </summary>
    [Dict(nameof(CodeGenSceneEnum))]
    public override CodeGenSceneEnum Scene { get; set; }

    /// <summary>
    /// 命名空间
    /// </summary>
    [Required(ErrorMessage = "命名空间不能为空")]
    public override string NameSpace { get; set; }

    /// <summary>
    /// 业务名
    /// </summary>
    [Required(ErrorMessage = "业务名不能为空")]
    public override string BusName { get; set; }

    /// <summary>
    /// 关联表
    /// </summary>
    [Required(ErrorMessage = "关联表列表不能为空")]
    public new List<AddSysCodeGenTable> TableList { get; set; } = new();

    /// <summary>
    /// 页面目录
    /// </summary>
    [Required(ErrorMessage = "页面目录不能为空")]
    public override string PagePath { get; set; }

    /// <summary>
    /// 支持打印类型
    /// </summary>
    [Dict(nameof(CodeGenPrintTypeEnum))]
    public override CodeGenPrintTypeEnum PrintType { get; set; }

    /// <summary>
    /// 页面目录
    /// </summary>
    public new EffectTreeConfigInput TreeConfig { get; set; }
}

/// <summary>
/// 添加关联表
/// </summary>
public class AddSysCodeGenTable : SysCodeGenTable
{
    /// <summary>
    /// 代码生成Id
    /// </summary>
    [Required(ErrorMessage = "代码生成Id不能为空")]
    public override long CodeGenId { get; set; }

    /// <summary>
    /// 数据库配置id
    /// </summary>
    [Required(ErrorMessage = "数据库配置id不能为空")]
    public override string ConfigId { get; set; }

    /// <summary>
    /// 数据库表名
    /// </summary>
    [Required(ErrorMessage = "数据库表名不能为空")]
    public override string TableName { get; set; }

    /// <summary>
    /// 表实体名称
    /// </summary>
    [Required(ErrorMessage = "表实体名称不能为空")]
    public override string EntityName { get; set; }

    /// <summary>
    /// 模块名称
    /// </summary>
    [Required(ErrorMessage = "模块名称不能为空")]
    public override string ModuleName { get; set; }

    /// <summary>
    /// 业务名称
    /// </summary>
    [Required(ErrorMessage = "业务名称不能为空")]
    public override string BusName { get; set; }

    /// <summary>
    /// 字段列表
    /// </summary>
    [Required(ErrorMessage = "字段列表不能为空")]
    public new List<AddSysCodeGenColumn> ColumnList { get; set; }
}

/// <summary>
/// 增加表字段输入参数
/// </summary>
public class AddSysCodeGenColumn : SysCodeGenColumn
{
    /// <summary>
    /// 代码生成表Id
    /// </summary>
    [Required(ErrorMessage = "代码生成表Id不能为空")]
    public override long CodeGenTableId { get; set; }

    /// <summary>
    /// 数据库字段名
    /// </summary>
    [Required(ErrorMessage = "数据库字段名不能为空")]
    public override string ColumnName { get; set; }

    /// <summary>
    /// 实体属性名
    /// </summary>
    [Required(ErrorMessage = "实体属性名不能为空")]
    public override string PropertyName { get; set; }

    /// <summary>
    /// .NET数据类型
    /// </summary>
    [Required(ErrorMessage = ".NET数据类型不能为空")]
    public override string NetType { get; set; }

    /// <summary>
    /// 数据库中类型（物理类型）
    /// </summary>
    [Required(ErrorMessage = "数据库中类型不能为空")]
    public override string DataType { get; set; }

    /// <summary>
    /// 字段描述
    /// </summary>
    [Required(ErrorMessage = "字段描述不能为空")]
    public override string ColumnComment { get; set; }

    /// <summary>
    /// 控件类型
    /// </summary>
    [Dict(nameof(CodeGenEffectTypeEnum))]
    public override CodeGenEffectTypeEnum EffectType { get; set; }

    /// <summary>
    /// 字段验证规则
    /// </summary>
    [Dict(nameof(CodeGenFromRuleValidEnum))]
    public override CodeGenFromRuleValidEnum? FromValid { get; set; }
}

public class DeleteCodeGenInput
{
    /// <summary>
    /// Id列表
    /// </summary>
    [NotEmpty(ErrorMessage = "Id列表不能为空")]
    [Required(ErrorMessage = "Id列表不能为空")]
    public List<long> Id { get; set; }
}

public class UpdateCodeGenInput : AddCodeGenInput
{
    /// <summary>
    /// 代码生成器Id
    /// </summary>
    [Required(ErrorMessage = "代码生成器Id不能为空")]
    public override long Id { get; set; }
}

/// <summary>
/// 获取默认列配置输入参数
/// </summary>
public class DefaultColumnConfigInput
{
    /// <summary>
    /// 数据库配置id
    /// </summary>
    public string ConfigId { get; set; } = SqlSugarConst.MainConfigId;

    /// <summary>
    /// 数据库表名
    /// </summary>
    [Required(ErrorMessage = "数据库表名不能为空")]
    public string TableName { get; set; }
}