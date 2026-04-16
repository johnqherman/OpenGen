using System.Text.Json.Serialization;

namespace OpenGen;

public class ComboPart
{
    [JsonPropertyName("item_id")]          public int    ItemId          { get; set; }
    [JsonPropertyName("skin_id")]          public int    SkinId          { get; set; }
    [JsonPropertyName("pattern_id")]       public int    PatternId       { get; set; }
    [JsonPropertyName("float_value")]      public float  FloatValue      { get; set; }

    [JsonPropertyName("sticker1_slot")]    public int    Sticker1Slot    { get; set; }
    [JsonPropertyName("sticker1_id")]      public int    Sticker1Id      { get; set; }
    [JsonPropertyName("sticker1_value")]   public float  Sticker1Value   { get; set; }
    [JsonPropertyName("sticker1_x")]       public float  Sticker1X       { get; set; }
    [JsonPropertyName("sticker1_y")]       public float  Sticker1Y       { get; set; }
    [JsonPropertyName("sticker1_r")]       public float  Sticker1R       { get; set; }

    [JsonPropertyName("sticker2_slot")]    public int    Sticker2Slot    { get; set; }
    [JsonPropertyName("sticker2_id")]      public int    Sticker2Id      { get; set; }
    [JsonPropertyName("sticker2_value")]   public float  Sticker2Value   { get; set; }
    [JsonPropertyName("sticker2_x")]       public float  Sticker2X       { get; set; }
    [JsonPropertyName("sticker2_y")]       public float  Sticker2Y       { get; set; }
    [JsonPropertyName("sticker2_r")]       public float  Sticker2R       { get; set; }

    [JsonPropertyName("sticker3_slot")]    public int    Sticker3Slot    { get; set; }
    [JsonPropertyName("sticker3_id")]      public int    Sticker3Id      { get; set; }
    [JsonPropertyName("sticker3_value")]   public float  Sticker3Value   { get; set; }
    [JsonPropertyName("sticker3_x")]       public float  Sticker3X       { get; set; }
    [JsonPropertyName("sticker3_y")]       public float  Sticker3Y       { get; set; }
    [JsonPropertyName("sticker3_r")]       public float  Sticker3R       { get; set; }

    [JsonPropertyName("sticker4_slot")]    public int    Sticker4Slot    { get; set; }
    [JsonPropertyName("sticker4_id")]      public int    Sticker4Id      { get; set; }
    [JsonPropertyName("sticker4_value")]   public float  Sticker4Value   { get; set; }
    [JsonPropertyName("sticker4_x")]       public float  Sticker4X       { get; set; }
    [JsonPropertyName("sticker4_y")]       public float  Sticker4Y       { get; set; }
    [JsonPropertyName("sticker4_r")]       public float  Sticker4R       { get; set; }

    [JsonPropertyName("sticker5_slot")]    public int    Sticker5Slot    { get; set; }
    [JsonPropertyName("sticker5_id")]      public int    Sticker5Id      { get; set; }
    [JsonPropertyName("sticker5_value")]   public float  Sticker5Value   { get; set; }
    [JsonPropertyName("sticker5_x")]       public float  Sticker5X       { get; set; }
    [JsonPropertyName("sticker5_y")]       public float  Sticker5Y       { get; set; }
    [JsonPropertyName("sticker5_r")]       public float  Sticker5R       { get; set; }

    [JsonPropertyName("kc_id")]            public int    KcId            { get; set; }
    [JsonPropertyName("kc_pattern_id")]    public int    KcPatternId     { get; set; }
    [JsonPropertyName("kc_x")]             public float  KcX             { get; set; }
    [JsonPropertyName("kc_y")]             public float  KcY             { get; set; }
    [JsonPropertyName("kc_z")]             public float  KcZ             { get; set; }

    [JsonPropertyName("stattrak_enabled")] public int    StatTrakEnabled { get; set; }
    [JsonPropertyName("stattrak_value")]   public int    StatTrakValue   { get; set; }
    [JsonPropertyName("nametag_value")]    public string NameTag         { get; set; } = "";
}
