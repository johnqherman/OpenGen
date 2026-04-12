using System.Text.Json.Serialization;

namespace OpenGen;

public class GenCodeDetail
{
    [JsonPropertyName("Item_ID")]        public string ItemId        { get; set; } = "";
    [JsonPropertyName("Item_Name")]      public string ItemName      { get; set; } = "";
    [JsonPropertyName("Skin_ID")]        public string SkinId        { get; set; } = "";
    [JsonPropertyName("Pattern_ID")]     public string PatternId     { get; set; } = "";
    [JsonPropertyName("Float_Value")]    public string FloatValue    { get; set; } = "0";
    [JsonPropertyName("Sticker1_Slot")]  public int    Sticker1Slot  { get; set; }
    [JsonPropertyName("Sticker1_ID")]    public int    Sticker1Id    { get; set; }
    [JsonPropertyName("Sticker1_Value")] public float  Sticker1Value { get; set; }
    [JsonPropertyName("Sticker2_Slot")]  public int    Sticker2Slot  { get; set; }
    [JsonPropertyName("Sticker2_ID")]    public int    Sticker2Id    { get; set; }
    [JsonPropertyName("Sticker2_Value")] public float  Sticker2Value { get; set; }
    [JsonPropertyName("Sticker3_Slot")]  public int    Sticker3Slot  { get; set; }
    [JsonPropertyName("Sticker3_ID")]    public int    Sticker3Id    { get; set; }
    [JsonPropertyName("Sticker3_Value")] public float  Sticker3Value { get; set; }
    [JsonPropertyName("Sticker4_Slot")]  public int    Sticker4Slot  { get; set; }
    [JsonPropertyName("Sticker4_ID")]    public int    Sticker4Id    { get; set; }
    [JsonPropertyName("Sticker4_Value")] public float  Sticker4Value { get; set; }
    [JsonPropertyName("Sticker5_Slot")]  public int    Sticker5Slot  { get; set; }
    [JsonPropertyName("Sticker5_ID")]    public int    Sticker5Id    { get; set; }
    [JsonPropertyName("Sticker5_Value")] public float  Sticker5Value { get; set; }
}
