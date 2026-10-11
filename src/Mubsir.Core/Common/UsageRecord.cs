namespace Mubsir.Core.Common;

public sealed record UsageRecord(
    DateTimeOffset At,
    string Model,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens,
    long AudioInputTokens = 0,
    int WebSearches = 0);
