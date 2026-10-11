using Basira.Core.Logging;

namespace Basira.Core.Tests.Logging;

public class LogRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);

    private static (string, DateTimeOffset) File(int daysAgo) =>
        ($"logs/basira-{Now.AddDays(-daysAgo):yyyy-MM-dd}.jsonl", Now.AddDays(-daysAgo));

    [Fact]
    public void DeletesFilesOlderThanFourteenDays_OldestFirst()
    {
        var files = new[] { File(0), File(20), File(13), File(15), File(14), File(40) };

        IReadOnlyList<string> delete = LogRetention.FilesToDelete(files, Now);

        Assert.Equal([File(40).Item1, File(20).Item1, File(15).Item1], delete);
    }

    [Fact]
    public void AFileExactlyAtTheLimitIsKept()
    {
        Assert.Empty(LogRetention.FilesToDelete([File(14)], Now));
        Assert.Single(LogRetention.FilesToDelete([("x", Now.AddDays(-14).AddTicks(-1))], Now));
    }

    [Fact]
    public void CustomKeepDays()
    {
        var files = new[] { File(1), File(2), File(3) };

        Assert.Equal([File(3).Item1], LogRetention.FilesToDelete(files, Now, keepDays: 2));
        Assert.Equal(3, LogRetention.FilesToDelete(files, Now, keepDays: 0).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => LogRetention.FilesToDelete(files, Now, keepDays: -1));
    }

    [Fact]
    public void FutureDatedAndEmptyPathsAreKept()
    {
        var files = new[] { ("future.jsonl", Now.AddDays(3)), (string.Empty, Now.AddDays(-30)), (null!, Now.AddDays(-30)) };

        Assert.Empty(LogRetention.FilesToDelete(files, Now));
        Assert.Empty(LogRetention.FilesToDelete([], Now));
        Assert.Throws<ArgumentNullException>(() => LogRetention.FilesToDelete(null!, Now));
    }

    [Fact]
    public void TimeZonesAreCompared_AsInstants()
    {
        // 15 days ago at UTC+3 is still more than 14 days ago.
        var file = ("a.jsonl", new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.FromHours(3)));

        Assert.Single(LogRetention.FilesToDelete([file], Now));
    }

    [Fact]
    public void FileNames_AreDailyInUtc()
    {
        Assert.Equal("basira-2026-10-11.jsonl", LogRetention.FileNameFor(new DateTimeOffset(2026, 10, 11, 23, 59, 0, TimeSpan.Zero)));

        // 01:00 on Oct 12 in Cairo (UTC+3) is still Oct 11 in UTC.
        Assert.Equal("basira-2026-10-11.jsonl", LogRetention.FileNameFor(new DateTimeOffset(2026, 10, 12, 1, 0, 0, TimeSpan.FromHours(3))));
    }

    [Theory]
    [InlineData("basira-2026-10-11.jsonl", 2026, 10, 11)]
    [InlineData("C:/Users/x/AppData/Local/Basira/logs/basira-2026-01-02.jsonl", 2026, 1, 2)]
    [InlineData("basira-2025-12-31.jsonl.old", 2025, 12, 31)]
    public void TryParseDate_ReadsTheDayFromAName(string name, int y, int m, int d)
    {
        Assert.True(LogRetention.TryParseDate(name, out DateTimeOffset date));
        Assert.Equal(new DateTimeOffset(y, m, d, 0, 0, 0, TimeSpan.Zero), date);
    }

    [Theory]
    [InlineData("basira.jsonl")]
    [InlineData("basira-2026-13-01.jsonl")]
    [InlineData("basira-2026-02-30.jsonl")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseDate_RejectsNamesWithoutAValidDate(string? name)
    {
        Assert.False(LogRetention.TryParseDate(name, out _));
    }

    [Fact]
    public void FileNameAndParseRoundTrip()
    {
        string name = LogRetention.FileNameFor(Now);

        Assert.True(LogRetention.TryParseDate(name, out DateTimeOffset date));
        Assert.Equal(Now.UtcDateTime.Date, date.UtcDateTime);
    }
}
