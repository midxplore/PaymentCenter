// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Application;

/// <summary>
/// 对外接口：收款匹配与订单状态查询（<c>/api/pay/*</c>，签名鉴权）
/// </summary>
[ApiDescriptionSettings(Name = "pay", Order = 401, Description = "收款匹配")]
public class PayAllocateController : IDynamicApiController, ITransient
{
    private readonly PayAllocateService _payAllocateService;

    public PayAllocateController(PayAllocateService payAllocateService)
    {
        _payAllocateService = payAllocateService;
    }

    /// <summary>
    /// 查询匹配收款账号
    /// </summary>
    [ApiDescriptionSettings(Name = "Allocate"), HttpPost]
    [DisplayName("查询匹配收款账号")]
    [Authorize(AuthenticationSchemes = SignatureAuthenticationDefaults.AuthenticationScheme)]
    [PayScope(PayConst.ScopeAllocate)]
    public async Task<AllocateOutput> Allocate(AllocateInput input)
        => await _payAllocateService.Allocate(input);

    /// <summary>
    /// 查询收款订单状态。查无此单与非本调用方的订单都返回 API_ORDER_NOT_FOUND。
    /// </summary>
    [DisplayName("查询收款订单状态")]
    [Authorize(AuthenticationSchemes = SignatureAuthenticationDefaults.AuthenticationScheme)]
    [PayScope(PayConst.ScopeAllocate)]
    public async Task<OrderQueryOutput> GetStatus([FromQuery] string orderNo)
        => await _payAllocateService.GetStatus(orderNo);
}
