using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;

namespace OpenGen;

public partial class OpenGen
{
    private static readonly MemoryFunctionVoid<nint, string, float> SetOrAddAttr =
        new(GameData.GetSignature("CAttributeList::SetOrAddAttributeValueByName"));

    private static readonly MemoryFunctionWithReturn<nint, nint> CEconItemViewCtor =
        new(GameData.GetSignature("CEconItemView::CEconItemView"));

    internal static void DropWeapon(nint weaponServicesPtr, nint weaponPtr)
    {
        VirtualFunction.CreateVoid<nint, nint, Vector?, Vector?>(
            weaponServicesPtr,
            GameData.GetOffset("CCSPlayer_WeaponServices::DropWeapon")
        )(weaponServicesPtr, weaponPtr, null, null);
    }

    private static float UintAsFloat(int v) => BitConverter.Int32BitsToSingle(v);

    private void WriteAttributes(nint handle, PendingSkin pending)
    {
        SetOrAddAttr.Invoke(handle, "set item texture prefab", (float)pending.PaintKit);
        SetOrAddAttr.Invoke(handle, "set item texture seed",   (float)pending.Seed);
        SetOrAddAttr.Invoke(handle, "set item texture wear",   pending.Wear > 0f ? pending.Wear : 0.01f);
        foreach (var (slot, id, stickerWear, x, y, r) in pending.Stickers)
        {
            if (id == 0) continue;
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} id",       UintAsFloat(id));
            if (slot == 4) SetOrAddAttr.Invoke(handle, $"sticker slot {slot} schema",   0f);
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} wear",     stickerWear);
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} offset x", x);
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} offset y", y);
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} rotation", r);
            SetOrAddAttr.Invoke(handle, $"sticker slot {slot} scale",    1f);
        }
        if (pending.CharmId != 0)
        {
            SetOrAddAttr.Invoke(handle, "keychain slot 0 id",       UintAsFloat(pending.CharmId));
            SetOrAddAttr.Invoke(handle, "keychain slot 0 seed",     UintAsFloat(pending.CharmSeed));
            SetOrAddAttr.Invoke(handle, "keychain slot 0 offset x", pending.CharmX);
            SetOrAddAttr.Invoke(handle, "keychain slot 0 offset y", pending.CharmY);
            SetOrAddAttr.Invoke(handle, "keychain slot 0 offset z", pending.CharmZ);
            if (pending.CharmSeed != 0)
                SetOrAddAttr.Invoke(handle, "keychain slot 0 sticker", UintAsFloat(pending.CharmSeed));
        }
        if (pending.StatTrakEnabled)
        {
            SetOrAddAttr.Invoke(handle, "kill eater",            (float)pending.StatTrakValue);
            SetOrAddAttr.Invoke(handle, "kill eater score type", 0f);
        }
    }

    private nint GetOrBuildEconItemView(ulong steamId, PendingSkin pending)
    {
        if (_econItemViews.TryGetValue(steamId, out var old))
            Marshal.FreeHGlobal(old);

        var ptr = Marshal.AllocHGlobal(Schema.GetClassSize("CEconItemView"));
        CEconItemViewCtor.Invoke(ptr);
        _econItemViews[steamId] = ptr;

        var isKnife = pending.ClassName.Contains("knife");
        var view    = new CEconItemView(ptr);

        view.Initialized         = true;
        view.ItemDefinitionIndex = pending.DefIndex;

        var itemId = _nextItemId++;
        view.ItemID     = itemId;
        view.ItemIDLow  = (uint)(itemId & 0xFFFFFFFF);
        view.ItemIDHigh = (uint)(itemId >> 32);

        view.EntityQuality = isKnife ? 3 : 4;

        var attrs = view.NetworkedDynamicAttributes;
        attrs.Attributes.RemoveAll();
        WriteAttributes(attrs.Handle, pending);

        var nameTagOffset = Schema.GetSchemaOffset("CEconItemView", "m_szCustomName");
        var nameTagPtr    = ptr + nameTagOffset;
        if (!string.IsNullOrEmpty(pending.NameTag))
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(pending.NameTag);
            var len = Math.Min(nameBytes.Length, 127);
            Marshal.Copy(nameBytes, 0, nameTagPtr, len);
            Marshal.WriteByte(nameTagPtr, len, 0);
        }
        else
        {
            Marshal.WriteByte(nameTagPtr, 0, 0);
        }

        return ptr;
    }

    internal float GetBumpedWear(ulong steamId, int paintKit, float wear,
        StickerSlot[] stickers)
    {
        var baseWear = wear > 0f ? wear : 0.01f;

        if (!stickers.Any(s => s.Id != 0)) return baseWear;

        var fp = string.Join("|", stickers.Where(s => s.Id != 0).Select(s => $"{s.Slot}:{s.Id}"));

        if (!_stickerWearCache.TryGetValue(steamId, out var pkMap))
            _stickerWearCache[steamId] = pkMap = new();

        if (pkMap.TryGetValue(paintKit, out var cached) && cached.StickerFp != fp)
            baseWear = Math.Min(cached.Wear + 0.001f, 1.0f);

        pkMap[paintKit] = (baseWear, fp);
        return baseWear;
    }

    internal void FreeEconItemView(ulong steamId)
    {
        if (_econItemViews.Remove(steamId, out var ptr))
            Marshal.FreeHGlobal(ptr);
    }

    internal void FreeAllEconItemViews()
    {
        foreach (var ptr in _econItemViews.Values)
            Marshal.FreeHGlobal(ptr);
        _econItemViews.Clear();
    }

    internal void RegisterGiveHooks()
    {
        VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPre,  HookMode.Pre);
        VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPost, HookMode.Post);
    }

    internal void UnregisterGiveHooks()
    {
        VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPre,  HookMode.Pre);
        VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPost, HookMode.Post);
    }

    private static readonly Dictionary<string, string> SilencedVariantAliases = new()
    {
        ["weapon_m4a1_silencer"] = "weapon_m4a1",
        ["weapon_usp_silencer"]  = "weapon_hkp2000",
    };

    private HookResult OnGiveNamedItemPre(DynamicHook hook)
    {
        try
        {
            var itemServices = hook.GetParam<CCSPlayer_ItemServices>(0);
            var player = itemServices.Pawn.Value?.Controller.Value?.As<CCSPlayerController>();
            if (player == null || !player.IsValid) return HookResult.Continue;

            if (!_pendingGive.TryGetValue(player.SteamID, out var pending)) return HookResult.Continue;

            var ptr = GetOrBuildEconItemView(player.SteamID, pending);
            hook.SetParam(3, ptr);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenGen] OnGiveNamedItemPre exception: {ex.Message}");
        }
        return HookResult.Continue;
    }

    private HookResult OnGiveNamedItemPost(DynamicHook hook)
    {
        try
        {
            var weapon = hook.GetReturn<CBasePlayerWeapon>();
            if (weapon == null || !weapon.IsValid) return HookResult.Continue;
            if (!weapon.DesignerName.Contains("weapon")) return HookResult.Continue;

            var itemServices = hook.GetParam<CCSPlayer_ItemServices>(0);
            var player = itemServices.Pawn.Value?.Controller.Value?.As<CCSPlayerController>();
            if (player == null || !player.IsValid) return HookResult.Continue;

            if (!_pendingGive.TryGetValue(player.SteamID, out var pending)) return HookResult.Continue;

            var isKnife      = pending.ClassName.Contains("knife");
            var resolvedName = SilencedVariantAliases.GetValueOrDefault(pending.ClassName, pending.ClassName);
            var nameMatch    = isKnife
                ? weapon.DesignerName.Contains("knife")
                : weapon.DesignerName == pending.ClassName || weapon.DesignerName == resolvedName;
            if (!nameMatch) return HookResult.Continue;

            _pendingGive.Remove(player.SteamID);

            weapon.FallbackPaintKit = pending.PaintKit;
            weapon.FallbackSeed     = pending.Seed;
            weapon.FallbackWear     = GetBumpedWear(player.SteamID, pending.PaintKit, pending.Wear, pending.Stickers);
            weapon.FallbackStatTrak = pending.StatTrakEnabled ? pending.StatTrakValue : -1;

            if (!isKnife)
            {
                weapon.AcceptInput("SetBodygroup", value: $"body,{(IsLegacyModel(pending.PaintKit) ? 1 : 0)}");
                weapon.AcceptInput("SubclassChange", value: weapon.DesignerName);
            }

            var isPistol     = !isKnife && PistolClasses.Contains(resolvedName);
            var slot         = isKnife ? "slot3" : isPistol ? "slot2" : "slot1";
            var switchPlayer = player;
            Server.NextFrame(() =>
            {
                if (!switchPlayer.IsValid || !switchPlayer.PawnIsAlive) return;
                Server.NextWorldUpdate(() =>
                {
                    if (switchPlayer.IsValid && switchPlayer.PawnIsAlive)
                        switchPlayer.ExecuteClientCommand(slot);
                });
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenGen] OnGiveNamedItemPost exception: {ex.Message}");
        }
        return HookResult.Continue;
    }

    private static readonly HashSet<ushort> GloveDefIndexes = new()
    {
        5027, 5028, 5029, 5030, 5031, 5032, 5033, 5034,
    };

    internal static bool IsGloveDefIndex(ushort defIndex) => GloveDefIndexes.Contains(defIndex);

    private void ApplyGloves(CCSPlayerController player, ushort defIndex, PendingSkin pending)
    {
        _equippedGloves[player.SteamID] = (defIndex, pending);

        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        var item = pawn.EconGloves;

        item.ItemDefinitionIndex = 0;

        item.ItemIDLow   = 0;
        item.ItemIDHigh  = 0;
        item.ItemID      = 0;
        item.AccountID   = 0;
        item.Initialized = false;

        item.AttributeList.Attributes.RemoveAll();
        item.NetworkedDynamicAttributes.Attributes.RemoveAll();

        item.ItemDefinitionIndex = defIndex;

        item.ItemIDLow   = (uint)(_nextItemId & 0xFFFFFFFF);
        item.ItemIDHigh  = (uint)(_nextItemId >> 32);
        item.ItemID      = _nextItemId++;
        item.AccountID   = (uint)player.SteamID;
        item.Initialized = true;

        item.AttributeList.Attributes.RemoveAll();
        item.NetworkedDynamicAttributes.Attributes.RemoveAll();

        var dynAttrs = item.NetworkedDynamicAttributes.Handle;
        SetOrAddAttr.Invoke(dynAttrs, "set item texture prefab", (float)pending.PaintKit);
        SetOrAddAttr.Invoke(dynAttrs, "set item texture seed",   (float)pending.Seed);
        SetOrAddAttr.Invoke(dynAttrs, "set item texture wear",   pending.Wear > 0f ? pending.Wear : 0.01f);

        var staticAttrs = item.AttributeList.Handle;
        SetOrAddAttr.Invoke(staticAttrs, "set item texture prefab", (float)pending.PaintKit);
        SetOrAddAttr.Invoke(staticAttrs, "set item texture seed",   (float)pending.Seed);
        SetOrAddAttr.Invoke(staticAttrs, "set item texture wear",   pending.Wear > 0f ? pending.Wear : 0.01f);

        var currentModel = pawn.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName ?? "";
        if (!string.IsNullOrEmpty(currentModel))
        {
            pawn.SetModel("characters/models/tm_jumpsuit/tm_jumpsuit_varianta.vmdl");
            pawn.SetModel(currentModel);
        }

        pawn.AcceptInput("SetBodygroup", value: "default_gloves,0");
        Server.NextFrame(() =>
        {
            if (!player.IsValid || !player.PawnIsAlive) return;
            player.PlayerPawn.Value?.AcceptInput("SetBodygroup", value: "default_gloves,1");
        });
    }
}
