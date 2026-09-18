using Newtonsoft.Json;

namespace Admin.NET.Core.Extension;

public static class JsonExtensions
{
    /// <summary>
    /// 对象转化为JSON字符串
    /// </summary>
    public static string ToJson(this object obj, Formatting formatting = Formatting.None)
    {
        try
        {
            return JsonConvert.SerializeObject(obj, formatting);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 使用自定义设置将对象转换为JSON字符串
    /// </summary>
    public static string ToJson(this object obj, JsonSerializerSettings settings)
    {
        try
        {
            return JsonConvert.SerializeObject(obj, settings);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 尝试将JSON解析为JArray
    /// </summary>
    public static bool TryParseJArray(this string json, out JArray jsonArray)
    {
        jsonArray = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            jsonArray = JArray.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从JToken深度获取嵌套属性值
    /// </summary>
    public static T GetNestedValueOrDefault<T>(this JToken token, string path, T defaultValue = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return defaultValue;

        try
        {
            JToken result = token.SelectToken(path);
            return result != null ? result.Value<T>() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 尝试将JSON解析为JObject
    /// </summary>
    public static bool TryParseJObject(this string json, out JObject jsonObject)
    {
        jsonObject = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            jsonObject = JObject.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将JSON解析为动态对象
    /// </summary>
    public static bool TryParseDynamic(this string json, out dynamic jsonObject)
    {
        jsonObject = null;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            jsonObject = JsonConvert.DeserializeObject<dynamic>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试将JSON文本解析为指定的对象类型
    /// </summary>
    public static bool TryParseJson<T>(this string json, out T obj)
    {
        obj = default;

        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            obj = JsonConvert.DeserializeObject<T>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 从JObject快速获取属性值，失败则返回默认值
    /// </summary>
    public static T GetValueOrDefault<T>(this JObject jObject, string propertyName, T defaultValue = default)
    {
        try
        {
            if (jObject.TryGetValue(propertyName, out JToken valueToken))
            {
                return valueToken.Value<T>();
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}
