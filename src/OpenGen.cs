using System.Net.Http;
using System.Text.Json;
using CounterStrikeSharp.API.Core;

namespace OpenGen;

public partial class OpenGen : BasePlugin
{
    public override string ModuleName    => "OpenGen";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor  => "inspect server";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Converters = { new NullToZeroIntConverter() }
    };

    private readonly Dictionary<int, bool>  _skinLegacyMap  = new();
    private readonly Dictionary<ulong, PendingSkin>                          _pendingGive    = new();
    private readonly Dictionary<ulong, (ushort DefIndex, PendingSkin Pending)> _equippedGloves = new();
    private readonly Dictionary<ulong, nint> _econItemViews  = new();
    private readonly Dictionary<ulong, Dictionary<int, (float Wear, string StickerFp)>> _stickerWearCache = new();

    private ulong _nextItemId = 65578;

    public override void Load(bool hotReload)
    {
        _ = LoadSkinLegacyMapAsync();
        _ = LoadAgentMapAsync();
        AddCommand("css_g",     "Apply weapon skin from gencode",       CmdGen);
        AddCommand("css_gen",   "Apply weapon skin from parsed fields", CmdGenParsed);
        AddCommand("css_combo", "Apply full combo set from gencode",    CmdCombo);
        RegisterGiveHooks();
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawnPost, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnectPost, HookMode.Post);
    }

    public override void Unload(bool hotReload)
    {
        UnregisterGiveHooks();
        FreeAllEconItemViews();
    }
}
