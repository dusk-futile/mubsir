using System.Globalization;
using System.Text.RegularExpressions;

namespace Basira.Core.Logging;

/// <summary>Daily log files and how long they are kept.</summary>
public static partial class LogRetention
{
    public const int DefaultKeepDays = 14;

    /// <summary>
    /// Files older than <paramref name="keepDays"/> days at <paramref name="now"/>, oldest first.
    /// A file exactly <paramref name="keepDays"/> days old is kept; future-dated files are kept.
    /// </summary>
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

    /// <summary>"basira-2026-10-10.jsonl" for the UTC day of <paramref name="at"/>.</summary>
    public static string FileNameFor(DateTimeOffset at) =>
        "basira-" + at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl";

    /// <summary>Reads the yyyy-MM-dd date (UTC midnight) from a log file name or path.</summary>
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
