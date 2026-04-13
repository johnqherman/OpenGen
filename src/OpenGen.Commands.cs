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

        player.PrintToChat($" {C.Grey}Fetching gencode{C.Grey}...");

        Task.Run(() => FetchAndGive(gencode, userId, steamId, scriptPath));
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

        var stickers = new[]
        {
            (detail.Sticker1Slot, detail.Sticker1Id, detail.Sticker1Value),
            (detail.Sticker2Slot, detail.Sticker2Id, detail.Sticker2Value),
            (detail.Sticker3Slot, detail.Sticker3Id, detail.Sticker3Value),
            (detail.Sticker4Slot, detail.Sticker4Id, detail.Sticker4Value),
            (detail.Sticker5Slot, detail.Sticker5Id, detail.Sticker5Value),
        };

        if (IsGloveDefIndex(defIndex))
        {
            var gloveName = detail.ItemName;
            var pending   = new PendingSkin("", paintKit, seed, wear, stickers);
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                if (p == null || !p.IsValid || !p.PawnIsAlive) return;
                ApplyGloves(p, defIndex, pending);
                p.PrintToChat($" {C.Green}✓ {C.Default}{gloveName}");
            });
            return;
        }

        if (!WeaponClasses.TryGetValue(defIndex, out var className))
        {
            if (TryGetAgentModel(defIndex, out var modelPath))
            {
                var agentName = detail.ItemName;
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
                    p.PrintToChat($" {C.Green}✓ {C.Default}{agentName}");
                });
                return;
            }

            if (defIndex is 5600 or 5200)
            {
                var defaultModel = defIndex == 5600
                    ? "characters/models/ctm_sas/ctm_sas.vmdl"
                    : "characters/models/tm_phoenix/tm_phoenix.vmdl";
                var agentName = detail.ItemName;
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
                    p.PrintToChat($" {C.Green}✓ {C.Default}{agentName}");
                });
                return;
            }

            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}Unsupported item {C.Green}{detail.ItemName} {C.Default}(defindex {C.Green}{detail.ItemId}{C.Default})."));
            return;
        }

        Server.NextFrame(() =>
        {
            var p = Utilities.GetPlayerFromUserid(userId ?? 0);
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;

            if (className.Contains("knife"))
            {
                var knife = FindWeapon(p, n => n.Contains("knife"));
                var weaponServices = p.PlayerPawn.Value?.WeaponServices?.As<CCSPlayer_WeaponServices>();
                if (knife != null && weaponServices != null)
                {
                    DropWeapon(weaponServices.Handle, knife.Handle);
                    knife.Remove();
                }

                var giveClass = p.TeamNum == 2 ? "weapon_knife_t" : "weapon_knife";
                _pendingGive[steamId] = new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex);
                p.GiveNamedItem(giveClass);

                if (_pendingGive.ContainsKey(steamId))
                {
                    _pendingGive.Remove(steamId);
                    p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
                }
                else
                {
                    p.PrintToChat($" {C.Green}✓ {C.Default}{detail.ItemName}");
                }
                return;
            }

            _pendingGive[steamId] = new PendingSkin(className, paintKit, seed, wear, stickers);
            var weaponPtr = p.GiveNamedItem(className);

            if (_pendingGive.ContainsKey(steamId))
            {
                _pendingGive.Remove(steamId);
                p.PrintToChat($" {C.DarkRed}✗ {C.Default}Failed to give weapon.");
            }
            else
            {
                p.PrintToChat($" {C.Green}✓ {C.Default}{detail.ItemName}");
            }
        });
    }
}
