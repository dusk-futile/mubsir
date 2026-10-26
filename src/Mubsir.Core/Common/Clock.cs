namespace Mubsir.Core.Common;

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
