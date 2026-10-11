using Mubsir.Core.Logging;

namespace Mubsir.App.Screen;

internal static class MetaLog
{
    public static void Fail(this IEventLog log, LogKind kind, string code, string? tool = null, long? latencyMs = null, LogOutcome outcome = LogOutcome.Failed)
    {
        Write(log, new LogEvent(DateTimeOffset.UtcNow, kind, outcome, Tool: tool, ErrorCode: code, LatencyMs: latencyMs));
    }

    public static void Ok(this IEventLog log, LogKind kind, string? tool = null, long? latencyMs = null, string? controlType = null)
    {
        Write(log, new LogEvent(DateTimeOffset.UtcNow, kind, LogOutcome.Ok, Tool: tool, ControlType: controlType, LatencyMs: latencyMs));
    }

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
        }
    }
}
