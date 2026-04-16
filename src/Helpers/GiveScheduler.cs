using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using C = CounterStrikeSharp.API.Modules.Utils.ChatColors;

namespace OpenGen;

public partial class OpenGen
{
    private static CBasePlayerWeapon? FindWeapon(CCSPlayerController player, Func<string, bool> match)
    {
        return player.PlayerPawn.Value?.WeaponServices?.MyWeapons
            .Select(h => h.Value)
            .FirstOrDefault(w => w?.IsValid == true && match(w.DesignerName));
    }

    private static readonly HashSet<string> PistolClasses = new()
    {
        "weapon_deagle", "weapon_elite", "weapon_fiveseven", "weapon_glock",
        "weapon_hkp2000", "weapon_p250", "weapon_tec9", "weapon_cz75a",
        "weapon_usp_silencer", "weapon_revolver", "weapon_p2000",
    };

    private static CBasePlayerWeapon? FindSlotConflict(CCSPlayerController player, string targetClass)
    {
        var targetSlot = targetClass.Contains("knife") ? gear_slot_t.GEAR_SLOT_KNIFE
            : PistolClasses.Contains(targetClass)      ? gear_slot_t.GEAR_SLOT_PISTOL
            :                                            gear_slot_t.GEAR_SLOT_RIFLE;

        return player.PlayerPawn.Value?.WeaponServices?.MyWeapons
            .Select(h => h.Value)
            .FirstOrDefault(w => w?.IsValid == true &&
                w.VData?.As<CCSWeaponBaseVData>()?.GearSlot == targetSlot);
    }

    private void ScheduleWeaponGive(CCSPlayerController p, ulong steamId, string giveClass, PendingSkin pending)
    {
        SilencedVariantAliases.TryGetValue(giveClass, out var engineName);

        var isKnife = giveClass.Contains("knife");
        Func<string, bool> match = isKnife
            ? n => n.Contains("knife")
            : n => n == giveClass || n == engineName;
        var existing = FindWeapon(p, match);

        if (existing != null)
        {
            var ws = p.PlayerPawn.Value?.WeaponServices?.As<CCSPlayer_WeaponServices>();
            if (ws != null) DropWeapon(ws.Handle, existing.Handle);
            existing.Remove();
        }
        else
        {
            var conflict = FindSlotConflict(p, giveClass);
            if (conflict != null)
            {
                var ws = p.PlayerPawn.Value?.WeaponServices?.As<CCSPlayer_WeaponServices>();
                if (ws != null) DropWeapon(ws.Handle, conflict.Handle);
                conflict.Remove();
            }
        }

        _pendingGive[steamId] = pending with { ClassName = giveClass };
        Server.NextFrame(() =>
        {
            if (!p.IsValid || !p.PawnIsAlive) { _pendingGive.Remove(steamId); return; }
            p.GiveNamedItem(giveClass);
            if (_pendingGive.ContainsKey(steamId))
            {
                _pendingGive.Remove(steamId);
                p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
            }
        });
    }

    private static StickerSlot[] DeduplicateStickerSlots(StickerSlot[] stickers)
    {
        var used   = new HashSet<int>();
        var result = new StickerSlot[stickers.Length];
        int next   = 0;

        for (int i = 0; i < stickers.Length; i++)
        {
            var s = stickers[i];
            if (!used.Contains(s.Slot))
            {
                used.Add(s.Slot);
                result[i] = s;
            }
            else
            {
                while (next < stickers.Length && used.Contains(next)) next++;
                used.Add(next);
                result[i] = s with { Slot = next };
            }
        }

        return result;
    }
}
