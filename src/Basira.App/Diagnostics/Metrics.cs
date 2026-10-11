using System.Collections.Concurrent;
using System.Diagnostics;
using Basira.Core.Common;
using Basira.Core.Logging;

namespace Basira.App.Diagnostics;

/// <summary>
/// Latency and memory metrics, logged as metadata from day one: key press or talk-key release to first audio,
/// cold start, and idle RAM every 10 minutes. Percentiles come from Core's <see cref="LatencyStats"/>.
/// </summary>
internal sealed class Metrics : IDisposable
{
    public const string KeyToFirstAudio = "key_to_first_audio_ms";
    public const string ReleaseToFirstAudio = "release_to_first_audio_ms";
    public const string ColdStart = "cold_start_ms";
    public const string IdleRam = "idle_ram_mb";
    public const string TranscribeMs = "transcribe_ms";

    private const long ProbeExpiryMs = 30_000;

    private readonly LatencyStats _stats = new();
    private readonly ConcurrentDictionary<string, long> _armed = new(StringComparer.Ordinal);
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private Timer? _ramTimer;

    public Metrics(IEventLog log, IClock clock)
    {
        _log = log;
        _clock = clock;
    }

    /// <summary>When set, RAM is sampled only while this returns true (nothing being spoken or run).</summary>
    public Func<bool>? IsIdle { get; set; }

    public void Record(string metric, double valueMs)
    {
        _stats.Record(metric, valueMs);
        _log.Metric(metric, Math.Round(valueMs));
    }

    /// <summary>Starts a "to first audio" measurement; the next <see cref="OnAudioStarted"/> completes it.</summary>
    public void Arm(string metric) => _armed[metric] = _clock.NowMs;

    public void Disarm(string metric) => _armed.TryRemove(metric, out _);

    /// <summary>Basira started producing sound: completes every armed measurement.</summary>
    public void OnAudioStarted()
    {
        long now = _clock.NowMs;
        foreach (string metric in _armed.Keys)
        {
            if (_armed.TryRemove(metric, out long start) && now - start <= ProbeExpiryMs)
            {
                Record(metric, now - start);
            }
        }
    }

    public void StartRamSampling(TimeSpan interval)
    {
        _ramTimer = new Timer(_ => SampleRam(), null, interval, interval);
    }

    /// <summary>Writes p50/p90/max per metric (e.g. at shutdown).</summary>
    public void LogSummaries()
    {
        foreach (LogEvent e in _stats.ToLogEvents(_clock.UtcNow))
        {
            _log.Write(e);
        }
    }

    public void Dispose() => _ramTimer?.Dispose();

    private void SampleRam()
    {
        try
        {
            if (IsIdle is { } idle && !idle())
            {
                return;
            }

            using var process = Process.GetCurrentProcess();
            _log.Metric(IdleRam, Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 1));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("ram_sample", e);
        }
    }
}
