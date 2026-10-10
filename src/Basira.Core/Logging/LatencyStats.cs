namespace Basira.Core.Logging;

/// <summary>Summary of one metric's recent samples. Percentiles use the nearest-rank method.</summary>
public readonly record struct LatencySummary(string Metric, int Count, double P50, double P90, double Max);

/// <summary>
/// Latency samples per metric name (e.g. "release_to_first_audio") for the milestone metrics gates.
/// Keeps the most recent samples per metric (bounded memory). Thread-safe.
/// </summary>
public sealed class LatencyStats
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Dictionary<string, Ring> _metrics = new(StringComparer.Ordinal);

    public LatencyStats(int maxSamplesPerMetric = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSamplesPerMetric);
        _capacity = maxSamplesPerMetric;
    }

    /// <summary>Adds a sample in milliseconds. Negative, NaN and infinite values are ignored.</summary>
    public void Record(string metric, double valueMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        if (!double.IsFinite(valueMs) || valueMs < 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!_metrics.TryGetValue(metric, out Ring? ring))
            {
                ring = new Ring(_capacity);
                _metrics[metric] = ring;
            }

            ring.Add(valueMs);
        }
    }

    /// <summary>Summary for <paramref name="metric"/>, or null when it has no samples.</summary>
    public LatencySummary? Get(string metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        lock (_gate)
        {
            return _metrics.TryGetValue(metric, out Ring? ring) ? Summarize(metric, ring) : null;
        }
    }

    /// <summary>All metrics, sorted by name.</summary>
    public IReadOnlyList<LatencySummary> Snapshot()
    {
        lock (_gate)
        {
            return _metrics
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => Summarize(kv.Key, kv.Value))
                .ToList();
        }
    }

    /// <summary>Metric log events ("name.p50", "name.p90", "name.count") for the metadata log.</summary>
    public IReadOnlyList<LogEvent> ToLogEvents(DateTimeOffset at)
    {
        var events = new List<LogEvent>();
        foreach (LatencySummary s in Snapshot())
        {
            events.Add(new LogEvent(at, LogKind.Metric, Metric: s.Metric + ".p50", Value: s.P50));
            events.Add(new LogEvent(at, LogKind.Metric, Metric: s.Metric + ".p90", Value: s.P90));
            events.Add(new LogEvent(at, LogKind.Metric, Metric: s.Metric + ".count", Value: s.Count));
        }

        return events;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _metrics.Clear();
        }
    }

    /// <summary>Nearest-rank percentile of an ascending list (<paramref name="p"/> in 0–100).</summary>
    public static double Percentile(IReadOnlyList<double> sortedAscending, double p)
    {
        ArgumentNullException.ThrowIfNull(sortedAscending);
        if (sortedAscending.Count == 0)
        {
            throw new ArgumentException("No samples.", nameof(sortedAscending));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(p, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(p, 100);
        int rank = (int)Math.Ceiling(p / 100 * sortedAscending.Count);
        return sortedAscending[Math.Clamp(rank, 1, sortedAscending.Count) - 1];
    }

    private static LatencySummary Summarize(string metric, Ring ring)
    {
        double[] sorted = ring.ToArray();
        Array.Sort(sorted);
        return new LatencySummary(metric, sorted.Length, Percentile(sorted, 50), Percentile(sorted, 90), sorted[^1]);
    }

    private sealed class Ring(int capacity)
    {
        private readonly double[] _items = new double[capacity];
        private int _next;
        private int _count;

        public void Add(double value)
        {
            _items[_next] = value;
            _next = (_next + 1) % _items.Length;
            _count = Math.Min(_count + 1, _items.Length);
        }

        public double[] ToArray() => _items.AsSpan(0, _count).ToArray();
    }
}
