namespace OpenGen;

internal record PendingSkin(
    string ClassName, int PaintKit, int Seed, float Wear,
    (int Slot, int Id, float Wear, float X, float Y, float R)[] Stickers,
    ushort DefIndex = 0,
    int CharmId = 0, int CharmSeed = 0, float CharmX = 0f, float CharmY = 0f, float CharmZ = 0f,
    bool StatTrakEnabled = false, int StatTrakValue = 0,
    string NameTag = "");
