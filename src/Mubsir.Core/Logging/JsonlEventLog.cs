using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mubsir.Core.Logging;

public interface IEventLog
{
    void Write(LogEvent e);
}

public sealed class JsonlEventLog : IEventLog, IDisposable
{
    private readonly object _gate = new();
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;
    private readonly bool _flushEachLine;
    private bool _disposed;
    private long _failedWrites;

    public JsonlEventLog(TextWriter writer, bool ownsWriter = true, bool flushEachLine = true)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _ownsWriter = ownsWriter;
        _flushEachLine = flushEachLine;
    }

    public long FailedWrites => Interlocked.Read(ref _failedWrites);

    public static string ToJson(LogEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return JsonSerializer.Serialize(e, LogJsonContext.Default.LogEvent);
    }

    public void Write(LogEvent e)
    {
        string line = ToJson(e);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _writer.Write(line);
                _writer.Write('\n');
                if (_flushEachLine)
                {
                    _writer.Flush();
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or UnauthorizedAccessException)
            {
                Interlocked.Increment(ref _failedWrites);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _writer.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                Interlocked.Increment(ref _failedWrites);
            }

            if (_ownsWriter)
            {
                _writer.Dispose();
            }
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LogEvent))]
internal sealed partial class LogJsonContext : JsonSerializerContext;

public static partial class LogRetention
{
    public const int DefaultKeepDays = 14;

    public static IReadOnlyList<string> FilesToDelete(IEnumerable<(string Path, DateTimeOffset Date)> files, DateTimeOffset now, int keepDays = DefaultKeepDays)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(keepDays);
        DateTimeOffset cutoff = now - TimeSpan.FromDays(keepDays);
        return files
            .Where(f => !string.IsNullOrEmpty(f.Path) && f.Date < cutoff)
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Path, StringComparer.Ordinal)
            .Select(f => f.Path)
            .ToList();
    }

    public static string FileNameFor(DateTimeOffset at) =>
    "mubsir-" + at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl";

    public static bool TryParseDate(string? fileName, out DateTimeOffset date)
    {
        date = default;
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        Match m = DatePattern().Match(Path.GetFileName(fileName));
        if (!m.Success
            || !DateTime.TryParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime day))
        {
            return false;
        }

        date = new DateTimeOffset(DateTime.SpecifyKind(day, DateTimeKind.Utc));
        return true;
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DatePattern();
}
