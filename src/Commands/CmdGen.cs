using System.Globalization;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using C = CounterStrikeSharp.API.Modules.Utils.ChatColors;

namespace OpenGen;

public partial class OpenGen
{
    private void CmdGen(CCSPlayerController? player, CommandInfo info)
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
                ApplyAgentModel(userId, steamId, modelPath);
                return;
            }

            if (defIndex is 5600 or 5200)
            {
                ApplyAgentModel(userId, steamId, defIndex == 5600
                    ? "characters/models/ctm_sas/ctm_sas.vmdl"
                    : "characters/models/tm_phoenix/tm_phoenix.vmdl");
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
}
