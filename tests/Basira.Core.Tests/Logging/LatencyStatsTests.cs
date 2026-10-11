using Basira.Core.Logging;

namespace Basira.Core.Tests.Logging;

public class LatencyStatsTests
{
    [Fact]
    public void P50AndP90_NearestRank()
    {
        var stats = new LatencyStats();
        foreach (int ms in new[] { 900, 100, 500, 300, 700, 200, 800, 400, 1000, 600 })
        {
            stats.Record("voice_release_to_first_audio", ms);
        }

        LatencySummary s = stats.Get("voice_release_to_first_audio")!.Value;

        Assert.Equal(10, s.Count);
        Assert.Equal(500, s.P50);
        Assert.Equal(900, s.P90);
        Assert.Equal(1000, s.Max);
    }

    [Fact]
    public void SingleSample()
    {
        var stats = new LatencyStats();
        stats.Record("hotkey_to_audio", 120);

        LatencySummary s = stats.Get("hotkey_to_audio")!.Value;

        Assert.Equal((1, 120d, 120d, 120d), (s.Count, s.P50, s.P90, s.Max));
    }

    [Fact]
    public void UnknownMetricHasNoSummary()
    {
        Assert.Null(new LatencyStats().Get("nothing"));
    }

    [Fact]
    public void MetricsAreSeparate_AndCaseSensitive()
    {
        var stats = new LatencyStats();
        stats.Record("a", 1);
        stats.Record("a", 3);
        stats.Record("b", 100);
        stats.Record("A", 50);

        Assert.Equal(2, stats.Get("a")!.Value.Count);
        Assert.Equal(1, stats.Get("b")!.Value.Count);
        Assert.Equal(1, stats.Get("A")!.Value.Count);
        Assert.Equal(["A", "a", "b"], stats.Snapshot().Select(x => x.Metric));
    }

    [Fact]
    public void InvalidSamplesAreIgnored()
    {
        var stats = new LatencyStats();
        stats.Record("m", -1);
        stats.Record("m", double.NaN);
        stats.Record("m", double.PositiveInfinity);
        Assert.Null(stats.Get("m"));

        stats.Record("m", 0);
        Assert.Equal(1, stats.Get("m")!.Value.Count);

        Assert.Throws<ArgumentException>(() => stats.Record(" ", 5));
        Assert.Throws<ArgumentNullException>(() => stats.Record(null!, 5));
    }

    [Fact]
    public void KeepsOnlyTheMostRecentSamples()
    {
        var stats = new LatencyStats(maxSamplesPerMetric: 3);
        foreach (int ms in new[] { 9000, 9000, 9000, 10, 20, 30 })
        {
            stats.Record("m", ms);
        }

        LatencySummary s = stats.Get("m")!.Value;

        Assert.Equal(3, s.Count);
        Assert.Equal(20, s.P50);
        Assert.Equal(30, s.Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => new LatencyStats(0));
    }

    [Fact]
    public void Percentile_EdgeCases()
    {
        double[] sorted = [1, 2, 3, 4];

        Assert.Equal(1, LatencyStats.Percentile(sorted, 0));
        Assert.Equal(2, LatencyStats.Percentile(sorted, 50));
        Assert.Equal(4, LatencyStats.Percentile(sorted, 90));
        Assert.Equal(4, LatencyStats.Percentile(sorted, 100));
        Assert.Throws<ArgumentException>(() => LatencyStats.Percentile([], 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => LatencyStats.Percentile(sorted, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => LatencyStats.Percentile(sorted, -1));
    }

    [Fact]
    public void ToLogEvents_EmitsP50P90AndCountPerMetric()
    {
        var at = new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        var stats = new LatencyStats();
        stats.Record("typed_qa_first_audio", 2000);
        stats.Record("typed_qa_first_audio", 3000);

        IReadOnlyList<LogEvent> events = stats.ToLogEvents(at);

        Assert.All(events, e => Assert.Equal(LogKind.Metric, e.Kind));
        Assert.Equal(
            [("typed_qa_first_audio.p50", 2000d), ("typed_qa_first_audio.p90", 3000d), ("typed_qa_first_audio.count", 2d)],
            events.Select(e => (e.Metric!, e.Value!.Value)));
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var stats = new LatencyStats();
        stats.Record("m", 1);
        stats.Clear();

        Assert.Empty(stats.Snapshot());
    }

    [Fact]
    public async Task ConcurrentRecording()
    {
        var stats = new LatencyStats(maxSamplesPerMetric: 100_000);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                stats.Record(t % 2 == 0 ? "even" : "odd", i);
                _ = stats.Get("even");
            }
        })));

        Assert.Equal(4000, stats.Get("even")!.Value.Count);
        Assert.Equal(4000, stats.Get("odd")!.Value.Count);
    }
}
