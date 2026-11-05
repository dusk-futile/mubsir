using System.Net;
using System.Net.Http.Headers;
using Mubsir.Core.Setup;

namespace Mubsir.App.Host;

internal enum KeyTest
{
    Works,
    Wrong,
    NoCredit,
    CouldNotCheck,
}

/// <summary>Tries a key with the smallest real call, so a bad key is caught now and not on the first request.</summary>
internal static class KeyTester
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<KeyTest> TestAsync(KeyKind kind, string key, CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = Build(kind, key.Trim());
            using HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            string body = response.IsSuccessStatusCode ? string.Empty : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Read(response.StatusCode, body);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return KeyTest.CouldNotCheck;
        }
    }

    private static HttpRequestMessage Build(KeyKind kind, string key)
    {
        switch (kind)
        {
            case KeyKind.Anthropic:
                var claude = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
                {
                    Content = new StringContent("""{"model":"claude-haiku-5-5","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""", System.Text.Encoding.UTF8, "application/json"),
                };
                claude.Headers.Add("x-api-key", key);
                claude.Headers.Add("anthropic-version", "2023-06-01");
                return claude;
            case KeyKind.FishAudio:
                var fish = new HttpRequestMessage(HttpMethod.Get, "https://api.fish.audio/wallet/self/api-credit");
                fish.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                return fish;
            default:
                var gemini = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1");
                gemini.Headers.Add("x-goog-api-key", key);
                return gemini;
        }
    }

    private static KeyTest Read(HttpStatusCode status, string body) => (int)status switch
    {
        >= 200 and < 300 => KeyTest.Works,
        401 or 403 => KeyTest.Wrong,
        402 => KeyTest.NoCredit,
        400 when body.Contains("credit", StringComparison.OrdinalIgnoreCase) => KeyTest.NoCredit,
        400 when body.Contains("API key", StringComparison.OrdinalIgnoreCase) => KeyTest.Wrong,
        _ => KeyTest.CouldNotCheck,
    };
}
