namespace OpenGen;

internal record struct ParsedInspectData(
    uint   DefIndex,
    uint   PaintIndex,
    uint   PaintSeed,
    float  Wear,
    uint   Quality,
    uint   KillEaterValue,
    string CustomName,
    DecodedSubItem[] Stickers,
    DecodedSubItem[] Keychains
);

internal record struct DecodedSubItem(
    uint  Slot,
    uint  Id,
    float Wear,
    float OffsetX,
    float OffsetY,
    float OffsetZ,
    float Rotation,
    uint  Pattern
);
