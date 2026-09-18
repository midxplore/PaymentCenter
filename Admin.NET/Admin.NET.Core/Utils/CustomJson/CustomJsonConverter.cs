// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Admin.NET.Core;

/// <summary>
/// 自定义JSON转换器
/// </summary>
public class CustomJsonConverter : JsonConverterFactory
{
    /// <summary>
    /// 判断是否可以转换指定类型
    /// </summary>
    /// <param name="typeToConvert">要转换的类型</param>
    /// <returns>是否可以转换</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.GetProperties()
            .Any(p => p.GetCustomAttribute<CustomJsonPropertyAttribute>() != null);
    }

    /// <summary>
    /// 创建指定类型的转换器实例
    /// </summary>
    /// <param name="type">要转换的类型</param>
    /// <param name="options">JSON序列化选项</param>
    /// <returns>JSON转换器实例</returns>
    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
    {
        var converterType = typeof(CustomConverter<>).MakeGenericType(type);
        return (JsonConverter)Activator.CreateInstance(converterType, options);
    }

    /// <summary>
    /// 自定义转换器实现类
    /// </summary>
    /// <typeparam name="T">要转换的类型</typeparam>
    private class CustomConverter<T> : JsonConverter<T>
    {
        /// <summary>
        /// 基础JSON序列化选项
        /// </summary>
        private readonly JsonSerializerOptions _baseOptions;

        /// <summary>
        /// JSON属性名到对象属性的映射字典
        /// </summary>
        private readonly Dictionary<string, PropertyInfo> _jsonToPropertyMap;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseOptions">基础JSON序列化选项</param>
        public CustomConverter(JsonSerializerOptions baseOptions)
        {
            _baseOptions = new JsonSerializerOptions(baseOptions);
            _jsonToPropertyMap = CreatePropertyMap();
        }

        /// <summary>
        /// 创建属性映射字典
        /// </summary>
        /// <returns>属性映射字典</returns>
        private Dictionary<string, PropertyInfo> CreatePropertyMap()
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in typeof(T).GetProperties())
            {
                var customAttr = prop.GetCustomAttribute<CustomJsonPropertyAttribute>();
                if (customAttr != null)
                {
                    map[customAttr.Name] = prop;
                }
                else
                {
                    var jsonName = _baseOptions.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;
                    map[jsonName] = prop;
                }
            }

            return map;
        }

        /// <summary>
        /// 反序列化JSON为对象
        /// </summary>
        /// <param name="reader">JSON读取器</param>
        /// <param name="typeToConvert">要转换的类型</param>
        /// <param name="options">JSON序列化选项</param>
        /// <returns>反序列化后的对象</returns>
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return default;

            // 创建实例并设置默认值
            var instance = Activator.CreateInstance<T>();
            object GetDefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
            foreach (var prop in typeof(T).GetProperties().Where(p => p.CanWrite))
                prop.SetValue(instance, GetDefaultValue(prop.PropertyType));

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    if (_jsonToPropertyMap.TryGetValue(propertyName!, out var prop))
                    {
                        reader.Read();
                        var value = DeserializeWithCustomFormat(ref reader, prop, _baseOptions);
                        prop.SetValue(instance, value);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            return instance;
        }

        /// <summary>
        /// 序列化对象为JSON
        /// </summary>
        /// <param name="writer">JSON写入器</param>
        /// <param name="value">要序列化的对象</param>
        /// <param name="options">JSON序列化选项</param>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var prop in typeof(T).GetProperties())
            {
                var propValue = prop.GetValue(value);
                if (propValue == null && _baseOptions.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                    continue;

                var customAttr = prop.GetCustomAttribute<CustomJsonPropertyAttribute>();
                var jsonName = customAttr?.Name ?? ToCamelCase(prop.Name);

                writer.WritePropertyName(jsonName);

                // 处理日期格式化
                if (!string.IsNullOrEmpty(customAttr?.DateFormat))
                {
                    if (propValue is DateTime dateTime)
                    {
                        writer.WriteStringValue(dateTime.ToString(customAttr.DateFormat));
                    }
                    else if (prop.PropertyType == typeof(DateTime?) && propValue != null)
                    {
                        var nullableDateTime = (DateTime?)propValue;
                        writer.WriteStringValue(nullableDateTime.Value.ToString(customAttr.DateFormat));
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, propValue, prop.PropertyType, _baseOptions);
                    }
                }
                else
                {
                    JsonSerializer.Serialize(writer, propValue, prop.PropertyType, _baseOptions);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// 转换属性名
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>
        /// 反序列化JSON为对象，处理自定义格式
        /// </summary>
        private object DeserializeWithCustomFormat(ref Utf8JsonReader reader, PropertyInfo prop, JsonSerializerOptions options)
        {
            var customAttr = prop.GetCustomAttribute<CustomJsonPropertyAttribute>();

            // 特殊处理日期类型
            if (!string.IsNullOrEmpty(customAttr?.DateFormat))
            {
                if (prop.PropertyType == typeof(DateTime) && reader.TokenType == JsonTokenType.String)
                {
                    var dateString = reader.GetString();
                    if (DateTime.TryParseExact(dateString, customAttr.DateFormat, null, DateTimeStyles.None, out var dateTime))
                    {
                        return dateTime;
                    }
                }
                else if (prop.PropertyType == typeof(DateTime?) && reader.TokenType == JsonTokenType.String)
                {
                    var dateString = reader.GetString();
                    if (DateTime.TryParseExact(dateString, customAttr.DateFormat, null, DateTimeStyles.None, out var dateTime))
                    {
                        return (DateTime?)dateTime;
                    }
                    return null;
                }
            }

            // 默认处理
            return JsonSerializer.Deserialize(ref reader, prop.PropertyType, options);
        }
    }
}