// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;
using Furion;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 收银台：token 门禁、提交等待确认、取消释放额度
/// </summary>
public class PayCashierTest : IDisposable
{
    private const string TestType = "unittest_cashier";
    private const string ExternalNoPrefix = "unittest_cashier_";

    private readonly ISqlSugarClient _db;
    private readonly PayAllocateService _allocateService;
    private readonly PayCashierService _cashierService;
    private readonly IDisposable _caller;

    public PayCashierTest()
    {
        _db = App.GetRequiredService<ISqlSugarClient>();
        _allocateService = App.GetRequiredService<PayAllocateService>();
        _cashierService = App.GetRequiredService<PayCashierService>();
        _caller = PayTestCaller.Begin();
        Cleanup();
    }

    public void Dispose()
    {
        Cleanup();
        _caller.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
    {
        _db.Ado.ExecuteCommand(
            "delete from pay_audit_log where targettype = 'Order' and targetid in (select id from pay_order where externalno like @prefix)",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand(
            "delete from pay_order_event where orderno in (select orderno from pay_order where externalno like @prefix)",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_order where externalno like @prefix",
            new SugarParameter("@prefix", ExternalNoPrefix + "%"));
        _db.Ado.ExecuteCommand("delete from pay_account where type = @type",
            new SugarParameter("@type", TestType));
    }

    private long SeedAccount(decimal total)
    {
        var account = new PayAccount
        {
            Type = TestType,
            PayeeName = "测试收款人",
            AccountNo = "unittest-cashier-acc",
            AccountInfo = "unittest-cashier-acc",
            TotalQuota = total,
            UsedQuota = 0m,
            LockedQuota = 0m,
            Status = PayAccountStatusEnum.Enabled,
            Remark = "收银台单测"
        };
        _db.Insertable(account).ExecuteCommand();
        return account.Id;
    }

    [Fact]
    public async Task Allocate_应返回收银台链接且Token可查询()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 20m,
            ExternalNo = ExternalNoPrefix + "get"
        });

        Assert.Contains("/#/c/", output.CashierUrl);
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        var view = await _cashierService.GetAsync(order.CashierToken);
        Assert.Equal(order.OrderNo, view.OrderNo);
        Assert.Equal(20m, view.RequestAmount);
        Assert.Equal(1, view.Step);
        Assert.True(view.CanPay);
    }

    [Fact]
    public async Task Allocate_应落库返回地址并在收银台回显()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 18m,
            ExternalNo = ExternalNoPrefix + "ret",
            ReturnUrl = "https://merchant.example/done",
            ReturnLabel = "回游戏"
        });

        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        Assert.Equal("https://merchant.example/done", order.ReturnUrl);
        Assert.Equal("回游戏", order.ReturnLabel);

        var view = await _cashierService.GetAsync(order.CashierToken);
        Assert.Equal("https://merchant.example/done", view.ReturnUrl);
        Assert.Equal("回游戏", view.ReturnLabel);
    }

    [Fact]
    public async Task Allocate_未传返回文案_收银台默认返回()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 11m,
            ExternalNo = ExternalNoPrefix + "retdef"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        var view = await _cashierService.GetAsync(order.CashierToken);
        Assert.True(string.IsNullOrEmpty(order.ReturnUrl));
        Assert.Equal(PayConst.DefaultReturnLabel, view.ReturnLabel);
    }

    [Fact]
    public async Task 非法Token_应报订单不存在()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _cashierService.GetAsync("totally-invalid-token-xx"));
    }

    [Fact]
    public async Task 提交凭证_应自动到账完成()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 15m,
            ExternalNo = ExternalNoPrefix + "submit"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);

        // 直接写入凭证 JSON，绕过上传（单测不走 IFormFile）
        var urls = System.Text.Json.JsonSerializer.Serialize(new[] { "/upload/pay-voucher/t/a.jpg" });
        await _db.Updateable<PayOrder>()
            .SetColumns(u => new PayOrder { VoucherUrls = urls, VoucherUrl = "/upload/pay-voucher/t/a.jpg" })
            .Where(u => u.Id == order.Id)
            .ExecuteCommandAsync();

        var submit = await _cashierService.SubmitAsync(order.CashierToken);
        Assert.True(submit.Paid);

        var fresh = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(PayOrderStatusEnum.Completed, fresh.Status);
        Assert.Equal(15m, fresh.ReceivedAmount);
        var view = await _cashierService.GetAsync(order.CashierToken);
        Assert.Equal(3, view.Step);
        Assert.False(view.CanPay);
    }

    [Fact]
    public async Task 等待确认_不应被自动过期()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 12m,
            ExternalNo = ExternalNoPrefix + "noexpire"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        var urls = System.Text.Json.JsonSerializer.Serialize(new[] { "/upload/pay-voucher/t/b.jpg" });
        await _db.Updateable<PayOrder>()
            .SetColumns(u => new PayOrder
            {
                VoucherUrls = urls,
                Status = PayOrderStatusEnum.AwaitingConfirm,
                ExpireTime = DateTime.Now.AddMinutes(-5)
            })
            .Where(u => u.Id == order.Id)
            .ExecuteCommandAsync();

        var expire = App.GetRequiredService<PayOrderExpireService>();
        await expire.ExpireOrderAsync(order.Id);

        var fresh = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(PayOrderStatusEnum.AwaitingConfirm, fresh.Status);
    }

    [Fact]
    public async Task 取消订单_应释放预占并置过期()
    {
        var accountId = SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 25m,
            ExternalNo = ExternalNoPrefix + "cancel"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        Assert.Equal(25m, _db.Queryable<PayAccount>().First(u => u.Id == accountId).LockedQuota);

        await _cashierService.CancelAsync(order.CashierToken);

        var fresh = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(PayOrderStatusEnum.Expired, fresh.Status);
        Assert.Equal(0m, _db.Queryable<PayAccount>().First(u => u.Id == accountId).LockedQuota);
    }

    [Fact]
    public async Task 无凭证提交_应拒绝()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 10m,
            ExternalNo = ExternalNoPrefix + "novoucher"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        await Assert.ThrowsAnyAsync<Exception>(() => _cashierService.SubmitAsync(order.CashierToken));
    }

    [Fact]
    public async Task 重传确认提交_才替换旧凭证()
    {
        SeedAccount(100m);
        var output = await _allocateService.Allocate(new AllocateInput
        {
            Type = TestType,
            Amount = 11m,
            ExternalNo = ExternalNoPrefix + "resubmit"
        });
        var order = _db.Queryable<PayOrder>().First(u => u.OrderNo == output.OrderNo);
        var prefix = order.CashierToken.Length > 8 ? order.CashierToken[..8] : order.CashierToken;
        var oldUrl = $"/upload/pay-voucher/{prefix}/old.jpg";
        var newUrl = $"/upload/pay-voucher/{prefix}/new.jpg";
        var oldJson = System.Text.Json.JsonSerializer.Serialize(new[] { oldUrl });
        await _db.Updateable<PayOrder>()
            .SetColumns(u => new PayOrder
            {
                VoucherUrls = oldJson,
                VoucherUrl = oldUrl,
                Status = PayOrderStatusEnum.AwaitingConfirm
            })
            .Where(u => u.Id == order.Id)
            .ExecuteCommandAsync();

        // 未带新 URL 再提交：旧凭证保持
        await _cashierService.SubmitAsync(order.CashierToken);
        var mid = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(oldUrl, mid.VoucherUrl);

        // 带草稿 URL 提交：此时才替换
        await _cashierService.SubmitAsync(order.CashierToken, new CashierSubmitInput
        {
            VoucherUrls = [newUrl]
        });
        var fresh = _db.Queryable<PayOrder>().First(u => u.Id == order.Id);
        Assert.Equal(PayOrderStatusEnum.AwaitingConfirm, fresh.Status);
        Assert.Equal(newUrl, fresh.VoucherUrl);
        Assert.Contains("new.jpg", fresh.VoucherUrls);
        Assert.DoesNotContain("old.jpg", fresh.VoucherUrls);
    }
}
