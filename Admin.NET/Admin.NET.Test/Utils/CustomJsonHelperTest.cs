// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Admin.NET.Test.Utils;

public class CustomJsonHelperTest
{
    // 测试基本数据类型序列化
    [Fact]
    public void Serialize_BasicTypes_ShouldWork()
    {
        var obj = new BasicTypesModel
        {
            IntValue = 42,
            StringValue = "Hello",
            BoolValue = true,
            DoubleValue = 3.14,
            DecimalValue = 123.45m
        };

        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<BasicTypesModel>(json);

        Assert.Equal(obj.IntValue, deserialized.IntValue);
        Assert.Equal(obj.StringValue, deserialized.StringValue);
        Assert.Equal(obj.BoolValue, deserialized.BoolValue);
        Assert.Equal(obj.DoubleValue, deserialized.DoubleValue);
        Assert.Equal(obj.DecimalValue, deserialized.DecimalValue);
    }

    // 测试可空类型
    [Fact]
    public void Serialize_NullableTypes_ShouldWork()
    {
        var obj = new NullableTypesModel
        {
            NullableInt = null,
            NullableDateTime = DateTime.Now,
            NullableBool = true
        };

        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<NullableTypesModel>(json);

        Assert.Null(deserialized.NullableInt);
        Assert.Equal(obj.NullableDateTime, deserialized.NullableDateTime);
        Assert.True(deserialized.NullableBool);
    }

    // 测试自定义属性名和日期格式
    [Fact]
    public void Serialize_CustomJsonProperty_ShouldUseCustomNameAndDateFormat()
    {
        var obj = new CustomPropertyModel
        {
            NormalProperty = "Normal",
            CustomNameProperty = "Custom",
            DateTimeProperty = new DateTime(2023, 1, 1, 12, 0, 0)
        };

        var json = CustomJsonHelper.Serialize(obj);

        // 验证自定义属性名和日期格式
        Assert.Contains("\"custom_name\"", json);
        Assert.Contains("\"2023-01-01 12:00:00\"", json);

        var deserialized = CustomJsonHelper.Deserialize<CustomPropertyModel>(json);
        Assert.Equal(obj.NormalProperty, deserialized.NormalProperty);
        Assert.Equal(obj.CustomNameProperty, deserialized.CustomNameProperty);
        Assert.Equal(obj.DateTimeProperty, deserialized.DateTimeProperty);
    }

    // 测试嵌套对象
    [Fact]
    public void Serialize_NestedObjects_ShouldWork()
    {
        var obj = new NestedModel
        {
            Id = 1,
            Inner = new BasicTypesModel
            {
                IntValue = 100,
                StringValue = "Inner"
            }
        };

        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<NestedModel>(json);

        Assert.Equal(obj.Id, deserialized.Id);
        Assert.Equal(obj.Inner.IntValue, deserialized.Inner.IntValue);
        Assert.Equal(obj.Inner.StringValue, deserialized.Inner.StringValue);
    }

    // 测试循环引用（通过引用处理）
    [Fact]
    public void Serialize_CircularReference_ShouldHandleGracefully()
    {
        var parent = new ParentModel { Name = "Parent" };
        var child = new ChildModel { Name = "Child", Parent = parent };
        parent.Child = child;

        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        var json = CustomJsonHelper.Serialize(parent);
        var deserialized = CustomJsonHelper.Deserialize<ParentModel>(json, options);

        Assert.Equal(parent.Name, deserialized.Name);
        Assert.Equal(parent.Child.Name, deserialized.Child.Name);
        // 循环引用部分会被忽略，所以 Parent.Child.Parent 会是 null
        Assert.Null(deserialized.Child.Parent);
    }

    // 测试忽略序列化特性
    [Fact]
    public void Serialize_JsonIgnoreAttribute_ShouldIgnoreProperty()
    {
        var obj = new IgnorePropertyModel
        {
            SerializedProperty = "Serialized",
            IgnoredProperty = "Ignored"
        };

        var json = CustomJsonHelper.Serialize(obj);

        Assert.Contains("\"SerializedProperty\"", json);
        Assert.DoesNotContain("\"ignoredProperty\"", json);

        var deserialized = CustomJsonHelper.Deserialize<IgnorePropertyModel>(json);
        Assert.Equal(obj.SerializedProperty, deserialized.SerializedProperty);
        Assert.Null(deserialized.IgnoredProperty);
    }

    // 测试枚举类型
    [Fact]
    public void Serialize_EnumTypes_ShouldWork()
    {
        var obj = new EnumModel
        {
            Status = Status.Active,
            NullableStatus = Status.Inactive
        };

        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<EnumModel>(json);

        Assert.Equal(obj.Status, deserialized.Status);
        Assert.Equal(obj.NullableStatus, deserialized.NullableStatus);
    }

    // 测试空对象
    [Fact]
    public void Serialize_EmptyObject_ShouldWork()
    {
        var obj = new EmptyModel();
        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<EmptyModel>(json);

        Assert.NotNull(deserialized);
    }

    // 测试数组和集合
    [Fact]
    public void Serialize_ArraysAndCollections_ShouldWork()
    {
        var obj = new CollectionModel
        {
            IntArray = [1, 2, 3],
            StringList = ["a", "b", "c"]
        };

        var json = CustomJsonHelper.Serialize(obj);
        var deserialized = CustomJsonHelper.Deserialize<CollectionModel>(json);

        Assert.Equal(obj.IntArray, deserialized.IntArray);
        Assert.Equal(obj.StringList, deserialized.StringList);
    }

    // 测试模型类
    private class BasicTypesModel
    {
        public int IntValue { get; init; }
        public string StringValue { get; init; }
        public bool BoolValue { get; init; }
        public double DoubleValue { get; init; }
        public decimal DecimalValue { get; init; }
    }

    private class NullableTypesModel
    {
        public int? NullableInt { get; init; }
        public DateTime? NullableDateTime { get; init; }
        public bool? NullableBool { get; init; }
    }

    private class CustomPropertyModel
    {
        public string NormalProperty { get; init; }

        [CustomJsonProperty("custom_name")] public string CustomNameProperty { get; init; }

        [CustomJsonProperty("date_time", DateFormat = "yyyy-MM-dd HH:mm:ss")]
        public DateTime DateTimeProperty { get; init; }
    }

    private class NestedModel
    {
        public int Id { get; init; }
        public BasicTypesModel Inner { get; init; }
    }

    public class ParentModel
    {
        public string Name { get; init; }
        public ChildModel Child { get; set; }
    }

    public class ChildModel
    {
        public string Name { get; init; }
        public ParentModel Parent { get; init; }
    }

    private class IgnorePropertyModel
    {
        public string SerializedProperty { get; init; }

        [JsonIgnore] public string IgnoredProperty { get; init; }
    }

    private enum Status
    {
        Active,
        Inactive
    }

    private class EnumModel
    {
        public Status Status { get; init; }
        public Status? NullableStatus { get; init; }
    }

    private class EmptyModel
    {
    }

    private class CollectionModel
    {
        public int[] IntArray { get; init; }
        public List<string> StringList { get; init; }
    }
}