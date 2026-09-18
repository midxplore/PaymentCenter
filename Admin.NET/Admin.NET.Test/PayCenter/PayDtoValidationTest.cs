// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Admin.NET.Application;

namespace Admin.NET.Test.PayCenter;

/// <summary>
/// 收款分配接口的入参契约（F2 / F4）
/// </summary>
/// <remarks>
/// 纯校验用例：不启动应用、不碰数据库，只固定对外接口的入参边界。
/// 契约见设计文档 §7；改动 DTO 上的校验特性时，本文件必须同步。
/// </remarks>
public class PayDtoValidationTest
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        return results;
    }

    private static bool IsValid(object model) => Validate(model).Count == 0;

    #region AllocateInput（F2）

    [Fact]
    public void AllocateInput_合法入参_应通过()
    {
        Assert.True(IsValid(new AllocateInput { Type = "wxpay", Amount = 0.01m }));
        Assert.True(IsValid(new AllocateInput { Type = "bank", Amount = 100m, ExternalNo = "EXT-1" }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AllocateInput_收款类型为空_应失败(string type)
    {
        Assert.False(IsValid(new AllocateInput { Type = type, Amount = 10m }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void AllocateInput_金额非正_应失败(double amount)
    {
        Assert.False(IsValid(new AllocateInput { Type = "wxpay", Amount = (decimal)amount }));
    }

    [Fact]
    public void AllocateInput_收款类型超长_应失败()
    {
        Assert.False(IsValid(new AllocateInput { Type = new string('x', 33), Amount = 10m }));
    }

    [Fact]
    public void AllocateInput_外部单号超长_应失败()
    {
        Assert.False(IsValid(new AllocateInput { Type = "wxpay", Amount = 10m, ExternalNo = new string('x', 65) }));
    }

    [Fact]
    public void AllocateInput_外部单号可省略_幂等键为可选()
    {
        Assert.True(IsValid(new AllocateInput { Type = "wxpay", Amount = 10m, ExternalNo = null }));
    }

    #endregion AllocateInput（F2）

    #region NotifyInput（F4）

    [Fact]
    public void NotifyInput_合法入参_应通过()
    {
        Assert.True(IsValid(new NotifyInput { OrderNo = "PAY1", Amount = 0.01m, VoucherNo = "V1" }));
        Assert.True(IsValid(new NotifyInput
        {
            OrderNo = "PAY1", Amount = 100m, VoucherNo = "V1", NotifyTime = DateTime.Now
        }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotifyInput_订单号为空_应失败(string orderNo)
    {
        Assert.False(IsValid(new NotifyInput { OrderNo = orderNo, Amount = 10m, VoucherNo = "V1" }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotifyInput_凭证号为空_应失败(string voucherNo)
    {
        Assert.False(IsValid(new NotifyInput { OrderNo = "PAY1", Amount = 10m, VoucherNo = voucherNo }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void NotifyInput_到账金额非正_应失败(double amount)
    {
        Assert.False(IsValid(new NotifyInput { OrderNo = "PAY1", Amount = (decimal)amount, VoucherNo = "V1" }));
    }

    [Fact]
    public void NotifyInput_订单号与凭证号超长_应失败()
    {
        // 上限是 PayConst.OrderNoLength（= 列宽 64），不是历史上写死的 32
        Assert.False(IsValid(new NotifyInput
        {
            OrderNo = new string('x', PayConst.OrderNoLength + 1), Amount = 10m, VoucherNo = "V1"
        }));
        Assert.False(IsValid(new NotifyInput { OrderNo = "PAY1", Amount = 10m, VoucherNo = new string('x', 65) }));
    }

    [Fact]
    public void NotifyInput_到账时间可省略_由服务端补当前时间()
    {
        Assert.True(IsValid(new NotifyInput { OrderNo = "PAY1", Amount = 10m, VoucherNo = "V1", NotifyTime = null }));
    }

    #endregion NotifyInput（F4）

    #region 订单号长度预算（防「列宽放宽了、DTO 校验没跟着放」）

    /// <summary>
    /// 订单号是 <c>{yyyyMMddHHmmssfff}{雪花尾段}</c>，**长度随年份增长**
    /// （2026 年 32 位，约 2027-11 起 33 位）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这里固定住一条容易忘的联动：**列宽 / DTO 校验上限 / 实际生成长度**三者必须同向。
    /// 曾经踩过：数据库列宽已从 32 放宽到 64，但 <see cref="NotifyInput.OrderNo"/> 上的
    /// <c>[MaxLength(32)]</c> 没跟着改 —— 当年正好 32 位所以测不出来，等雪花进位就会
    /// 把所有到账通知挡在参数校验外。
    /// </para>
    /// <para>本用例用反射读特性值，谁再把数字写死就会在这里失败。</para>
    /// </remarks>
    [Fact]
    public void 订单号相关的入参上限_必须等于列宽常量()
    {
        Assert.Equal(PayConst.OrderNoLength, MaxLengthOf<NotifyInput>(nameof(NotifyInput.OrderNo)));
        Assert.Equal(PayConst.OrderNoLength, MaxLengthOf<LinkAbnormalInput>(nameof(LinkAbnormalInput.TargetOrderNo)));
    }

    private static int MaxLengthOf<T>(string propertyName)
    {
        var prop = typeof(T).GetProperty(propertyName);
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        return attr!.Length;
    }

    #endregion 订单号长度预算
}
