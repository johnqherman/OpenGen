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
    internal static bool IsGloveClass(string name) =>
        name.Contains("glove") || name.Contains("handwrap");

    private static void StripItemIf(CCSPlayerController player, Func<string, bool> match)
    {
        var services = player.PlayerPawn.Value?.WeaponServices;
        if (services == null) return;
        var toRemove = services.MyWeapons
            .Select(h => h.Value)
            .Where(w => w?.IsValid == true && match(w.DesignerName))
            .ToList();
        foreach (var w in toRemove)
            w!.Remove();
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
                        p.PlayerPawn.Value?.SetModel(modelPath);
                    p.PrintToChat($" {C.Green}✓ {C.Default}{agentName}");
                });
                return;
            }

            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" {C.DarkRed}✗ {C.Default}Unsupported item {C.Green}{detail.ItemName} {C.Default}(defindex {C.Green}{detail.ItemId}{C.Default})."));
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

        Server.NextFrame(() =>
        {
            var p = Utilities.GetPlayerFromUserid(userId ?? 0);
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;

            if (className.Contains("knife"))
                StripItemIf(p, n => n.Contains("knife"));
            else if (IsGloveClass(className))
                StripItemIf(p, IsGloveClass);

            _pendingGive[steamId] = new PendingSkin(className, paintKit, seed, wear, stickers);
            var weaponPtr = p.GiveNamedItem(className);

            if (weaponPtr == nint.Zero)
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
