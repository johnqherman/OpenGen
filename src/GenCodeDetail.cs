using System.Text.Json.Serialization;

namespace OpenGen;

public class GenCodeDetail
{
    [JsonPropertyName("Item_ID")]          public string ItemId          { get; set; } = "";
    [JsonPropertyName("Skin_ID")]          public string SkinId          { get; set; } = "";
    [JsonPropertyName("Pattern_ID")]       public string PatternId       { get; set; } = "";
    [JsonPropertyName("Float_Value")]      public string FloatValue      { get; set; } = "0";
    [JsonPropertyName("Sticker1_Slot")]    public int    Sticker1Slot    { get; set; }
    [JsonPropertyName("Sticker1_ID")]      public int    Sticker1Id      { get; set; }
    [JsonPropertyName("Sticker1_Value")]   public float  Sticker1Value   { get; set; }
    [JsonPropertyName("Sticker1_X")]       public float  Sticker1X       { get; set; }
    [JsonPropertyName("Sticker1_Y")]       public float  Sticker1Y       { get; set; }
    [JsonPropertyName("Sticker1_R")]       public float  Sticker1R       { get; set; }
    [JsonPropertyName("Sticker2_Slot")]    public int    Sticker2Slot    { get; set; }
    [JsonPropertyName("Sticker2_ID")]      public int    Sticker2Id      { get; set; }
    [JsonPropertyName("Sticker2_Value")]   public float  Sticker2Value   { get; set; }
    [JsonPropertyName("Sticker2_X")]       public float  Sticker2X       { get; set; }
    [JsonPropertyName("Sticker2_Y")]       public float  Sticker2Y       { get; set; }
    [JsonPropertyName("Sticker2_R")]       public float  Sticker2R       { get; set; }
    [JsonPropertyName("Sticker3_Slot")]    public int    Sticker3Slot    { get; set; }
    [JsonPropertyName("Sticker3_ID")]      public int    Sticker3Id      { get; set; }
    [JsonPropertyName("Sticker3_Value")]   public float  Sticker3Value   { get; set; }
    [JsonPropertyName("Sticker3_X")]       public float  Sticker3X       { get; set; }
    [JsonPropertyName("Sticker3_Y")]       public float  Sticker3Y       { get; set; }
    [JsonPropertyName("Sticker3_R")]       public float  Sticker3R       { get; set; }
    [JsonPropertyName("Sticker4_Slot")]    public int    Sticker4Slot    { get; set; }
    [JsonPropertyName("Sticker4_ID")]      public int    Sticker4Id      { get; set; }
    [JsonPropertyName("Sticker4_Value")]   public float  Sticker4Value   { get; set; }
    [JsonPropertyName("Sticker4_X")]       public float  Sticker4X       { get; set; }
    [JsonPropertyName("Sticker4_Y")]       public float  Sticker4Y       { get; set; }
    [JsonPropertyName("Sticker4_R")]       public float  Sticker4R       { get; set; }
    [JsonPropertyName("Sticker5_Slot")]    public int    Sticker5Slot    { get; set; }
    [JsonPropertyName("Sticker5_ID")]      public int    Sticker5Id      { get; set; }
    [JsonPropertyName("Sticker5_Value")]   public float  Sticker5Value   { get; set; }
    [JsonPropertyName("Sticker5_X")]       public float  Sticker5X       { get; set; }
    [JsonPropertyName("Sticker5_Y")]       public float  Sticker5Y       { get; set; }
    [JsonPropertyName("Sticker5_R")]       public float  Sticker5R       { get; set; }
    [JsonPropertyName("KeyChain_ID")]      public int    KeyChainId      { get; set; }
    [JsonPropertyName("KeyChain_Pattern")] public int    KeyChainPattern { get; set; }
    [JsonPropertyName("KeyChain_X")]       public float  KeyChainX       { get; set; }
    [JsonPropertyName("KeyChain_Y")]       public float  KeyChainY       { get; set; }
    [JsonPropertyName("KeyChain_Z")]       public float  KeyChainZ       { get; set; }
    [JsonPropertyName("Stattrak_enabled")] public string StatTrakEnabled { get; set; } = "0";
    [JsonPropertyName("Stattrak_value")]   public int    StatTrakValue   { get; set; }
    [JsonPropertyName("Nametag_value")]    public string NameTag         { get; set; } = "";
}
