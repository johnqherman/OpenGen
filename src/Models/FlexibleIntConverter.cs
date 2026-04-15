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
