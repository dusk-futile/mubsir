using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Common;

namespace Mubsir.Core.Reading;

public sealed record ReadingPosition(int Page, int Offset, DateTimeOffset UpdatedAt, string? Path = null, int Pages = 0, string? Print = null);

/// <summary>
/// Where the user stopped in each book. Found by the file's contents, so renaming or moving a book keeps the
/// place. If the contents changed, the old path still finds it, and so do bookmarks from older versions.
/// </summary>
public sealed class ReadingPositions(string file)
{
    private const int MaxEntries = 200;

    public event Action? SaveFailed;

    private readonly object _gate = new();
    private readonly Dictionary<string, (long Length, DateTime Written, string? Print)> _prints = new(StringComparer.Ordinal);
    private Dictionary<string, ReadingPosition>? _items;

    public ReadingPosition? Get(string path)
    {
        lock (_gate)
        {
            var items = Items();
            string pathKey = Key(path);
            if (Print(path, pathKey) is { } print && items.TryGetValue(print, out var byContents))
            {
                return byContents;
            }

            return items.TryGetValue(pathKey, out var legacy)
                ? legacy
                : items.Values.Where(p => p.Path == pathKey).MaxBy(p => p.UpdatedAt);
        }
    }

    public bool Set(string path, int page, int offset, int pages = 0)
    {
        lock (_gate)
        {
            var items = Items();
            string pathKey = Key(path);
            string key = Print(path, pathKey) ?? pathKey;
            items.Remove(pathKey);
            foreach (var stale in items.Where(kv => kv.Key != key && kv.Value.Path == pathKey).Select(kv => kv.Key).ToList())
            {
                items.Remove(stale);
            }

            items[key] = new ReadingPosition(Math.Max(1, page), Math.Max(0, offset), DateTimeOffset.UtcNow, pathKey, Math.Max(0, pages), key == pathKey ? null : key);
            if (items.Count > MaxEntries)
            {
                foreach (var old in items.OrderBy(kv => kv.Value.UpdatedAt).Take(items.Count - MaxEntries).Select(kv => kv.Key).ToList())
                {
                    items.Remove(old);
                }
            }

            return Save(items);
        }
    }

    /// <summary>The books read most recently, newest first.</summary>
    public IReadOnlyList<ReadingPosition> Recent(int count)
    {
        lock (_gate)
        {
            return Items().Values.OrderByDescending(p => p.UpdatedAt).Take(count).ToArray();
        }
    }

    internal static string Key(string path)
    {
        try
        {
            return System.IO.Path.GetFullPath(path).ToUpperInvariant();
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim().ToUpperInvariant();
        }
    }

    /// <summary>Hashes a file once per version of it, because the place is saved at every sentence.</summary>
    private string? Print(string path, string pathKey)
    {
        _prints.TryGetValue(pathKey, out var known);
        FileInfo info;
        try
        {
            info = new FileInfo(path);
            if (!info.Exists)
            {
                return known.Print;
            }

            if (known.Print is not null && known.Length == info.Length && known.Written == info.LastWriteTimeUtc)
            {
                return known.Print;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return known.Print;
        }

        string? print = FileFingerprint.Of(path);
        _prints[pathKey] = (info.Length, info.LastWriteTimeUtc, print);
        return print;
    }

    private Dictionary<string, ReadingPosition> Items()
    {
        if (_items is not null)
        {
            return _items;
        }

        try
        {
            if (File.Exists(file))
            {
                var json = File.ReadAllText(file);
                var loaded = JsonSerializer.Deserialize(json, PositionsJson.Default.PositionsFileModel);
                _items = loaded?.Items is { } d ? new Dictionary<string, ReadingPosition>(d, StringComparer.Ordinal) : null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _items = null;
        }

        return _items ??= new Dictionary<string, ReadingPosition>(StringComparer.Ordinal);
    }

    private bool Save(Dictionary<string, ReadingPosition> items)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new PositionsFileModel { Items = items }, PositionsJson.Default.PositionsFileModel));
            File.Move(tmp, file, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke();
            return false;
        }
    }
}

internal sealed class PositionsFileModel
{
    public int Version { get; set; } = 2;

    public Dictionary<string, ReadingPosition>? Items { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PositionsFileModel))]
internal sealed partial class PositionsJson : JsonSerializerContext;

/// <summary>What the reader was reading, in words, for the start-up greeting and "what was I reading?".</summary>
public static class ReadingJournal
{
    public static string Line(ReadingPosition place, string title, DateTimeOffset now, Lang lang)
    {
        string when = When(place.UpdatedAt, now, lang);
        return lang == Lang.Arabic
            ? $"{when} وقفت عند صفحة {place.Page}{(place.Pages > 0 ? $" من {place.Pages}" : string.Empty)} في {title}."
            : $"{when} you stopped on page {place.Page}{(place.Pages > 0 ? $" of {place.Pages}" : string.Empty)} of {title}.";
    }

    public static string Greeting(ReadingPosition place, string title, DateTimeOffset now, Lang lang) =>
        Line(place, title, now, lang) + (lang == Lang.Arabic ? " قول كمل عشان نكمل." : " Say continue to carry on.");

    public static string Summary(IReadOnlyList<(ReadingPosition Place, string Title)> books, DateTimeOffset now, Lang lang)
    {
        if (books.Count == 0)
        {
            return lang == Lang.Arabic ? "لسه ماقريناش كتب مع بعض." : "We haven't read any books together yet.";
        }

        return string.Join(' ', books.Select(b => Line(b.Place, b.Title, now, lang)));
    }

    private static string When(DateTimeOffset then, DateTimeOffset now, Lang lang)
    {
        int days = (now.ToLocalTime().Date - then.ToLocalTime().Date).Days;
        return (days, lang == Lang.Arabic) switch
        {
            (<= 0, true) => "النهاردة",
            (<= 0, false) => "Today",
            (1, true) => "إمبارح",
            (1, false) => "Yesterday",
            (_, true) => $"من {days} يوم",
            (_, false) => $"{days} days ago",
        };
    }
}
