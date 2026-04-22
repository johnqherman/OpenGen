using System.Globalization;
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
            player.PrintToChat(" Usage: !g <gencode or inspect link>");
            return;
        }

        var gencode = info.ArgString.Contains("//") ? info.ArgString.Trim() : info.ArgByIndex(1);
        var userId  = player.UserId;
        var steamId = player.SteamID;

        _ = FetchAndGiveAsync(gencode, userId, steamId);
    }

    private async Task FetchAndGiveAsync(string gencode, int? userId, ulong steamId)
    {
        if (InspectLinkParser.TryParse(gencode, out var inspectData, out var parseError))
        {
            var defIndex = (ushort)inspectData.DefIndex;
            var paintKit = (int)inspectData.PaintIndex;
            var seed     = (int)inspectData.PaintSeed;
            var wear     = inspectData.Wear;

            var stickers = DeduplicateStickerSlots(inspectData.Stickers
                .Select(s => new StickerSlot((int)s.Slot, (int)s.Id, s.Wear, s.OffsetX, s.OffsetY, s.Rotation))
                .ToArray());

            var kc = inspectData.Keychains.Length > 0 ? inspectData.Keychains[0] : default;

            GiveItem(defIndex, paintKit, seed, wear,
                stickers,
                (int)kc.Id, (int)kc.Pattern, kc.OffsetX, kc.OffsetY, kc.OffsetZ,
                inspectData.Quality == 9,
                inspectData.Quality == 9 ? (int)inspectData.KillEaterValue : 0,
                inspectData.CustomName,
                userId, steamId);
            return;
        }

        if (parseError != null)
        {
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                p?.PrintToChat($" {C.DarkRed}✗ {C.Default}{parseError}");
            });
            return;
        }

        var (apiResponse, lastError) = await FetchGenCodeAsync(gencode);
        var detail = apiResponse?.GenCodeDetail;

        if (detail == null || string.IsNullOrEmpty(detail.ItemId))
        {
            var msg = lastError ?? "Gencode not found";
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                p?.PrintToChat($" {C.DarkRed}✗ {C.Default}{msg}.");
            });
            return;
        }

        if (!ushort.TryParse(detail.ItemId, out var apiDefIndex))
        {
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                p?.PrintToChat($" {C.DarkRed}✗ {C.Default}Invalid item ID {C.Green}{detail.ItemId}{C.Default}.");
            });
            return;
        }

        int.TryParse(detail.SkinId,    out var apiPaintKit);
        int.TryParse(detail.PatternId, out var apiSeed);
        float.TryParse(detail.FloatValue, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var apiWear);

        GiveItem(apiDefIndex, apiPaintKit, apiSeed, apiWear,
            DeduplicateStickerSlots(new[]
            {
                new StickerSlot(detail.Sticker1Slot, detail.Sticker1Id, detail.Sticker1Value, detail.Sticker1X, detail.Sticker1Y, detail.Sticker1R),
                new StickerSlot(detail.Sticker2Slot, detail.Sticker2Id, detail.Sticker2Value, detail.Sticker2X, detail.Sticker2Y, detail.Sticker2R),
                new StickerSlot(detail.Sticker3Slot, detail.Sticker3Id, detail.Sticker3Value, detail.Sticker3X, detail.Sticker3Y, detail.Sticker3R),
                new StickerSlot(detail.Sticker4Slot, detail.Sticker4Id, detail.Sticker4Value, detail.Sticker4X, detail.Sticker4Y, detail.Sticker4R),
                new StickerSlot(detail.Sticker5Slot, detail.Sticker5Id, detail.Sticker5Value, detail.Sticker5X, detail.Sticker5Y, detail.Sticker5R),
            }.Where(s => s.Id != 0).ToArray()),
            detail.KeyChainId, detail.KeyChainPattern, detail.KeyChainX, detail.KeyChainY, detail.KeyChainZ,
            detail.StatTrakEnabled == "1", detail.StatTrakValue, detail.NameTag,
            userId, steamId);
    }

    private void GiveItem(
        ushort defIndex, int paintKit, int seed, float wear, StickerSlot[] stickers,
        int charmId, int charmSeed, float charmX, float charmY, float charmZ,
        bool statTrakEnabled, int statTrakValue, string nameTag,
        int? userId, ulong steamId)
    {
        if (IsGloveDefIndex(defIndex))
        {
            var pending = new PendingSkin("", paintKit, seed, wear, new StickerSlot[5]);
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
                    ? "agents/models/ctm_sas/ctm_sas.vmdl"
                    : "agents/models/tm_phoenix/tm_phoenix.vmdl");
                return;
            }

            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                p?.PrintToChat($" {C.DarkRed}✗ {C.Default}Unsupported item (defindex {C.Green}{defIndex}{C.Default}).");
            });
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

            var pending = new PendingSkin(
                giveClass, paintKit, seed, wear, stickers, defIndex,
                charmId, charmSeed, charmX, charmY, charmZ,
                statTrakEnabled, statTrakValue, nameTag);
            ScheduleWeaponGive(p, steamId, giveClass, pending);
        });
    }
}
