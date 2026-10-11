using System.Text;
using System.Text.Json;
using Basira.Core.Logging;

namespace Basira.Core.Tests.Logging;

public class JsonlEventLogTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 11, 8, 30, 15, 250, TimeSpan.Zero);

    [Fact]
    public void WritesOneJsonObjectPerLine()
    {
        var sw = new StringWriter();
        using (var log = new JsonlEventLog(sw, ownsWriter: false))
        {
            log.Write(new LogEvent(At, LogKind.Startup));
            log.Write(new LogEvent(At, LogKind.ToolCall, LogOutcome.Blocked, Tool: "press_keys", ControlType: "Edit", LatencyMs: 42));
            log.Write(new LogEvent(At, LogKind.BrainRequest, InputTokens: 1200, OutputTokens: 80, CostUsd: 0.00016m, LatencyMs: 900));
        }

        string[] lines = sw.ToString().Split('\n');
        Assert.Equal(4, lines.Length); // three lines plus the empty tail after the last newline
        Assert.Equal(string.Empty, lines[3]);

        using JsonDocument second = JsonDocument.Parse(lines[1]);
        JsonElement r = second.RootElement;
        Assert.Equal("ToolCall", r.GetProperty("kind").GetString());
        Assert.Equal("Blocked", r.GetProperty("outcome").GetString());
        Assert.Equal("press_keys", r.GetProperty("tool").GetString());
        Assert.Equal("Edit", r.GetProperty("controlType").GetString());
        Assert.Equal(42, r.GetProperty("latencyMs").GetInt64());
        Assert.False(r.TryGetProperty("costUsd", out _)); // nulls are left out

        using JsonDocument third = JsonDocument.Parse(lines[2]);
        Assert.Equal(0.00016m, third.RootElement.GetProperty("costUsd").GetDecimal());
        Assert.Equal(1200, third.RootElement.GetProperty("inputTokens").GetInt64());
        Assert.Equal(At, third.RootElement.GetProperty("at").GetDateTimeOffset());
    }

    [Fact]
    public void MetricEvents()
    {
        string json = JsonlEventLog.ToJson(new LogEvent(At, LogKind.Metric, Metric: "idle_ram_mb", Value: 61.5));

        Assert.Contains("\"metric\":\"idle_ram_mb\"", json);
        Assert.Contains("\"value\":61.5", json);
    }

    [Fact]
    public void SanitizedFieldsAreWrittenAsInvalid()
    {
        string json = JsonlEventLog.ToJson(new LogEvent(At, LogKind.Error, LogOutcome.Failed, ErrorCode: "could not open C:\\secret.docx"));

        Assert.Contains("\"errorCode\":\"invalid\"", json);
        Assert.DoesNotContain("secret", json);
    }

    [Fact]
    public void ConcurrentWritesProduceWholeLines()
    {
        var sw = new StringWriter();
        using var log = new JsonlEventLog(sw, ownsWriter: false, flushEachLine: false);

        Parallel.For(0, 2000, i => log.Write(new LogEvent(At, LogKind.Hotkey, LatencyMs: i)));

        string[] lines = sw.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2000, lines.Length);
        var seen = new HashSet<long>();
        foreach (string line in lines)
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            Assert.True(seen.Add(doc.RootElement.GetProperty("latencyMs").GetInt64()));
        }
    }

    [Fact]
    public void FlushesEachLineByDefault()
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 4096);
        using var log = new JsonlEventLog(writer);

        log.Write(new LogEvent(At, LogKind.Startup));

        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void DisposeOwnsTheWriterOnlyWhenAsked()
    {
        var owned = new TrackingWriter();
        new JsonlEventLog(owned).Dispose();
        Assert.True(owned.Disposed);

        var borrowed = new TrackingWriter();
        new JsonlEventLog(borrowed, ownsWriter: false).Dispose();
        Assert.False(borrowed.Disposed);
        Assert.True(borrowed.Flushed);
    }

    [Fact]
    public void WritesAfterDisposeAreDropped_AndDisposeIsIdempotent()
    {
        var sw = new StringWriter();
        var log = new JsonlEventLog(sw, ownsWriter: false);
        log.Dispose();
        log.Dispose();

        log.Write(new LogEvent(At, LogKind.Shutdown));

        Assert.Equal(string.Empty, sw.ToString());
    }

    [Fact]
    public void IoFailuresAreCountedNotThrown()
    {
        var failing = new FailingWriter();
        using var log = new JsonlEventLog(failing, ownsWriter: false);

        log.Write(new LogEvent(At, LogKind.Startup));
        log.Write(new LogEvent(At, LogKind.Shutdown));

        Assert.Equal(2, log.FailedWrites);
    }

    [Fact]
    public void ImplementsIEventLog()
    {
        var sw = new StringWriter();
        IEventLog log = new JsonlEventLog(sw, ownsWriter: false);

        log.Write(new LogEvent(At, LogKind.Budget, CostUsd: 6.4m));

        Assert.Contains("\"kind\":\"Budget\"", sw.ToString());
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonlEventLog(null!));
        Assert.Throws<ArgumentNullException>(() => JsonlEventLog.ToJson(null!));
    }

    private sealed class TrackingWriter : StringWriter
    {
        public bool Disposed { get; private set; }

        public bool Flushed { get; private set; }

        public override void Flush()
        {
            Flushed = true;
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FailingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new IOException("disk full");

        public override void Write(string? value) => throw new IOException("disk full");
    }
}
