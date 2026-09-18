// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

namespace Admin.NET.Test.Attribute;

public class RequiredIFTest
{
    #region 辅助方法：对象校验

    private static ValidationResult ValidateProperty(object instance, string propertyName, object value)
    {
        var context = new ValidationContext(instance) { MemberName = propertyName };
        var property = instance.GetType().GetProperty(propertyName);
        return property?.GetCustomAttribute<ValidationAttribute>()?.GetValidationResult(value, context);
    }

    private static bool IsValid(object instance) => Validator.TryValidateObject(instance, new ValidationContext(instance), null, true);

    #endregion 辅助方法：对象校验

    #region 示例1：等于时必填 [RequiredIF(nameof(TarProperty), 1)]

    [Theory]
    [InlineData(1, null, false)]     // TarProperty=1, SomeProperty=null → 失败
    [InlineData(1, "", false)]       // TarProperty=1, SomeProperty="" → 失败
    [InlineData(1, " ", false)]      // TarProperty=1, SomeProperty=" " → 失败
    [InlineData(1, "a", true)]       // TarProperty=1, SomeProperty="a" → 成功
    [InlineData(2, null, true)]      // TarProperty≠1, SomeProperty=null → 成功（不触发）
    public void RequiredIF_Equal_WhenTargetIsValue_ShouldRequire(int tarProperty, string someProperty, bool expectedValid)
    {
        var model = new RequiredIfTest1Input { TarProperty = tarProperty, SomeProperty = someProperty };
        var result = ValidateProperty(model, nameof(model.SomeProperty), someProperty);
        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    private class RequiredIfTest1Input
    {
        public int TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), 1, ErrorMessage = "TarProperty为1时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    #endregion 示例1：等于时必填 [RequiredIF(nameof(TarProperty), 1)]

    #region 示例2：不等于时必填 [RequiredIF(nameof(TarProperty), 1, Operator.NotEqual)]

    [Theory]
    [InlineData(1, "a", true)]       // TarProperty=1 → 不触发必填
    [InlineData(2, null, false)]     // TarProperty≠1, SomeProperty=null → 必填失败
    [InlineData(0, "", false)]       // TarProperty≠1, SomeProperty="" → 失败
    [InlineData(3, "ok", true)]      // TarProperty≠1, SomeProperty=ok → 成功
    public void RequiredIF_NotEqual_WhenTargetIsNotValue_ShouldRequire(int tarProperty, string someProperty, bool expectedValid)
    {
        var model = new RequiredIfTest2Input { TarProperty = tarProperty, SomeProperty = someProperty };
        var result = ValidateProperty(model, nameof(model.SomeProperty), someProperty);
        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    private class RequiredIfTest2Input
    {
        public int TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), 1, Operator.NotEqual, ErrorMessage = "TarProperty不为1时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    #endregion 示例2：不等于时必填 [RequiredIF(nameof(TarProperty), 1, Operator.NotEqual)]

    #region 示例3：包含时必填 [RequiredIF(nameof(TarProperty), new[]{1,2}, Operator.Contains)]

    [Theory]
    [InlineData(new int[] { 1 }, null, false)]
    [InlineData(new int[] { 2 }, "", false)]
    [InlineData(new int[] { 1, 3 }, "x", true)]
    [InlineData(new int[] { 3, 4 }, null, true)]  // 不包含1或2 → 不必填
    public void RequiredIF_Contains_WhenTargetContainsValue_ShouldRequire(int[] tarProperty, string someProperty, bool expectedValid)
    {
        var model = new RequiredIfTest3Input { TarProperty = tarProperty, SomeProperty = someProperty };
        var result = ValidateProperty(model, nameof(model.SomeProperty), someProperty);
        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    private class RequiredIfTest3Input
    {
        public int[] TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), new object[] { 1, 2 }, Operator.Contains, ErrorMessage = "TarProperty包含1或2时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    #endregion 示例3：包含时必填 [RequiredIF(nameof(TarProperty), new[]{1,2}, Operator.Contains)]

    #region 示例4：不包含时必填 [RequiredIF(nameof(TarProperty), new[]{1,2}, Operator.NotContains)]

    [Theory]
    [InlineData(new[] { 1 }, null, true)]     // 包含1 → 不必填
    [InlineData(new[] { 2 }, "", true)]       // 包含2 → 不必填
    [InlineData(new[] { 3, 4 }, null, false)] // 不包含1,2 → 必填
    [InlineData(new[] { 3, 4 }, "ok", true)]  // 不包含1,2 → 必填
    public void RequiredIF_NotContains_WhenTargetNotContainsValue_ShouldRequire(int[] tarProperty, string someProperty, bool expectedValid)
    {
        var result = IsValid(new RequiredIfTest4Input { TarProperty = tarProperty, SomeProperty = someProperty });
        Assert.Equal(expectedValid, result);
    }

    private class RequiredIfTest4Input
    {
        public int[] TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), new object[] { 1, 2 }, Operator.NotContains, ErrorMessage = "TarProperty不包含1和2时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    #endregion 示例4：不包含时必填 [RequiredIF(nameof(TarProperty), new[]{1,2}, Operator.NotContains)]

    #region 特殊类型支持：long、string、IList 等

    [Theory]
    [InlineData(0L, null, false)]     // long=0 → 必填，无值 → 失败
    [InlineData(1L, null, true)]    // long=1 ≠ 0 → 不必填
    [InlineData(0L, "a", true)]      // long=0 → 必填，有值 → 成功
    public void RequiredIF_WhenTargetIsLong_ShouldTreatZeroAsEmpty(long tarProperty, string someProperty, bool expectedValid)
    {
        var model = new RequiredIfTestLongInput { TarProperty = tarProperty, SomeProperty = someProperty };
        var result = ValidateProperty(model, nameof(model.SomeProperty), someProperty);
        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    private class RequiredIfTestLongInput
    {
        public long TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), ErrorMessage = "TarProperty为0时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    [Theory]
    [InlineData(new string[] { }, null, false)]     // 空集合 → 必填
    [InlineData(new[] { "a" }, null, true)] // 非空集合 → 非必填
    public void RequiredIF_WhenTargetIsList_ShouldTreatEmptyAsNull(string[] tarProperty, string someProperty, bool expectedValid)
    {
        var model = new RequiredIfTestListInput { TarProperty = tarProperty, SomeProperty = someProperty };
        var result = ValidateProperty(model, nameof(model.SomeProperty), someProperty);
        Assert.Equal(expectedValid, result == ValidationResult.Success);
    }

    private class RequiredIfTestListInput
    {
        public string[] TarProperty { get; set; }

        [RequiredIF(nameof(TarProperty), ErrorMessage = "TarProperty为空时，SomeProperty不能为空")]
        public string SomeProperty { get; set; }
    }

    #endregion 特殊类型支持：long、string、IList 等

    #region 边界情况：属性不存在、null比较、可比较类型（IComparable）

    [Fact]
    public void RequiredIF_WhenTargetPropertyNotFound_ShouldReturnError()
    {
        var model = new RequiredIfTestInvalidProperty { SomeProperty = null };
        var result = ValidateProperty(model, nameof(model.SomeProperty), null);
        Assert.IsType<ValidationResult>(result);
        Assert.Contains("找不到属性", result.ErrorMessage);
    }

    private class RequiredIfTestInvalidProperty
    {
        [RequiredIF("NonExistentProperty", "test")]
        public string SomeProperty { get; set; }
    }

    [Theory]
    [InlineData(null, null, Operator.Equal, false)]
    [InlineData(null, "a", Operator.Equal, false)]
    [InlineData("a", null, Operator.NotEqual, true)]
    [InlineData(null, null, Operator.NotEqual, true)]
    public void RequiredIF_NullComparison_ShouldWork(object source, object target, Operator op, bool expectedSuccess)
    {
        var model = new RequiredIfTestNullInput { Source = source, TargetValue = target };
        var attr = new RequiredIFAttribute(nameof(RequiredIfTestNullInput.TargetValue), target, op);
        var result = attr.GetValidationResult(source, new ValidationContext(model) { MemberName = nameof(model.Source) });
        Assert.Equal(expectedSuccess, result == ValidationResult.Success);
    }

    private class RequiredIfTestNullInput
    {
        public object Source { get; set; }
        public object TargetValue { get; set; }
    }

    [Theory]
    [InlineData(null, 4, Operator.GreaterThan, false)] // [RequiredIF(nameof(TargetValue), 3, Operator.GreaterThan))]
    [InlineData(1, 4, Operator.GreaterThan, true)] // [RequiredIF(nameof(TargetValue), 3, Operator.GreaterThan))]
    [InlineData(null, 3, Operator.GreaterThanOrEqual, false)] // [RequiredIF(nameof(TargetValue), 3, Operator.GreaterThanOrEqual))]
    [InlineData(null, 2, Operator.LessThan, false)] // [RequiredIF(nameof(TargetValue), 3, Operator.LessThan))]
    public void RequiredIF_ComparableTypes_ShouldSupportComparison(int? source, int target, Operator op, bool expectedSuccess)
    {
        var model = new RequiredIfTestComparable { Source = source, TargetValue = target };
        var attr = new RequiredIFAttribute(nameof(RequiredIfTestComparable.TargetValue), 3, op);
        var result = attr.GetValidationResult(source, new ValidationContext(model) { MemberName = nameof(model.Source) });
        Assert.Equal(expectedSuccess, result == ValidationResult.Success);
    }

    private class RequiredIfTestComparable
    {
        public int? Source { get; set; }
        public int TargetValue { get; set; }
    }

    #endregion 边界情况：属性不存在、null比较、可比较类型（IComparable）
}