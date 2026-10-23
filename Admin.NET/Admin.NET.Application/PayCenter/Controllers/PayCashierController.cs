// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Microsoft.AspNetCore.Http;

namespace Admin.NET.Application;

/// <summary>
/// 收银台匿名接口（token 门禁，不走签名鉴权）
/// </summary>
[ApiDescriptionSettings(Name = "pay", Order = 402, Description = "收银台")]
public class PayCashierController : IDynamicApiController, ITransient
{
    private readonly PayCashierService _cashierService;

    public PayCashierController(PayCashierService cashierService)
    {
        _cashierService = cashierService;
    }

    /// <summary>获取收银台订单展示</summary>
    [AllowAnonymous]
    [HttpGet("cashier/{token}")]
    [DisplayName("收银台查询")]
    public Task<CashierOrderOutput> GetCashier(string token)
        => _cashierService.GetAsync(token);

    /// <summary>
    /// 上传付款凭证。
    /// draft=true：只存文件返回 URL，不改订单（重传草稿）；确认提交时再整体替换。
    /// replace=true：立即清空后写入（兼容旧客户端，新重传勿用）。
    /// </summary>
    [AllowAnonymous]
    [HttpPost("cashier/{token}/voucher")]
    [DisplayName("收银台上传凭证")]
    public Task<string> UploadCashierVoucher(string token, [Required] IFormFile file,
        [FromForm] bool replace = false, [FromForm] bool draft = false)
        => _cashierService.UploadVoucherAsync(token, file, replace, draft);

    /// <summary>提交凭证 → 自动到账；body.voucherUrls 有值则此时才替换旧凭证</summary>
    [AllowAnonymous]
    [HttpPost("cashier/{token}/submit")]
    [DisplayName("收银台提交凭证")]
    public Task<CashierSubmitOutput> SubmitCashier(string token, [FromBody] CashierSubmitInput input = null)
        => _cashierService.SubmitAsync(token, input);

    /// <summary>取消订单（真取消）</summary>
    [AllowAnonymous]
    [HttpPost("cashier/{token}/cancel")]
    [DisplayName("收银台取消订单")]
    public Task CancelCashier(string token)
        => _cashierService.CancelAsync(token);
}
