using System.Text.Json;
using System.Text.Json.Serialization;

namespace Admin.NET.Core;

public class JsonConverterUtil
{
    public class DateTimeNullConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => string.IsNullOrEmpty(reader.GetString()) ? default : ParseDateTime(reader.GetString());

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value?.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateTime = ParseDateTime(reader.GetString());
            return dateTime == null ? DateTime.MinValue : dateTime.Value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    public static DateTime? ParseDateTime(string dateStr)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(dateStr, @"^\d{4}[/-]") && DateTime.TryParse(dateStr, null,
                System.Globalization.DateTimeStyles.AssumeLocal, out var dateVal))
            return dateVal;
        return null;
    }

    public class LongConverter : System.Text.Json.Serialization.JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var v = reader.GetString();
            if (string.IsNullOrEmpty(v))
            {
                return 0;
            }
            else
            {
                if (long.TryParse(v, out long result))
                {
                    return result;
                }
                else
                {
                    return 0;
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class LongNullConverter : System.Text.Json.Serialization.JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {

            var v = reader.GetString();
            if (string.IsNullOrEmpty(v))
            {
                return 0;
            }
            else
            {
                if (long.TryParse(v, out long result))
                {
                    return result;
                }
                else
                {
                    return 0;
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteStringValue(string.Empty);
            }
            else
            {
                writer.WriteStringValue(value.Value.ToString());
            }
        }
    }
}
