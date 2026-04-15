using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
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
            : PistolClasses.Contains(targetClass)     ? gear_slot_t.GEAR_SLOT_PISTOL
            :                                           gear_slot_t.GEAR_SLOT_RIFLE;

        return player.PlayerPawn.Value?.WeaponServices?.MyWeapons
            .Select(h => h.Value)
            .FirstOrDefault(w => w?.IsValid == true &&
                w.VData?.As<CCSWeaponBaseVData>()?.GearSlot == targetSlot);
    }

    private void ScheduleWeaponGive(CCSPlayerController p, ulong steamId, string giveClass, PendingSkin pending)
    {
        SilencedVariantAliases.TryGetValue(giveClass, out var engineName);
        var isKnife  = giveClass.Contains("knife");
        var existing = FindWeapon(p, isKnife ? n => n.Contains("knife") : n => n == giveClass || n == engineName);
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
        _pendingGive[steamId] = pending;
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

    private void CmdGive(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        if (info.ArgCount < 2)
        {
            player.PrintToChat(" Usage: !g <gencode>  (gencode from cs2inspects.com)");
            return;
        }

        var gencode = info.ArgByIndex(1);
        var userId  = player.UserId;
        var steamId = player.SteamID;

        _ = FetchAndGiveAsync(gencode, userId, steamId);
    }

    private void CmdGiveParsed(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        if (info.ArgCount < 5)
        {
            player.PrintToChat(" Usage: !gen <defindex> <skin_id> <pattern> <float> [<sticker_id> <wear> ...]");
            return;
        }

        if (!ushort.TryParse(info.ArgByIndex(1), out var defIndex))
        {
            player.PrintToChat($" {C.DarkRed}✗ {C.Default}Invalid defindex.");
            return;
        }

        int.TryParse(info.ArgByIndex(2), out var paintKit);
        int.TryParse(info.ArgByIndex(3), out var seed);
        float.TryParse(info.ArgByIndex(4), NumberStyles.Float, CultureInfo.InvariantCulture, out var wear);

        var stickers = new (int Slot, int Id, float Wear, float X, float Y, float R)[5];
        for (int i = 0; i < 5; i++)
        {
            int argBase = 5 + i * 2;
            if (info.ArgCount < argBase + 2) break;
            int.TryParse(info.ArgByIndex(argBase), out var id);
            float.TryParse(info.ArgByIndex(argBase + 1), NumberStyles.Float,
                           CultureInfo.InvariantCulture, out var stickerWear);
            stickers[i] = (i, id, stickerWear, 0f, 0f, 0f);
        }

        var pending = new PendingSkin(
            WeaponClasses.TryGetValue(defIndex, out var cls) ? cls : "",
            paintKit, seed, wear, stickers, defIndex);

        Server.NextFrame(() =>
        {
            var p = player;
            if (!p.IsValid || !p.PawnIsAlive) return;

            if (IsGloveDefIndex(defIndex))
            {
                ApplyGloves(p, defIndex, pending);
                return;
            }

            if (!WeaponClasses.TryGetValue(defIndex, out var className))
            {
                p.PrintToChat($" {C.DarkRed}✗ {C.Default}Unsupported defindex {C.Green}{defIndex}{C.Default}.");
                return;
            }

            var isKnife   = className.Contains("knife");
            var giveClass = isKnife
                ? (p.TeamNum == 2 ? "weapon_knife_t" : "weapon_knife")
                : className;

            SilencedVariantAliases.TryGetValue(className, out var engineName);
            var existing = FindWeapon(p, isKnife ? n => n.Contains("knife") : n => n == className || n == engineName);
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
            _pendingGive[p.SteamID] = new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex);
            Server.NextFrame(() =>
            {
                if (!p.IsValid || !p.PawnIsAlive) { _pendingGive.Remove(p.SteamID); return; }
                p.GiveNamedItem(giveClass);
                if (_pendingGive.ContainsKey(p.SteamID))
                {
                    _pendingGive.Remove(p.SteamID);
                    p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
                }
            });
        });
    }

    private async Task FetchAndGiveAsync(string gencode, int? userId, ulong steamId)
    {
        const int maxAttempts = 3;

        GenCodeDetail? detail = null;
        string? lastError    = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var url      = $"https://api.cs2inspects.com/getGenCode?url={Uri.EscapeDataString(gencode)}";
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json   = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<GenCodeApiResponse>(json)?.GenCodeDetail;

                if (parsed == null || string.IsNullOrEmpty(parsed.ItemId))
                {
                    lastError = "Gencode not found";
                    if (attempt < maxAttempts) { await Task.Delay(800); continue; }
                    break;
                }

                detail = parsed;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < maxAttempts) { await Task.Delay(800); continue; }
            }
        }

        if (detail == null)
        {
            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}{lastError ?? "Unknown error"}."));
            return;
        }

        if (!ushort.TryParse(detail.ItemId, out var defIndex))
        {
            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}Invalid item ID {C.Green}{detail.ItemId}{C.Default}."));
            return;
        }

        int.TryParse(detail.SkinId,    out var paintKit);
        int.TryParse(detail.PatternId, out var seed);
        float.TryParse(detail.FloatValue, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var wear);

        var stickers = DeduplicateStickerSlots(new[]
        {
            (detail.Sticker1Slot, detail.Sticker1Id, detail.Sticker1Value, detail.Sticker1X, detail.Sticker1Y, detail.Sticker1R),
            (detail.Sticker2Slot, detail.Sticker2Id, detail.Sticker2Value, detail.Sticker2X, detail.Sticker2Y, detail.Sticker2R),
            (detail.Sticker3Slot, detail.Sticker3Id, detail.Sticker3Value, detail.Sticker3X, detail.Sticker3Y, detail.Sticker3R),
            (detail.Sticker4Slot, detail.Sticker4Id, detail.Sticker4Value, detail.Sticker4X, detail.Sticker4Y, detail.Sticker4R),
            (detail.Sticker5Slot, detail.Sticker5Id, detail.Sticker5Value, detail.Sticker5X, detail.Sticker5Y, detail.Sticker5R),
        });

        var charmId         = detail.KeyChainId;
        var charmSeed       = detail.KeyChainPattern;
        var charmX          = detail.KeyChainX;
        var charmY          = detail.KeyChainY;
        var charmZ          = detail.KeyChainZ;
        var statTrakEnabled = detail.StatTrakEnabled == "1";
        var statTrakValue   = detail.StatTrakValue;
        var nameTag         = detail.NameTag;

        if (IsGloveDefIndex(defIndex))
        {
            var pending = new PendingSkin("", paintKit, seed, wear,
                new (int, int, float, float, float, float)[5]);
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                if (p == null || !p.IsValid || !p.PawnIsAlive) return;
                ApplyGloves(p, defIndex, pending);
            });
            return;
        }

        if (!WeaponClasses.TryGetValue(defIndex, out var className))
        {
            if (TryGetAgentModel(defIndex, out var modelPath))
            {
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                    if (p == null || !p.IsValid) return;
                    _agentModels[steamId] = modelPath;
                    if (p.PawnIsAlive)
                    {
                        p.PlayerPawn.Value?.SetModel(modelPath);
                        if (_equippedGloves.TryGetValue(steamId, out var gloves))
                            Server.NextFrame(() =>
                            {
                                if (p.IsValid && p.PawnIsAlive)
                                    ApplyGloves(p, gloves.DefIndex, gloves.Pending);
                            });
                    }
                });
                return;
            }

            if (defIndex is 5600 or 5200)
            {
                var defaultModel = defIndex == 5600
                    ? "characters/models/ctm_sas/ctm_sas.vmdl"
                    : "characters/models/tm_phoenix/tm_phoenix.vmdl";
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                    if (p == null || !p.IsValid) return;
                    _agentModels[steamId] = defaultModel;
                    if (p.PawnIsAlive)
                    {
                        p.PlayerPawn.Value?.SetModel(defaultModel);
                        if (_equippedGloves.TryGetValue(steamId, out var gloves))
                            Server.NextFrame(() =>
                            {
                                if (p.IsValid && p.PawnIsAlive)
                                    ApplyGloves(p, gloves.DefIndex, gloves.Pending);
                            });
                    }
                });
                return;
            }

            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}Unsupported item (defindex {C.Green}{detail.ItemId}{C.Default})."));
            return;
        }

        Server.NextFrame(() =>
        {
            var p = Utilities.GetPlayerFromUserid(userId ?? 0);
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;

            var isKnife   = className.Contains("knife");
            var giveClass = isKnife
                ? (p.TeamNum == 2 ? "weapon_knife_t" : "weapon_knife")
                : className;

            var pending = new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex,
                charmId, charmSeed, charmX, charmY, charmZ, statTrakEnabled, statTrakValue, nameTag);
            ScheduleWeaponGive(p, steamId, giveClass, pending);
        });
    }

    private void CmdCombo(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        if (info.ArgCount < 2)
        {
            player.PrintToChat(" Usage: !combo <gencode>  (gencode from cs2inspects.com)");
            return;
        }

        _ = FetchAndGiveComboAsync(info.ArgByIndex(1), player.UserId, player.SteamID);
    }

    private async Task FetchAndGiveComboAsync(string gencode, int? userId, ulong steamId)
    {
        const int maxAttempts = 3;

        List<ComboPart>? parts = null;
        string? lastError     = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var url      = $"https://api.cs2inspects.com/getGenCode?url={Uri.EscapeDataString(gencode)}";
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json   = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<GenCodeApiResponse>(json)?.ComboParts;

                if (parsed == null || !parsed.Any(p => p.ItemId != 0))
                {
                    lastError = "Combo not found";
                    if (attempt < maxAttempts) { await Task.Delay(800); continue; }
                    break;
                }

                parts = parsed;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < maxAttempts) { await Task.Delay(800); continue; }
            }
        }

        if (parts == null)
        {
            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}{lastError ?? "Unknown error"}."));
            return;
        }

        string? agentModel                             = null;
        ushort  gloveDefIndex                          = 0;
        PendingSkin? glovePending                      = null;
        var weapons = new List<(string ClassName, PendingSkin Pending)>();

        foreach (var part in parts.Where(p => p.ItemId != 0))
        {
            var defIndex = (ushort)part.ItemId;
            var stickers = DeduplicateStickerSlots(new[]
            {
                (part.Sticker1Slot, part.Sticker1Id, part.Sticker1Value, part.Sticker1X, part.Sticker1Y, part.Sticker1R),
                (part.Sticker2Slot, part.Sticker2Id, part.Sticker2Value, part.Sticker2X, part.Sticker2Y, part.Sticker2R),
                (part.Sticker3Slot, part.Sticker3Id, part.Sticker3Value, part.Sticker3X, part.Sticker3Y, part.Sticker3R),
                (part.Sticker4Slot, part.Sticker4Id, part.Sticker4Value, part.Sticker4X, part.Sticker4Y, part.Sticker4R),
                (part.Sticker5Slot, part.Sticker5Id, part.Sticker5Value, part.Sticker5X, part.Sticker5Y, part.Sticker5R),
            });

            if (IsGloveDefIndex(defIndex))
            {
                gloveDefIndex = defIndex;
                glovePending  = new PendingSkin("", part.SkinId, part.PatternId, part.FloatValue,
                    new (int, int, float, float, float, float)[5], defIndex);
                continue;
            }

            if (TryGetAgentModel(defIndex, out var modelPath))
            {
                agentModel = modelPath;
                continue;
            }

            if (defIndex is 5600 or 5200)
            {
                agentModel = defIndex == 5600
                    ? "characters/models/ctm_sas/ctm_sas.vmdl"
                    : "characters/models/tm_phoenix/tm_phoenix.vmdl";
                continue;
            }

            if (!WeaponClasses.TryGetValue(defIndex, out var className)) continue;

            var pending = new PendingSkin(className, part.SkinId, part.PatternId, part.FloatValue, stickers, defIndex,
                part.KcId, part.KcPatternId, part.KcX, part.KcY, part.KcZ,
                part.StatTrakEnabled == 1, part.StatTrakValue, part.NameTag);
            weapons.Add((className, pending));
        }

        Server.NextFrame(() =>
        {
            var p = Utilities.GetPlayerFromUserid(userId ?? 0);
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;

            if (agentModel != null)
            {
                _agentModels[steamId] = agentModel;
                p.PlayerPawn.Value?.SetModel(agentModel);
            }

            if (glovePending != null)
            {
                var gd = gloveDefIndex;
                var gp = glovePending;
                if (agentModel != null)
                {
                    Server.NextFrame(() =>
                    {
                        if (p.IsValid && p.PawnIsAlive)
                            ApplyGloves(p, gd, gp);
                    });
                }
                else
                {
                    ApplyGloves(p, gd, gp);
                }
            }

            void GiveNext(int i)
            {
                if (i >= weapons.Count) return;
                var (className, pending) = weapons[i];
                var isKnife   = className.Contains("knife");
                var giveClass = isKnife
                    ? (p.TeamNum == 2 ? "weapon_knife_t" : "weapon_knife")
                    : className;
                ScheduleWeaponGive(p, steamId, giveClass, pending with { ClassName = giveClass });
                Server.NextFrame(() => GiveNext(i + 1));
            }
            GiveNext(0);
        });
    }

    private static (int Slot, int Id, float Wear, float X, float Y, float R)[] DeduplicateStickerSlots(
        (int Slot, int Id, float Wear, float X, float Y, float R)[] stickers)
    {
        var used   = new HashSet<int>();
        var result = new (int Slot, int Id, float Wear, float X, float Y, float R)[stickers.Length];
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
