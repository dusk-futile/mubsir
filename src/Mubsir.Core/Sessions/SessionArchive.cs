using System.Globalization;
using System.Text;
using Mubsir.Core.Policy;
using Mubsir.Core.Text;

namespace Mubsir.Core.Sessions;

/// <summary>
/// Saves conversations one file a day, in a folder the user owns: plain text they can open, or, if they chose it,
/// encrypted lines only this Windows account can read. It's written to as it goes, so a crash loses at most one
/// line. Card numbers, codes and the like are hidden first.
/// </summary>
public sealed class SessionArchive(string folder, Func<DateTimeOffset> now, Func<string, string>? seal = null, Func<string, string?>? unseal = null)
{
    private const string DayFormat = "yyyy-MM-dd";
    private const string SealedExtension = ".chat";
    private const int DaysSearched = 90;

    private readonly object _gate = new();

    public string Folder => folder;

    /// <summary>The plain file for a day. It's a stable name so "open today's chat" always finds it.</summary>
    public string FileFor(DateTimeOffset when) =>
        Path.Combine(folder, when.ToString(DayFormat, CultureInfo.InvariantCulture) + ".md");

    /// <summary>Never throws: saving a chat must not be able to break the thing being said.</summary>
    public bool Add(string who, string text, bool encrypt = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        DateTimeOffset when = now();
        string line = $"**{when.ToString("HH:mm", CultureInfo.InvariantCulture)} {who}:** {Redactor.Redact(text.Trim()).ReplaceLineEndings(" ")}";
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(folder);
                if (encrypt && seal is not null)
                {
                    File.AppendAllText(Path.ChangeExtension(FileFor(when), SealedExtension), seal(line) + "\n");
                }
                else
                {
                    File.AppendAllText(FileFor(when), line + "\n\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Every line saved on one day, plain and encrypted. A line that can't be opened is left out.</summary>
    public IReadOnlyList<string> Lines(DateTimeOffset day)
    {
        var lines = new List<string>();
        try
        {
            lock (_gate)
            {
                string plain = FileFor(day);
                if (File.Exists(plain))
                {
                    lines.AddRange(File.ReadLines(plain).Where(l => l.Length > 0));
                }

                string kept = Path.ChangeExtension(plain, SealedExtension);
                if (unseal is not null && File.Exists(kept))
                {
                    lines.AddRange(File.ReadLines(kept).Select(unseal).OfType<string>());
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return lines;
    }

    /// <summary>The days that have a saved chat, newest first.</summary>
    public IReadOnlyList<DateTimeOffset> Days(int count)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder)
                    .Where(f => Path.GetExtension(f) is ".md" or SealedExtension)
                    .Select(f => DateTimeOffset.TryParseExact(Path.GetFileNameWithoutExtension(f), DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset day) ? day : (DateTimeOffset?)null)
                    .OfType<DateTimeOffset>()
                    .Distinct()
                    .OrderDescending()
                    .Take(Math.Max(0, count))
                    .ToArray()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>"today", "yesterday" or a date as 2026-11-13. Null for anything else.</summary>
    public static DateTimeOffset? ParseDay(string? said, DateTimeOffset today) => said?.Trim().ToLowerInvariant() switch
    {
        "today" => today,
        "yesterday" => today.AddDays(-1),
        { } date when DateTimeOffset.TryParseExact(date, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset day) => day,
        _ => null,
    };

    /// <summary>
    /// What was said earlier, for "what did we do yesterday?" and "when did I ask about the invoice?". A day gives
    /// that day's lines. Words give the lines holding all of them, from any day, newest first. Never more than
    /// <paramref name="maxChars"/>, so it stays cheap to send.
    /// </summary>
    public string Recall(DateTimeOffset? day, string? query, int maxChars = 6000)
    {
        string[] terms = ArabicFolding.Fold(query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<DateTimeOffset> days = day is { } one ? [one] : Days(terms.Length > 0 ? DaysSearched : 1);
        var found = new StringBuilder();
        foreach (DateTimeOffset d in days)
        {
            string date = d.ToString(DayFormat, CultureInfo.InvariantCulture);
            foreach (string line in Lines(d))
            {
                if (terms.Length > 0 && !terms.All(ArabicFolding.Fold(line).Contains))
                {
                    continue;
                }

                if (found.Length + line.Length > maxChars)
                {
                    return found.Append("(more was said; ask about one part of it)").ToString();
                }

                found.Append(date).Append(' ').Append(line).Append('\n');
            }
        }

        return found.Length > 0 ? found.ToString().TrimEnd()
            : terms.Length > 0 ? "No saved chat has those words."
            : "No chat was saved for that day.";
    }
}
