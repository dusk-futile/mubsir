using System.Globalization;
using System.Text;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Sessions;

/// <summary>
/// Saves conversations as plain text the user can open, one file a day, in a folder they own. It's written to as
/// it goes, so a crash loses at most one line. Card numbers, codes and the like are hidden first.
/// </summary>
public sealed class SessionArchive(string folder, Func<DateTimeOffset> now)
{
    private readonly object _gate = new();

    public string Folder => folder;

    /// <summary>The file for today. It's a stable name so "open today's chat" always finds it.</summary>
    public string FileFor(DateTimeOffset when) =>
        Path.Combine(folder, when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");

    /// <summary>Never throws: saving a chat must not be able to break the thing being said.</summary>
    public bool Add(string who, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        DateTimeOffset when = now();
        string line = $"**{when.ToString("HH:mm", CultureInfo.InvariantCulture)} {who}:** {Redactor.Redact(text.Trim()).ReplaceLineEndings(" ")}\n\n";
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(FileFor(when), line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>The newest saved days first, so "what did we talk about yesterday" has somewhere to look.</summary>
    public IReadOnlyList<string> RecentFiles(int count)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.md").OrderDescending(StringComparer.Ordinal).Take(Math.Max(0, count)).ToArray()
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
