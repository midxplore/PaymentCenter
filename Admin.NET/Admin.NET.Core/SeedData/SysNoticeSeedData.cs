// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Core;

/// <summary>
/// 系统通知公告表种子数据
/// </summary>
[IgnoreUpdateSeed]
public class SysNoticeSeedData : ISqlSugarEntitySeedData<SysNotice>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysNotice> HasData()
    {
        return
        [
            new SysNotice{ Id=1300000000101, Title="欢迎使用支付中心 · 收款账号分配系统", Content="在「收款管理」下维护收款账号与额度、查询收款订单、处理异常到账；对外接口（签名鉴权 + scope）与额度规则见项目文档。", CreateTime=DateTime.Parse("2025-03-26 00:00:00"), Status=NoticeStatusEnum.PUBLIC },
        ];
    }
}