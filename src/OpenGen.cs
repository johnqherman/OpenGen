using System.Net.Http;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace OpenGen;

public partial class OpenGen : BasePlugin
{
    public override string ModuleName    => "OpenGen";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor  => "inspect server";

    private static readonly MemoryFunctionVoid<nint, string, float> SetOrAddAttr =
        new(GameData.GetSignature("CAttributeList::SetOrAddAttributeValueByName"));

    private static float UintAsFloat(uint v) => BitConverter.Int32BitsToSingle((int)v);

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly Dictionary<int, bool> _skinLegacyMap = new();
    private readonly Dictionary<ulong, PendingSkin> _pendingGive = new();
    private ulong _nextItemId = 65578;

    private record PendingSkin(
        string ClassName, int PaintKit, int Seed, float Wear,
        (int Slot, int Id, float Wear)[] Stickers);

    public override void Load(bool hotReload)
    {
        _ = LoadSkinLegacyMapAsync();
        _ = LoadAgentMapAsync();
        AddCommand("css_g", "Give weapon from cs2inspects.com gencode", CmdGive);
        VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPost, HookMode.Post);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawnPost, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnectPost, HookMode.Post);
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPost, HookMode.Post);
    }
}
