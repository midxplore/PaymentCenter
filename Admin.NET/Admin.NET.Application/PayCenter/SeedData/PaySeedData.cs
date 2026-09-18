// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 收款类型字典类型种子数据（F1.3）
/// </summary>
/// <remarks>
/// code = <see cref="PayConst.AccountTypeDictCode"/>（pay_account_type）。
/// 采用骨架字典管理，便于后台自行增删收款类型，无需改代码。
/// </remarks>
[SeedData(500)]
[IncreSeed]
public class PayDictTypeSeedData : ISqlSugarEntitySeedData<SysDictType>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysDictType> HasData()
    {
        return
        [
            new SysDictType{ Id=1300000005101, Name="收款类型", Code=PayConst.AccountTypeDictCode, SysFlag=YesNoEnum.Y, OrderNo=510, Remark="收款账号的收款类型标签（如微信、支付宝）", Status=StatusEnum.Enable, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
        ];
    }
}

/// <summary>
/// 收款类型字典值种子数据（F1.3）
/// </summary>
[SeedData(501)]
[IncreSeed]
public class PayDictDataSeedData : ISqlSugarEntitySeedData<SysDictData>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysDictData> HasData()
    {
        return
        [
            new SysDictData{ Id=1300000005101, DictTypeId=1300000005101, Label="微信收款码", Value="wxpay", OrderNo=100, Remark="微信个人/商户收款码", Status=StatusEnum.Enable, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
            new SysDictData{ Id=1300000005102, DictTypeId=1300000005101, Label="支付宝收款码", Value="alipay", OrderNo=101, Remark="支付宝收款码", Status=StatusEnum.Enable, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
            new SysDictData{ Id=1300000005103, DictTypeId=1300000005101, Label="银行卡转账", Value="bank", OrderNo=102, Remark="银行卡号转账", Status=StatusEnum.Enable, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
            new SysDictData{ Id=1300000005104, DictTypeId=1300000005101, Label="云闪付收款码", Value="cloudquickpass", OrderNo=103, Remark="云闪付收款码", Status=StatusEnum.Enable, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
        ];
    }
}

/// <summary>
/// 收款中心系统配置种子数据（F3.1）
/// </summary>
[SeedData(502)]
[IncreSeed]
public class PayConfigSeedData : ISqlSugarEntitySeedData<SysConfig>
{
    /// <summary>
    /// 种子数据
    /// </summary>
    /// <returns></returns>
    public IEnumerable<SysConfig> HasData()
    {
        return
        [
            new SysConfig{ Id=1300000005201, Name="订单过期时长（分钟）", Code=PayConst.OrderExpireMinutes, Value=PayConst.DefaultOrderExpireMinutes.ToString(), SysFlag=YesNoEnum.Y, Remark="收款订单超过该时长未达成即自动过期并释放预占额度", OrderNo=510, GroupCode=ConfigConst.SysDefaultGroup, CreateTime=DateTime.Parse("2026-09-16 00:00:00") },
        ];
    }
}
