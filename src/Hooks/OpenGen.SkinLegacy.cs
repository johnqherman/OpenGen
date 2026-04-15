using System.Text.Json;

namespace OpenGen;

public partial class OpenGen
{
    private const string SkinsUrl = "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins.json";
    private const int CacheDays = 7;

    private async Task LoadSkinLegacyMapAsync()
    {
        var cacheFile = Path.Combine(ModuleDirectory, "skin_legacy_cache.json");
        try
        {
            if (File.Exists(cacheFile) &&
                (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalDays < CacheDays)
            {
                var cached = JsonSerializer.Deserialize<Dictionary<int, bool>>(
                    await File.ReadAllTextAsync(cacheFile));
                if (cached != null)
                {
                    ApplyMap(cached);
                    Console.WriteLine($"[OpenGen] Loaded legacy_model map from cache: {_skinLegacyMap.Count} skins");
                    return;
                }
            }

            using var stream = await _http.GetStreamAsync(SkinsUrl);
            using var doc    = await JsonDocument.ParseAsync(stream);

            var newMap = new Dictionary<int, bool>();
            foreach (var skin in doc.RootElement.EnumerateArray())
            {
                if (!skin.TryGetProperty("paint_index", out var piProp)) continue;
                if (!int.TryParse(piProp.GetString(), out var paintIndex)) continue;

                var legacy = !skin.TryGetProperty("legacy_model", out var legProp)
                             || legProp.GetBoolean();
                newMap[paintIndex] = legacy;
            }

            ApplyMap(newMap);
            Console.WriteLine($"[OpenGen] Loaded legacy_model map: {_skinLegacyMap.Count} skins");

            await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(newMap));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OpenGen] Failed to load skin legacy map: {ex.Message}");
        }
    }

    private void ApplyMap(Dictionary<int, bool> map)
    {
        lock (_skinLegacyMap)
        {
            _skinLegacyMap.Clear();
            foreach (var kv in map)
                _skinLegacyMap[kv.Key] = kv.Value;
        }
    }

    private bool IsLegacyModel(int paintKit)
    {
        lock (_skinLegacyMap)
            return !_skinLegacyMap.TryGetValue(paintKit, out var legacy) || legacy;
    }
}
