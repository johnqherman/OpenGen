using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGen;

internal class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return "";
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var l)) return l.ToString();
            if (reader.TryGetDouble(out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return reader.GetString() ?? "";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
