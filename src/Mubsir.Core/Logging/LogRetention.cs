using System.Globalization;
using System.Text.RegularExpressions;

namespace Mubsir.Core.Logging;

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
