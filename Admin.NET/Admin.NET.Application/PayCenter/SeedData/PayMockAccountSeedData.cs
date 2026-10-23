// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 模拟收款账号种子（联调 allocate / 收银台用，备注标明模拟）
/// </summary>
[SeedData(516)]
[IncreSeed]
public class PayMockAccountSeedData : ISqlSugarEntitySeedData<PayAccount>
{
    public IEnumerable<PayAccount> HasData()
    {
        var t = DateTime.Parse("2026-09-22 00:00:00");
        return
        [
            new PayAccount
            {
                Id = 1300000000801,
                Type = "wxpay",
                PayeeName = "模拟微信收款人",
                AccountNo = "mock-wx-001",
                AccountInfo = "mock-wx-001",
                QrImageUrl = "",
                TotalQuota = 5000m,
                UsedQuota = 0m,
                LockedQuota = 0m,
                Status = PayAccountStatusEnum.Enabled,
                Remark = "模拟·微信小额档",
                EmptyExpireCount = 0,
                CreateTime = t
            },
            new PayAccount
            {
                Id = 1300000000802,
                Type = "wxpay",
                PayeeName = "模拟微信收款人",
                AccountNo = "mock-wx-002",
                AccountInfo = "mock-wx-002",
                QrImageUrl = "",
                TotalQuota = 50000m,
                UsedQuota = 0m,
                LockedQuota = 0m,
                Status = PayAccountStatusEnum.Enabled,
                Remark = "模拟·微信大额档",
                EmptyExpireCount = 0,
                CreateTime = t
            },
            new PayAccount
            {
                Id = 1300000000803,
                Type = "alipay",
                PayeeName = "模拟支付宝收款人",
                AccountNo = "mock-ali-001",
                AccountInfo = "mock-ali-001",
                QrImageUrl = "",
                TotalQuota = 8000m,
                UsedQuota = 0m,
                LockedQuota = 0m,
                Status = PayAccountStatusEnum.Enabled,
                Remark = "模拟·支付宝",
                EmptyExpireCount = 0,
                CreateTime = t
            },
            new PayAccount
            {
                Id = 1300000000804,
                Type = "bank",
                PayeeName = "模拟银行收款人",
                AccountNo = "6222020000000000001",
                AccountInfo = "6222020000000000001",
                BankName = "中国工商银行",
                BranchName = "模拟支行",
                QrImageUrl = "",
                TotalQuota = 100000m,
                UsedQuota = 0m,
                LockedQuota = 0m,
                Status = PayAccountStatusEnum.Enabled,
                Remark = "请勿使用工商银行卡支付，该银行卡付款可能导致无法到账。\n若您已完成付款，请务必点击【我已付款完成】",
                EmptyExpireCount = 0,
                CreateTime = t
            },
        ];
    }
}
