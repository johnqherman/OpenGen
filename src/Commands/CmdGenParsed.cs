using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using C = CounterStrikeSharp.API.Modules.Utils.ChatColors;

namespace OpenGen;

public partial class OpenGen
{
    private void CmdGenParsed(CCSPlayerController? player, CommandInfo info)
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

        Server.NextFrame(() =>
        {
            var p = player;
            if (!p.IsValid || !p.PawnIsAlive) return;

            if (IsGloveDefIndex(defIndex))
            {
                ApplyGloves(p, defIndex, new PendingSkin("", paintKit, seed, wear, stickers, defIndex));
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

            ScheduleWeaponGive(p, p.SteamID, giveClass,
                new PendingSkin(giveClass, paintKit, seed, wear, stickers, defIndex));
        });
    }
}
