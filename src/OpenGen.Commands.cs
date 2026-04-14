using System.Diagnostics;
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

    private void CmdGive(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive) return;

        if (info.ArgCount < 2)
        {
            player.PrintToChat(" Usage: !g <gencode>  (gencode from cs2inspects.com)");
            return;
        }

        var gencode    = info.ArgByIndex(1);
        var userId     = player.UserId;
        var steamId    = player.SteamID;
        var scriptPath = Path.Combine(ModuleDirectory, "gencode.sh");

        Task.Run(() => FetchAndGive(gencode, userId, steamId, scriptPath));
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

            _pendingGive[p.SteamID] = new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex);
            p.GiveNamedItem(giveClass);

            if (_pendingGive.ContainsKey(p.SteamID))
            {
                _pendingGive.Remove(p.SteamID);
                p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
            }
        });
    }

    private void FetchAndGive(string gencode, int? userId, ulong steamId, string scriptPath)
    {
        const int maxAttempts = 3;

        GenCodeDetail? detail = null;
        string? lastError    = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var psi = new ProcessStartInfo("bash", $"\"{scriptPath}\" {gencode}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                };
                using var proc = Process.Start(psi)!;
                var json = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(6000);

                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                {
                    lastError = "Gencode not found";
                    if (attempt < maxAttempts) { Thread.Sleep(800); continue; }
                    break;
                }

                if (json.Contains("\"error\""))
                {
                    lastError = "API returned an error";
                    if (attempt < maxAttempts) { Thread.Sleep(800); continue; }
                    break;
                }

                var parsed = JsonSerializer.Deserialize<GenCodeDetail>(json);
                if (parsed == null || string.IsNullOrEmpty(parsed.ItemId))
                {
                    lastError = "Failed to parse response";
                    if (attempt < maxAttempts) { Thread.Sleep(800); continue; }
                    break;
                }

                detail = parsed;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < maxAttempts) { Thread.Sleep(800); continue; }
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

            SilencedVariantAliases.TryGetValue(className, out var engineName);
            var existing = FindWeapon(p, isKnife ? n => n.Contains("knife") : n => n == className || n == engineName);
            if (existing != null)
            {
                var ws = p.PlayerPawn.Value?.WeaponServices?.As<CCSPlayer_WeaponServices>();
                if (ws != null) DropWeapon(ws.Handle, existing.Handle);
                existing.Remove();
            }

            _pendingGive[steamId] = new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex,
                charmId, charmSeed, charmX, charmY, charmZ, statTrakEnabled, statTrakValue, nameTag);
            p.GiveNamedItem(giveClass);

            if (_pendingGive.ContainsKey(steamId))
            {
                _pendingGive.Remove(steamId);
                p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
            }
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
