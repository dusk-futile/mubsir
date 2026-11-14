namespace Mubsir.Core.Common;

public enum Lang
{
    Arabic,
    English,
}

public static class LangExtensions
{
    public static string Tag(this Lang lang) => lang == Lang.Arabic ? "ar" : "en";

    public static Lang Other(this Lang lang) => lang == Lang.Arabic ? Lang.English : Lang.Arabic;
}

public enum Verbosity
{
    Brief,
    Normal,
    Detailed,
}

public enum ReplyStyle
{
    SimpleMsa,
    Dialect,
}

public enum Earcon
{
    Ready,
    ListeningStart,
    ListeningEnd,
    Thinking,
    Done,
    Error,
    NeedsConfirmation,
    LayerOpen,
    LayerClose,
    AiContent,
    BankMode,
    Paused,
}

public interface IClock
{
    long NowMs { get; }

    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public long NowMs => Environment.TickCount64;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed record UsageRecord(
    DateTimeOffset At,
    string Model,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens,
    long AudioInputTokens = 0,
    int WebSearches = 0);
