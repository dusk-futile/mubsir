using System.Collections.ObjectModel;
using Mubsir.Core.Common;

namespace Mubsir.Core.Cost;

public sealed record ModelPrice(
    string ModelId,
    decimal InputPerMTok,
    decimal CacheReadPerMTok,
    decimal CacheWritePerMTok,
    decimal OutputPerMTok,
    decimal AudioInputPerMTok = 0,
    long LongPromptThresholdTokens = 0,
    decimal LongPromptMultiplier = 1);

public static class PriceTable
{
    public const decimal WebSearchPerCall = 0.01m;

    private const decimal TokensPerUnit = 1_000_000m;

    public static IReadOnlyDictionary<string, ModelPrice> Defaults { get; } = new ReadOnlyDictionary<string, ModelPrice>(
    new Dictionary<string, ModelPrice>(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-haiku-5-5"] = new("claude-haiku-5-5", 0.10m, 0.01m, 0.125m, 0.50m, LongPromptThresholdTokens: 100_000, LongPromptMultiplier: 5m),
        ["claude-sonnet-5-5"] = new("claude-sonnet-5-5", 2.00m, 0.20m, 2.50m, 10.00m),
        ["gemini-3.5-flash-lite"] = new("gemini-3.5-flash-lite", 0.30m, 0.03m, 0m, 2.50m, AudioInputPerMTok: 0.30m),
    });

    public static bool TryFind(IReadOnlyDictionary<string, ModelPrice> prices, string? modelId, out ModelPrice price)
    {
        ArgumentNullException.ThrowIfNull(prices);
        price = null!;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        modelId = modelId.Trim();
        if (prices.TryGetValue(modelId, out ModelPrice? exact))
        {
            price = exact;
            return true;
        }

        ModelPrice? best = null;
        int bestLength = 0;
        foreach ((string id, ModelPrice candidate) in prices)
        {
            if (id.Length > bestLength
                && modelId.Length > id.Length + 1
                && modelId.StartsWith(id, StringComparison.OrdinalIgnoreCase)
                && modelId[id.Length] is '-' or '@')
            {
                best = candidate;
                bestLength = id.Length;
            }
        }

        if (best is null)
        {
            return false;
        }

        price = best;
        return true;
    }

    public static decimal Compute(UsageRecord usage, ModelPrice price)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(price);

        long input = Math.Max(0, usage.InputTokens);
        long cacheRead = Math.Max(0, usage.CacheReadTokens);
        long cacheWrite = Math.Max(0, usage.CacheWriteTokens);
        long output = Math.Max(0, usage.OutputTokens);
        long audio = Math.Max(0, usage.AudioInputTokens);

        decimal audioRate = price.AudioInputPerMTok > 0 ? price.AudioInputPerMTok : price.InputPerMTok;
        decimal tokens = (input * price.InputPerMTok)
            + (cacheRead * price.CacheReadPerMTok)
            + (cacheWrite * price.CacheWritePerMTok)
            + (output * price.OutputPerMTok)
            + (audio * audioRate);
        decimal cost = tokens / TokensPerUnit;

        long promptTokens = input + cacheRead + cacheWrite + audio;
        if (price.LongPromptThresholdTokens > 0 && promptTokens > price.LongPromptThresholdTokens)
        {
            cost *= price.LongPromptMultiplier;
        }

        return cost + (Math.Max(0, usage.WebSearches) * WebSearchPerCall);
    }
}
