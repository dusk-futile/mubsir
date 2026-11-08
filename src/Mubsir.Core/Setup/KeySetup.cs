using System.Text.RegularExpressions;

namespace Mubsir.Core.Setup;

public enum KeyKind
{
    Anthropic,
    FishAudio,
    Gemini,
}

/// <summary>Which service is the brain.</summary>
public enum BrainChoice
{
    Gemini,
    Claude,
}

/// <summary>Which service hears what the person says.</summary>
public enum EarsChoice
{
    Gemini,
    Fish,
}

/// <summary>Which keys are saved on this PC.</summary>
public readonly record struct KeysHeld(bool Claude, bool Fish, bool Gemini)
{
    public bool Has(KeyKind kind) => kind switch
    {
        KeyKind.Anthropic => Claude,
        KeyKind.FishAudio => Fish,
        _ => Gemini,
    };
}

/// <summary>
/// The person chooses each service, then gives only that service's key. If the chosen one has no key yet,
/// the other one is used when its key is there, so Mubsir keeps working. Someone who never chose gets what
/// the keys they have suggest: Claude when there's a Claude key, otherwise Gemini.
/// </summary>
public static class Providers
{
    public static KeyKind KeyFor(BrainChoice brain) => brain == BrainChoice.Claude ? KeyKind.Anthropic : KeyKind.Gemini;

    public static KeyKind KeyFor(EarsChoice ears) => ears == EarsChoice.Fish ? KeyKind.FishAudio : KeyKind.Gemini;

    public static BrainChoice Chosen(BrainChoice? chosen, KeysHeld keys) => chosen ?? (keys.Claude ? BrainChoice.Claude : BrainChoice.Gemini);

    public static EarsChoice Chosen(EarsChoice? chosen, KeysHeld keys) => chosen ?? (keys.Fish && !keys.Gemini ? EarsChoice.Fish : EarsChoice.Gemini);

    /// <summary>The brain to use now, or null when there is no key for either.</summary>
    public static BrainChoice? Brain(BrainChoice? chosen, KeysHeld keys)
    {
        BrainChoice first = Chosen(chosen, keys);
        BrainChoice other = first == BrainChoice.Claude ? BrainChoice.Gemini : BrainChoice.Claude;
        return keys.Has(KeyFor(first)) ? first : keys.Has(KeyFor(other)) ? other : null;
    }

    /// <summary>The services to try for hearing, in order: the chosen one, then the other as a fallback.</summary>
    public static IReadOnlyList<EarsChoice> Ears(EarsChoice? chosen, KeysHeld keys)
    {
        EarsChoice first = Chosen(chosen, keys);
        EarsChoice other = first == EarsChoice.Fish ? EarsChoice.Gemini : EarsChoice.Fish;
        return new[] { first, other }.Where(e => keys.Has(KeyFor(e))).ToArray();
    }

    /// <summary>Enough to start: the chosen brain and the chosen hearing both have their keys.</summary>
    public static bool CanStart(BrainChoice? brain, EarsChoice? ears, KeysHeld keys) =>
        keys.Has(KeyFor(Chosen(brain, keys))) && keys.Has(KeyFor(Chosen(ears, keys)));
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

    [GeneratedRegex("^[0-9a-fA-F]{32}$")]
    private static partial Regex FishShape();
}

/// <summary>An estimate of one month, in US dollars. The rates change, so the page that shows it says it's an estimate.</summary>
public sealed record MonthlyCost(int RequestsADay, decimal Brain, decimal Ears, decimal Voice)
{
    public decimal Total => Brain + Ears + Voice;

    public decimal WithFreeVoice => Brain + Ears;
}

/// <summary>What Mubsir might cost a month. The setup window shows the Claude part next to its key.</summary>
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
