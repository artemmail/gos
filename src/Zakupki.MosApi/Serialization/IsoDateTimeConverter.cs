using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SystemDateTime = global::System.DateTime;

namespace Zakupki.MosApi.Serialization;

public class IsoDateTimeConverter : JsonConverter<SystemDateTime>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss";

    public override SystemDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null
            ? default
            : SystemDateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, SystemDateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}

public class IsoDateTimeNullableConverter : JsonConverter<SystemDateTime?>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss";

    public override SystemDateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrEmpty(value)
            ? null
            : SystemDateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, SystemDateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString(Format, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
