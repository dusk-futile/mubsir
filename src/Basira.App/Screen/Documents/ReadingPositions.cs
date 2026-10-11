using System.Text.Json;
using System.Text.Json.Serialization;
using Basira.App.Abstractions;

namespace Basira.App.Screen.Documents;

/// <summary>Where the user stopped in a document.</summary>
public sealed record ReadingPosition(int Page, int Offset, DateTimeOffset UpdatedAt);

/// <summary>
/// Last reading position per document ("continue the book"), kept in <see cref="AppPaths.PositionsFile"/>.
/// File paths are stored on this PC only and never logged or sent anywhere. Thread-safe; I/O errors are swallowed.
/// </summary>
internal sealed class ReadingPositions
{
    private const int MaxEntries = 200;

    private readonly object _gate = new();
    private readonly string _file;
    private Dictionary<string, ReadingPosition>? _items;

    public ReadingPositions(string? file = null)
    {
        _file = file ?? AppPaths.PositionsFile;
    }

    public ReadingPosition? Get(string path)
    {
        lock (_gate)
        {
            return Items().TryGetValue(Key(path), out var p) ? p : null;
        }
    }

    public void Set(string path, int page, int offset)
    {
        lock (_gate)
        {
            var items = Items();
            items[Key(path)] = new ReadingPosition(Math.Max(1, page), Math.Max(0, offset), DateTimeOffset.UtcNow);
            if (items.Count > MaxEntries)
            {
                foreach (var old in items.OrderBy(kv => kv.Value.UpdatedAt).Take(items.Count - MaxEntries).Select(kv => kv.Key).ToList())
                {
                    items.Remove(old);
                }
            }

            Save(items);
        }
    }

    internal static string Key(string path)
    {
        try
        {
            return Path.GetFullPath(path).ToUpperInvariant();
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim().ToUpperInvariant();
        }
    }

    private Dictionary<string, ReadingPosition> Items()
    {
        if (_items is not null)
        {
            return _items;
        }

        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var loaded = JsonSerializer.Deserialize(json, PositionsJson.Default.PositionsFileModel);
                _items = loaded?.Items is { } d ? new Dictionary<string, ReadingPosition>(d, StringComparer.Ordinal) : null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _items = null; // a damaged file starts over rather than breaking reading
        }

        return _items ??= new Dictionary<string, ReadingPosition>(StringComparer.Ordinal);
    }

    private void Save(Dictionary<string, ReadingPosition> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new PositionsFileModel { Items = items }, PositionsJson.Default.PositionsFileModel));
            File.Move(tmp, _file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a bookmark is better than failing the read.
        }
    }
}

internal sealed class PositionsFileModel
{
    public int Version { get; set; } = 1;

    public Dictionary<string, ReadingPosition>? Items { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PositionsFileModel))]
internal sealed partial class PositionsJson : JsonSerializerContext;
