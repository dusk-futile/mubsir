using System.Text.Json;
using System.Text.Json.Serialization;
using Mubsir.Core.Library;

namespace Mubsir.Core.Reading;

/// <summary>A place the reader marked, with or without something they said about it.</summary>
public sealed record BookNote(int Page, int Offset, string? Text, DateTimeOffset At);

/// <summary>Voice notes and bookmarks for each book. Kept on this PC, and they follow the file by its contents.</summary>
public sealed class BookNotes(string file)
{
    private const int MaxPerBook = 500;
    private readonly object _gate = new();
    private Dictionary<string, List<BookNote>>? _items;

    public event Action? SaveFailed;

    public bool Add(string path, int page, int offset, string? text, DateTimeOffset at)
    {
        lock (_gate)
        {
            var items = Items();
            string key = FileFingerprint.Of(path) ?? ReadingPositions.Key(path);
            if (!items.TryGetValue(key, out List<BookNote>? notes))
            {
                items[key] = notes = [];
            }

            notes.Add(new BookNote(page, offset, string.IsNullOrWhiteSpace(text) ? null : text.Trim(), at));
            if (notes.Count > MaxPerBook)
            {
                notes.RemoveAt(0);
            }

            try
            {
                string temp = file + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
                File.WriteAllText(temp, JsonSerializer.Serialize(items, ReadingJson.Default.DictionaryStringListBookNote));
                File.Move(temp, file, overwrite: true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                SaveFailed?.Invoke();
                return false;
            }
        }
    }

    public IReadOnlyList<BookNote> For(string path)
    {
        lock (_gate)
        {
            string key = FileFingerprint.Of(path) ?? ReadingPositions.Key(path);
            return Items().TryGetValue(key, out List<BookNote>? notes)
                ? notes.OrderBy(n => n.Page).ThenBy(n => n.Offset).ToArray()
                : [];
        }
    }

    private Dictionary<string, List<BookNote>> Items()
    {
        if (_items is not null)
        {
            return _items;
        }

        try
        {
            _items = File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), ReadingJson.Default.DictionaryStringListBookNote) : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _items = null;
        }

        return _items ??= new Dictionary<string, List<BookNote>>(StringComparer.Ordinal);
    }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, List<BookNote>>))]
[JsonSerializable(typeof(List<LibraryCard>))]
[JsonSerializable(typeof(PageImageFile))]
internal sealed partial class ReadingJson : JsonSerializerContext;
