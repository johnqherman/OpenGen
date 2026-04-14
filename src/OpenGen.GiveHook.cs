using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace OpenGen;

public partial class OpenGen
{
    private HookResult OnGiveNamedItemPre(DynamicHook hook)
    {
        try
        {
            var itemServices = hook.GetParam<CCSPlayer_ItemServices>(0);
            var player = itemServices.Pawn.Value?.Controller.Value?.As<CCSPlayerController>();
            if (player == null || !player.IsValid) return HookResult.Continue;

            if (!_pendingGive.TryGetValue(player.SteamID, out var pending)) return HookResult.Continue;

            var isKnife = pending.ClassName.Contains("knife");
            var ptr = GetOrBuildEconItemView(player.SteamID, pending.DefIndex,
                pending.PaintKit, pending.Seed, pending.Wear, pending.Stickers, isKnife,
                pending.CharmId, pending.CharmSeed, pending.CharmX, pending.CharmY, pending.CharmZ,
                pending.StatTrakEnabled, pending.StatTrakValue, pending.NameTag);
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

            var isKnife = pending.ClassName.Contains("knife");
            if (isKnife ? !weapon.DesignerName.Contains("knife") : weapon.DesignerName != pending.ClassName)
                return HookResult.Continue;

            _pendingGive.Remove(player.SteamID);

            weapon.FallbackPaintKit = pending.PaintKit;
            weapon.FallbackSeed     = pending.Seed;
            weapon.FallbackWear     = pending.Wear > 0f ? pending.Wear : 0.01f;
            weapon.FallbackStatTrak = pending.StatTrakEnabled ? pending.StatTrakValue : -1;

            var weaponDynAttrs = weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle;
            foreach (var (slot, id, stickerWear, x, y, r) in pending.Stickers)
            {
                if (id == 0) continue;
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} id",       UintAsFloat((uint)id));
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} wear",     stickerWear);
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} scale",    1f);
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} offset x", x);
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} offset y", y);
                SetOrAddAttr.Invoke(weaponDynAttrs, $"sticker slot {slot} rotation", r);
            }

            if (!isKnife)
                weapon.AcceptInput("SetBodygroup", value: $"body,{(IsLegacyModel(pending.PaintKit) ? 1 : 0)}");
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
        item.ItemIDLow  = 0;
        item.ItemIDHigh = 0;
        item.ItemID     = 0;
        item.AccountID  = 0;
        item.Initialized = false;
        item.AttributeList.Attributes.RemoveAll();
        item.NetworkedDynamicAttributes.Attributes.RemoveAll();

        item.ItemDefinitionIndex = defIndex;

        item.ItemIDLow  = (uint)(_nextItemId & 0xFFFFFFFF);
        item.ItemIDHigh = (uint)(_nextItemId >> 32);
        item.ItemID     = _nextItemId++;
        item.AccountID  = (uint)player.SteamID;
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
