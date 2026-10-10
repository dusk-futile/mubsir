namespace Basira.Core.Common;

/// <summary>Token and request usage of one cloud call. Metadata only, never content.</summary>
public sealed record UsageRecord(
    DateTimeOffset At,
    string Model,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens,
    long AudioInputTokens = 0,
    int WebSearches = 0);
