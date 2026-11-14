using System.Text;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;

namespace Mubsir.App.Diagnostics;

internal sealed class FileEventLog : IEventLog, IDisposable
{
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _prefix;
    private readonly IClock _clock;
    private JsonlEventLog? _current;
    private string? _currentName;
    private bool _disposed;

    public FileEventLog(string directory, IClock clock, string prefix = "mubsir")
    {
        _directory = directory;
        _clock = clock;
        _prefix = prefix;
    }

    public long FailedWrites { get; private set; }

    public int Prune(int keepDays = LogRetention.DefaultKeepDays)
    {
        int removed = 0;
        try
        {
            var files = new List<(string Path, DateTimeOffset Date)>();
            foreach (string path in Directory.EnumerateFiles(_directory, "*.jsonl"))
            {
                if (LogRetention.TryParseDate(path, out DateTimeOffset date))
                {
                    files.Add((path, date));
                }
            }

            foreach (string path in LogRetention.FilesToDelete(files, _clock.UtcNow, keepDays))
            {
                try
                {
                    File.Delete(path);
                    removed++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            FailedWrites++;
        }

        return removed;
    }

    public void Write(LogEvent e)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                EnsureFile(e.At)?.Write(e);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                FailedWrites++;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _current?.Dispose();
            _current = null;
        }
    }

    private JsonlEventLog? EnsureFile(DateTimeOffset at)
    {
        string name = FileName(at);
        if (_current is not null && name == _currentName)
        {
            return _current;
        }

        _current?.Dispose();
        _current = null;
        _currentName = name;
        Directory.CreateDirectory(_directory);

        var stream = new FileStream(
            Path.Combine(_directory, name),
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _current = new JsonlEventLog(writer, ownsWriter: true, flushEachLine: true);
        return _current;
    }

    private string FileName(DateTimeOffset at)
    {
        string core = LogRetention.FileNameFor(at);
        return _prefix == "mubsir" ? core : _prefix + core["mubsir".Length..];
    }
}

internal static class EventLogExtensions
{
    public static void Error(this IEventLog log, string code, LogKind kind = LogKind.Error, LogOutcome outcome = LogOutcome.Failed)
    {
        try
        {
            log.Write(new LogEvent(DateTimeOffset.UtcNow, kind, outcome, ErrorCode: code));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    public static void Failure(this IEventLog log, string where, Exception e)
    {
        Error(log, where + ":" + e.GetType().Name);
    }

    public static void Event(this IEventLog log, LogKind kind, LogOutcome outcome = LogOutcome.Ok, string? tool = null, long? latencyMs = null, string? errorCode = null)
    {
        try
        {
            log.Write(new LogEvent(DateTimeOffset.UtcNow, kind, outcome, Tool: tool, LatencyMs: latencyMs, ErrorCode: errorCode));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    public static void Metric(this IEventLog log, string metric, double value)
    {
        try
        {
            log.Write(new LogEvent(DateTimeOffset.UtcNow, LogKind.Metric, Metric: metric, Value: value));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }
}

internal sealed class NullEventLog : IEventLog
{
    public static readonly NullEventLog Instance = new();

    public void Write(LogEvent e)
    {
    }
}
