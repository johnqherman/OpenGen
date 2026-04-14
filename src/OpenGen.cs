using System.Net.Http;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace OpenGen;

public partial class OpenGen : BasePlugin
{
    public override string ModuleName    => "OpenGen";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor  => "inspect server";

    private static readonly MemoryFunctionVoid<nint, string, float> SetOrAddAttr =
        new(GameData.GetSignature("CAttributeList::SetOrAddAttributeValueByName"));

    private static readonly MemoryFunctionWithReturn<nint, nint> CEconItemViewCtor =
        new(GameData.GetSignature("CEconItemView::CEconItemView"));

    private static readonly int DropWeaponOffset =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 24 : 25;

    internal static void DropWeapon(nint weaponServicesPtr, nint weaponPtr)
    {
        VirtualFunction.CreateVoid<nint, nint, Vector?, Vector?>(weaponServicesPtr, DropWeaponOffset)
            .Invoke(weaponServicesPtr, weaponPtr, null, null);
    }

    private static float UintAsFloat(uint v) => BitConverter.Int32BitsToSingle((int)v);

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly Dictionary<int, bool>  _skinLegacyMap  = new();
    private readonly Dictionary<ulong, PendingSkin>                     _pendingGive    = new();
    private readonly Dictionary<ulong, (ushort DefIndex, PendingSkin Pending)> _equippedGloves = new();
    private readonly Dictionary<ulong, nint> _econItemViews = new();

    private ulong _nextItemId = 65578;

    private record PendingSkin(
        string ClassName, int PaintKit, int Seed, float Wear,
        (int Slot, int Id, float Wear, float X, float Y, float R)[] Stickers,
        ushort DefIndex = 0,
        int CharmId = 0, int CharmSeed = 0, float CharmX = 0f, float CharmY = 0f, float CharmZ = 0f,
        bool StatTrakEnabled = false, int StatTrakValue = 0,
        string NameTag = "");

    public override void Load(bool hotReload)
    {
        _ = LoadSkinLegacyMapAsync();
        _ = LoadAgentMapAsync();
        AddCommand("css_g",   "Give weapon skin by combo ID",    CmdGive);
        AddCommand("css_gen", "Give weapon skin by explicit fields", CmdGiveParsed);
        VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPre,  HookMode.Pre);
        VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPost, HookMode.Post);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawnPost, HookMode.Post);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnectPost, HookMode.Post);
    }

    public override void Unload(bool hotReload)
    {
        VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPre,  HookMode.Pre);
        VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPost, HookMode.Post);
        foreach (var ptr in _econItemViews.Values)
            Marshal.FreeHGlobal(ptr);
        _econItemViews.Clear();
    }

    internal nint GetOrBuildEconItemView(ulong steamId, ushort defIndex,
        int paintKit, int seed, float wear, (int Slot, int Id, float Wear, float X, float Y, float R)[] stickers,
        bool isKnife = false,
        int charmId = 0, int charmSeed = 0, float charmX = 0f, float charmY = 0f, float charmZ = 0f,
        bool statTrakEnabled = false, int statTrakValue = 0,
        string nameTag = "")
    {
        if (!_econItemViews.TryGetValue(steamId, out var ptr))
        {
            ptr = Marshal.AllocHGlobal(Schema.GetClassSize("CEconItemView"));
            CEconItemViewCtor.Invoke(ptr);
            _econItemViews[steamId] = ptr;
        }

        var view = new CEconItemView(ptr);
        view.Initialized            = true;
        view.ItemDefinitionIndex    = defIndex;

        var itemId = _nextItemId++;
        view.ItemID     = itemId;
        view.ItemIDLow  = (uint)(itemId & 0xFFFFFFFF);
        view.ItemIDHigh = (uint)(itemId >> 32);
        view.EntityQuality = isKnife ? 3 : 4;

        var attrs = view.NetworkedDynamicAttributes;
        attrs.Attributes.RemoveAll();
        SetOrAddAttr.Invoke(attrs.Handle, "set item texture prefab", (float)paintKit);
        SetOrAddAttr.Invoke(attrs.Handle, "set item texture seed",   (float)seed);
        SetOrAddAttr.Invoke(attrs.Handle, "set item texture wear",   wear > 0f ? wear : 0.01f);
        foreach (var (slot, id, stickerWear, x, y, r) in stickers)
        {
            if (id == 0) continue;
            SetOrAddAttr.Invoke(attrs.Handle, $"sticker slot {slot} id",       UintAsFloat((uint)id));
            SetOrAddAttr.Invoke(attrs.Handle, $"sticker slot {slot} wear",     stickerWear);
            SetOrAddAttr.Invoke(attrs.Handle, $"sticker slot {slot} offset x", x);
            SetOrAddAttr.Invoke(attrs.Handle, $"sticker slot {slot} offset y", y);
            SetOrAddAttr.Invoke(attrs.Handle, $"sticker slot {slot} rotation", r);
        }
        if (charmId != 0)
        {
            SetOrAddAttr.Invoke(attrs.Handle, "keychain slot 0 id",       UintAsFloat((uint)charmId));
            SetOrAddAttr.Invoke(attrs.Handle, "keychain slot 0 seed",     UintAsFloat((uint)charmSeed));
            SetOrAddAttr.Invoke(attrs.Handle, "keychain slot 0 offset x", charmX);
            SetOrAddAttr.Invoke(attrs.Handle, "keychain slot 0 offset y", charmY);
            SetOrAddAttr.Invoke(attrs.Handle, "keychain slot 0 offset z", charmZ);
        }
        if (statTrakEnabled)
        {
            SetOrAddAttr.Invoke(attrs.Handle, "kill eater",            (float)statTrakValue);
            SetOrAddAttr.Invoke(attrs.Handle, "kill eater score type", 0f);
        }
        if (!string.IsNullOrEmpty(nameTag))
        {
            var offset   = Schema.GetSchemaOffset("CEconItemView", "m_szCustomName");
            var namePtr  = ptr + offset;
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(nameTag);
            var len = Math.Min(nameBytes.Length, 127);
            Marshal.Copy(nameBytes, 0, namePtr, len);
            Marshal.WriteByte(namePtr, len, 0);
        }

        return ptr;
    }

    internal void FreeEconItemView(ulong steamId)
    {
        if (_econItemViews.Remove(steamId, out var ptr))
            Marshal.FreeHGlobal(ptr);
    }
}
