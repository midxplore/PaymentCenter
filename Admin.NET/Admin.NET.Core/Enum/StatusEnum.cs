namespace Admin.NET.Core;

/// <summary>
/// 通用状态枚举
/// </summary>
[Description("通用状态枚举")]
public enum StatusEnum
{
    /// <summary>
    /// 启用
    /// </summary>
    [Description("启用")]
    Enable = 1,

    /// <summary>
    /// 禁用
    /// </summary>
    [Description("禁用")]
    Disable = 2,
    
    /// <summary>
    /// 停用（用户无法通知）
    /// </summary>
    [Description("禁止")]
    Forbidden = 3,
    
    /// <summary>
    /// 删除
    /// </summary>
    [Description("删除")]
    Remove = 4
}