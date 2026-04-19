using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using C = CounterStrikeSharp.API.Modules.Utils.ChatColors;

namespace OpenGen;

public partial class OpenGen
{
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
        var (apiResponse, lastError) = await FetchGenCodeAsync(gencode);
        var parts = apiResponse?.ComboParts?.Where(p => p.ItemId != 0).ToList();

        if (parts == null || parts.Count == 0)
        {
            var msg = lastError ?? "Combo not found";
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromUserid(userId ?? 0);
                p?.PrintToChat($" {C.DarkRed}✗ {C.Default}{msg}.");
            });
            return;
        }

        string?      agentModel    = null;
        ushort       gloveDefIndex = 0;
        PendingSkin? glovePending  = null;
        var          weapons       = new List<(string ClassName, PendingSkin Pending)>();

        foreach (var part in parts)
        {
            var defIndex = (ushort)part.ItemId;
            var stickers = new[]
            {
                new StickerSlot(part.Sticker1Slot, part.Sticker1Id, part.Sticker1Value, part.Sticker1X, part.Sticker1Y, part.Sticker1R),
                new StickerSlot(part.Sticker2Slot, part.Sticker2Id, part.Sticker2Value, part.Sticker2X, part.Sticker2Y, part.Sticker2R),
                new StickerSlot(part.Sticker3Slot, part.Sticker3Id, part.Sticker3Value, part.Sticker3X, part.Sticker3Y, part.Sticker3R),
                new StickerSlot(part.Sticker4Slot, part.Sticker4Id, part.Sticker4Value, part.Sticker4X, part.Sticker4Y, part.Sticker4R),
                new StickerSlot(part.Sticker5Slot, part.Sticker5Id, part.Sticker5Value, part.Sticker5X, part.Sticker5Y, part.Sticker5R),
            }.Where(s => s.Id != 0).ToArray();

            if (IsGloveDefIndex(defIndex))
            {
                gloveDefIndex = defIndex;
                glovePending  = new PendingSkin(
                    "", part.SkinId, part.PatternId, part.FloatValue, new StickerSlot[5], defIndex);
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

            var pending = new PendingSkin(
                className, part.SkinId, part.PatternId, part.FloatValue, stickers, defIndex,
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

                ScheduleWeaponGive(p, steamId, giveClass, pending);
                Server.NextFrame(() => GiveNext(i + 1));
            }
            GiveNext(0);
        });
    }
}
