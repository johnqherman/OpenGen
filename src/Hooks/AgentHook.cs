using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace OpenGen;

public partial class OpenGen
{
    private HookResult OnPlayerSpawnPost(EventPlayerSpawn ev, GameEventInfo _)
    {
        var player = ev.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;
        if (!_agentModels.TryGetValue(player.SteamID, out var model)) return HookResult.Continue;

        Server.NextFrame(() =>
        {
            if (player.IsValid && player.PawnIsAlive)
            {
                player.PlayerPawn.Value?.SetModel(model);
                if (_equippedGloves.TryGetValue(player.SteamID, out var gloves))
                    Server.NextFrame(() =>
                    {
                        if (player.IsValid && player.PawnIsAlive)
                            ApplyGloves(player, gloves.DefIndex, gloves.Pending);
                    });
            }
        });

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnectPost(EventPlayerDisconnect ev, GameEventInfo _)
    {
        if (ev.Userid != null)
        {
            var steamId = ev.Userid.SteamID;
            _agentModels.Remove(steamId);
            _equippedGloves.Remove(steamId);
            _stickerWearCache.Remove(steamId);
            FreeEconItemView(steamId);
        }
        return HookResult.Continue;
    }
}
