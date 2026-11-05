using System.Text.RegularExpressions;

namespace Mubsir.Core.Setup;

public enum KeyKind
{
    Anthropic,
    FishAudio,
    Gemini,
}

/// <summary>Recognises a key the user copied, so nobody has to type or read one out.</summary>
public static partial class KeySetup
{
    public const string AnthropicKeysPage = "https://console.anthropic.com/settings/keys";
    public const string FishKeysPage = "https://fish.audio/app/api-keys";
    public const string GeminiKeysPage = "https://aistudio.google.com/apikey";

    /// <summary>What a copied piece of text is, if it's a key at all. A Fish key has no prefix: 32 hex characters.</summary>
    public static KeyKind? Recognize(string? copied)
    {
        string text = (copied ?? string.Empty).Trim();
        if (text.Length is < 20 or > 300 || text.Any(char.IsWhiteSpace))
        {
            return null;
        }

        if (text.StartsWith("sk-ant-", StringComparison.Ordinal))
        {
            return KeyKind.Anthropic;
        }

        if (text.StartsWith("AIza", StringComparison.Ordinal))
        {
            return KeyKind.Gemini;
        }

        return FishShape().IsMatch(text) ? KeyKind.FishAudio : null;
    }

    /// <summary>Keys that are enough to start. A free Gemini key alone is enough for hearing and the brain, and voice falls back to free ones.</summary>
    public static bool CanStart(bool anthropic, bool fish, bool gemini) => gemini || (anthropic && fish);

    /// <summary>What to ask for next, or null when nothing is missing.</summary>
    public static KeyKind? NextNeeded(bool anthropic, bool fish, bool gemini)
    {
        if (gemini)
        {
            return fish ? null : KeyKind.FishAudio;
        }

        return anthropic && fish ? null : KeyKind.Gemini;
    }

    [GeneratedRegex("^[0-9a-fA-F]{32}$")]
    private static partial Regex FishShape();
}

/// <summary>An estimate of one month, in US dollars. The rates change, so the page that shows it says it's an estimate.</summary>
public sealed record MonthlyCost(int RequestsADay, decimal Brain, decimal Ears, decimal Voice)
{
    public decimal Total => Brain + Ears + Voice;

    public decimal WithFreeVoice => Brain + Ears;
}

public static class CostGuide
{
    // Rates on 2026-11-05. Haiku 5.5 from PriceTable, Fish TTS from its pricing page, Fish speech to text from third parties.
    private const decimal HaikuInputPerMillion = 0.10m;
    private const decimal HaikuCachedPerMillion = 0.01m;
    private const decimal HaikuOutputPerMillion = 0.50m;
    private const decimal FishVoicePerMillionBytes = 15m;
    private const decimal FishEarsPerHour = 0.36m;

    // One typical request: a few steps, most of the prompt cached, ten seconds of talking, a short spoken answer.
    private const int FreshTokens = 8_000;
    private const int CachedTokens = 12_000;
    private const int OutputTokens = 300;
    private const int SecondsSpoken = 10;
    private const int ReplyBytes = 300;
    private const int Days = 30;

    /// <summary>A 300-page book holds about 600,000 Arabic letters, which is 1.2 million bytes.</summary>
    public const decimal ArabicBookOnFishVoice = 1.2m * FishVoicePerMillionBytes;

    public static MonthlyCost Estimate(int requestsADay)
    {
        decimal requests = requestsADay * Days;
        decimal brain = requests * ((FreshTokens * HaikuInputPerMillion) + (CachedTokens * HaikuCachedPerMillion) + (OutputTokens * HaikuOutputPerMillion)) / 1_000_000m;
        decimal ears = requests * SecondsSpoken / 3600m * FishEarsPerHour;
        decimal voice = requests * ReplyBytes * FishVoicePerMillionBytes / 1_000_000m;
        return new MonthlyCost(requestsADay, decimal.Round(brain, 2), decimal.Round(ears, 2), decimal.Round(voice, 2));
    }
}
