using System.Text.Json.Serialization;

namespace OpenGen;

internal class GenCodeApiResponse
{
    [JsonPropertyName("genCodeDetail")]
    public GenCodeDetail? GenCodeDetail { get; set; }

    [JsonPropertyName("comboParts")]
    public List<ComboPart>? ComboParts { get; set; }
}
