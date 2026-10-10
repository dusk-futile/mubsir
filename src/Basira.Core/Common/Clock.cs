namespace Basira.Core.Common;

/// <summary>Time source, injectable so timing logic (double-tap, holds, timeouts) is testable.</summary>
public interface IClock
{
    /// <summary>Monotonic milliseconds.</summary>
    long NowMs { get; }

    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public long NowMs => Environment.TickCount64;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Clock for tests: time only moves when told to.</summary>
public sealed class ManualClock : IClock
{
    public ManualClock(long startMs = 0, DateTimeOffset? utc = null)
    {
        NowMs = startMs;
        UtcNow = utc ?? new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    }

    public long NowMs { get; private set; }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(long ms)
    {
        NowMs += ms;
        UtcNow = UtcNow.AddMilliseconds(ms);
    }
}
