using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGen;

internal class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)  return 0;
        if (reader.TokenType == JsonTokenType.True)  return 1;
        if (reader.TokenType == JsonTokenType.False) return 0;
        if (reader.TokenType == JsonTokenType.String)
            return int.TryParse(reader.GetString(), out var s) ? s : 0;
        return reader.GetInt32();
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

internal class FlexibleFloatConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return 0f;
        if (reader.TokenType == JsonTokenType.String)
            return float.TryParse(reader.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var s) ? s : 0f;
        return reader.GetSingle();
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

internal class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return "";
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var l)) return l.ToString();
            if (reader.TryGetDouble(out var d)) return d.ToString(CultureInfo.InvariantCulture);
        }
        return reader.GetString() ?? "";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
