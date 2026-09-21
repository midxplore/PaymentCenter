// Admin.NET 项目的版权、商标、专利和其他相关权利均受相应法律法规的保护。使用本项目应遵守相关法律法规和许可证的要求。
//
// 本项目主要遵循 MIT 许可证和 Apache 许可证（版本 2.0）进行分发和使用。许可证位于源代码树根目录中的 LICENSE-MIT 和 LICENSE-APACHE 文件。
//
// 不得利用本项目从事危害国家安全、扰乱社会秩序、侵犯他人合法权益等法律法规禁止的活动！任何基于本项目二次开发而产生的一切法律纠纷和责任，我们不承担任何责任！

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace Admin.NET.Application;

/// <summary>
/// 对外金额写出为固定两位小数的字符串，避免接入方用双精度解析时静默失真。读入仍接受数字。只挂对外 DTO。
/// </summary>
public class AmountStringConverter : JsonConverter<decimal>
{
    private static readonly string FixedFormat = "0." + new string('0', PayConst.AmountScale);

    /// <inheritdoc />
    public override decimal ReadJson(JsonReader reader, Type objectType, decimal existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            var text = (reader.Value as string)?.Trim();
            if (string.IsNullOrEmpty(text))
                throw new JsonSerializationException("金额不能为空字符串");

            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                throw new JsonSerializationException($"金额不是合法的十进制数字：{text}");

            return parsed;
        }

        // 兼容历史调用方：数字形式照收（服务端从 JSON 原文精确解析，无浮点损失）
        if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
            return Convert.ToDecimal(reader.Value, CultureInfo.InvariantCulture);

        throw new JsonSerializationException($"金额必须是十进制字符串（如 \"1.20\"），实际收到 {reader.TokenType}");
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer)
        => writer.WriteValue(value.ToString(FixedFormat, CultureInfo.InvariantCulture));
}

/// <summary>
/// 对外枚举写出名称。读入仍接受数字。只挂对外 DTO。
/// </summary>
public class EnumNameConverter : StringEnumConverter
{
}
