using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace OpenGen;

public partial class OpenGen
{
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

        player.PrintToChat($" \x08Fetching \x04{gencode}\x08...");

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
                ?.PrintToChat($" \x02✗ \x01{lastError ?? "Unknown error"}."));
            return;
        }

        if (!ushort.TryParse(detail.ItemId, out var defIndex) ||
            !WeaponClasses.TryGetValue(defIndex, out var className))
        {
            Server.NextFrame(() => Utilities.GetPlayerFromUserid(userId ?? 0)
                ?.PrintToChat($" \x02✗ \x01Unknown weapon defindex \x04{detail.ItemId}\x01."));
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

            _pendingGive[steamId] = new PendingSkin(className, paintKit, seed, wear, stickers);
            var weaponPtr = p.GiveNamedItem(className);

            if (weaponPtr == nint.Zero)
            {
                _pendingGive.Remove(steamId);
                p.PrintToChat(" \x02✗ \x01" + "Failed to give weapon.");
            }
        });
    }
}
