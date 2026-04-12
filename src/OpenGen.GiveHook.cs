using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace OpenGen;

public partial class OpenGen
{
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
            if (weapon.DesignerName != pending.ClassName) return HookResult.Continue;

            _pendingGive.Remove(player.SteamID);
            ApplySkin(player, weapon, pending);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenGen] OnGiveNamedItemPost exception: {ex.Message}");
        }
        return HookResult.Continue;
    }

    private void ApplySkin(CCSPlayerController player, CBasePlayerWeapon weapon, PendingSkin pending)
    {
        weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
        weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();

        var itemId = _nextItemId++;
        weapon.AttributeManager.Item.ItemID     = itemId;
        weapon.AttributeManager.Item.ItemIDLow  = (uint)(itemId & 0xFFFFFFFF);
        weapon.AttributeManager.Item.ItemIDHigh = (uint)(itemId >> 32);
        weapon.AttributeManager.Item.AccountID  = (uint)player.SteamID;

        weapon.FallbackPaintKit = pending.PaintKit;
        weapon.FallbackSeed     = pending.Seed;
        weapon.FallbackWear     = pending.Wear > 0f ? pending.Wear : 0.01f;
        weapon.FallbackStatTrak = -1;

        var dynAttrs = weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle;
        SetOrAddAttr.Invoke(dynAttrs, "set item texture prefab", (float)pending.PaintKit);

        foreach (var (slot, id, stickerWear) in pending.Stickers)
        {
            if (id == 0) continue;
            SetOrAddAttr.Invoke(dynAttrs, $"sticker slot {slot} id",   UintAsFloat((uint)id));
            SetOrAddAttr.Invoke(dynAttrs, $"sticker slot {slot} wear", stickerWear);
        }

        weapon.AcceptInput("SetBodygroup", value: $"body,{(IsLegacyModel(pending.PaintKit) ? 1 : 0)}");
    }
}
