using System.Text.Json;

namespace OpenGen;

public partial class OpenGen
{
    private async Task<(GenCodeApiResponse? Response, string? Error)> FetchGenCodeAsync(string gencode)
    {
        const int maxAttempts = 3;
        string?   lastError   = null;

        var url = $"https://api.cs2inspects.com/getGenCode?url={Uri.EscapeDataString(gencode)}";

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json   = await response.Content.ReadAsStringAsync();
                var parsed = JsonSerializer.Deserialize<GenCodeApiResponse>(json, _jsonOptions);

                return (parsed, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < maxAttempts) await Task.Delay(800);
            }
        }

        return (null, lastError);
    }
}
