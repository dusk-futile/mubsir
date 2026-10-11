using Basira.Core.Logging;

namespace Basira.App.Screen;

/// <summary>Metadata-only logging helpers for the screen, brain and action code. Codes are short identifiers, never content.</summary>
internal static class MetaLog
{
    public static void Fail(this IEventLog log, LogKind kind, string code, string? tool = null, long? latencyMs = null, LogOutcome outcome = LogOutcome.Failed)
    {
        Write(log, new LogEvent(DateTimeOffset.UtcNow, kind, outcome, Tool: tool, ErrorCode: code, LatencyMs: latencyMs));
    }

    public static void Metric(this IEventLog log, string metric, double value)
    {
        Write(log, new LogEvent(DateTimeOffset.UtcNow, LogKind.Metric, Metric: metric, Value: value));
    }

    public static void Ok(this IEventLog log, LogKind kind, string? tool = null, long? latencyMs = null, string? controlType = null)
    {
        Write(log, new LogEvent(DateTimeOffset.UtcNow, kind, LogOutcome.Ok, Tool: tool, ControlType: controlType, LatencyMs: latencyMs));
    }

    /// <summary>A loggable code for an exception: its type name only (messages can hold paths or screen text).</summary>
    public static string Code(Exception e) => e switch
    {
        TimeoutException => "timeout",
        OperationCanceledException => "cancelled",
        UnauthorizedAccessException => "access_denied",
        System.Runtime.InteropServices.COMException com => "com_" + ((uint)com.HResult).ToString("x8", System.Globalization.CultureInfo.InvariantCulture),
        _ => e.GetType().Name,
    };

    private static void Write(IEventLog log, LogEvent e)
    {
        try
        {
            log.Write(e);
        }
        catch (Exception)
        {
            // Logging must never break the app.
        }
    }
}

/// <summary>Used when the host registered no event log.</summary>
internal sealed class NullEventLog : IEventLog
{
    public static readonly NullEventLog Instance = new();

    public void Write(LogEvent e)
    {
    }
}
