using System.Text.Json;
using System.Text.Json.Serialization;

namespace Basira.Core.Logging;

/// <summary>Destination for metadata-only events.</summary>
public interface IEventLog
{
    void Write(LogEvent e);
}

/// <summary>
/// Writes one JSON object per line to a caller-supplied <see cref="TextWriter"/>. Thread-safe.
/// Logging never throws into the app: I/O failures are counted in <see cref="FailedWrites"/>.
/// </summary>
public sealed class JsonlEventLog : IEventLog, IDisposable
{
    private readonly object _gate = new();
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;
    private readonly bool _flushEachLine;
    private bool _disposed;
    private long _failedWrites;

    /// <param name="writer">Destination; disposed with this log when <paramref name="ownsWriter"/> is true.</param>
    /// <param name="ownsWriter">Dispose <paramref name="writer"/> on <see cref="Dispose"/>.</param>
    /// <param name="flushEachLine">Flush after every line so a crash loses nothing.</param>
    public JsonlEventLog(TextWriter writer, bool ownsWriter = true, bool flushEachLine = true)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _ownsWriter = ownsWriter;
        _flushEachLine = flushEachLine;
    }

    public long FailedWrites => Interlocked.Read(ref _failedWrites);

    /// <summary>The JSON line (without newline) written for <paramref name="e"/>.</summary>
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
