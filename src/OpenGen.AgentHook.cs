using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Text.Json;

namespace OpenGen;

public partial class OpenGen
{
    private const string AgentsUrl =
        "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/agents.json";

    private readonly Dictionary<ulong, string> _agentModels = new();
    private readonly Dictionary<ushort, string> _agentModelPaths = new();

    private async Task LoadAgentMapAsync()
    {
        var cacheFile = Path.Combine(ModuleDirectory, "agent_model_cache.json");
        try
        {
            if (File.Exists(cacheFile) &&
                (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalDays < CacheDays)
            {
                var cached = JsonSerializer.Deserialize<Dictionary<ushort, string>>(
                    await File.ReadAllTextAsync(cacheFile));
                if (cached?.Count > 0)
                {
                    ApplyAgentMap(cached);
                    Console.WriteLine($"[OpenGen] Loaded agent model map from cache: {_agentModelPaths.Count} agents");
                    return;
                }
            }

            using var stream = await _http.GetStreamAsync(AgentsUrl);
            using var doc   = await JsonDocument.ParseAsync(stream);

            var newMap = new Dictionary<ushort, string>();
            foreach (var agent in doc.RootElement.EnumerateArray())
            {
                if (!TryParseAgentDefindex(agent, out var defIdx)) continue;
                if (!agent.TryGetProperty("model_player", out var modelProp)) continue;
                var model = modelProp.GetString();
                if (string.IsNullOrEmpty(model)) continue;
                newMap[defIdx] = model;
            }

            ApplyAgentMap(newMap);

            if (newMap.Count > 0)
            {
                Console.WriteLine($"[OpenGen] Loaded agent model map: {_agentModelPaths.Count} agents");
                await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(newMap));
            }
            else
            {
                Console.WriteLine("[OpenGen] Agent model map is empty. No agents found in agents.json.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenGen] Failed to load agent model map: {ex.Message}");
        }
    }

    private static bool TryParseAgentDefindex(JsonElement el, out ushort result)
    {
        result = 0;
        if (!el.TryGetProperty("def_index", out var prop)) return false;
        if (prop.ValueKind == JsonValueKind.Number) return prop.TryGetUInt16(out result);
        return ushort.TryParse(prop.GetString(), out result);
    }

    internal bool TryGetAgentModel(ushort defIndex, out string modelPath)
    {
        lock (_agentModelPaths)
            return _agentModelPaths.TryGetValue(defIndex, out modelPath!);
    }

    internal void ApplyAgentModel(int? userId, ulong steamId, string modelPath)
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
    }

    private void ApplyAgentMap(Dictionary<ushort, string> map)
    {
        lock (_agentModelPaths)
        {
            _agentModelPaths.Clear();
            foreach (var kv in map)
                _agentModelPaths[kv.Key] = kv.Value;
        }
    }

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
