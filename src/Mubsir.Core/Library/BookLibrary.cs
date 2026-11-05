using System.Text.Json;
using Mubsir.Core.Reading;
using Mubsir.Core.Text;

namespace Mubsir.Core.Library;

/// <summary>What Mubsir knows about one book on this PC, read locally from its first pages.</summary>
public sealed record LibraryCard(string Path, string? Print, string Title, string? Author, int Pages, string Opening, long Length, DateTime Written, bool Locked = false);

public static class BookTitle
{
    private static readonly string[] Useless = ["untitled", "microsoft word", "document", "scan", "adobe", "print", "powerpoint"];

    /// <summary>The file's own title, then the first real line of the book, then the file name.</summary>
    public static string Guess(string fileName, string? metadataTitle, string? opening)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrWhiteSpace(metadataTitle) && metadataTitle.Trim().Length >= 3
            && !Useless.Any(u => metadataTitle.Contains(u, StringComparison.OrdinalIgnoreCase))
            && metadataTitle.Any(char.IsLetter))
        {
            return metadataTitle.Trim();
        }

        bool poorName = name.Count(char.IsLetter) < 4 || Useless.Any(u => name.Contains(u, StringComparison.OrdinalIgnoreCase));
        if (poorName && opening is not null)
        {
            foreach (string line in opening.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.Length is >= 4 and <= 120 && line.Count(char.IsLetter) >= 4)
                {
                    return line;
                }
            }
        }

        return name;
    }
}

/// <summary>Every book found on this PC. Stays on this PC. Searching it needs no AI and no internet.</summary>
public sealed class BookLibrary(string file)
{
    public const int OpeningChars = 1500;
    private const int MaxCards = 5000;

    private readonly object _gate = new();
    private Dictionary<string, LibraryCard>? _cards;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return Cards().Count;
            }
        }
    }

    public bool IsCurrent(string path, long length, DateTime written)
    {
        lock (_gate)
        {
            return Cards().TryGetValue(Key(path), out LibraryCard? card) && card.Length == length && card.Written == written;
        }
    }

    public void Put(LibraryCard card)
    {
        lock (_gate)
        {
            Dictionary<string, LibraryCard> cards = Cards();
            if (cards.Count < MaxCards || cards.ContainsKey(Key(card.Path)))
            {
                cards[Key(card.Path)] = card with { Opening = card.Opening.Length <= OpeningChars ? card.Opening : card.Opening[..OpeningChars] };
            }
        }
    }

    public void Forget(Func<string, bool> stillThere)
    {
        lock (_gate)
        {
            Dictionary<string, LibraryCard> cards = Cards();
            foreach (string key in cards.Where(kv => !stillThere(kv.Value.Path)).Select(kv => kv.Key).ToArray())
            {
                cards.Remove(key);
            }
        }
    }

    public LibraryCard? ByPrint(string print)
    {
        lock (_gate)
        {
            return Cards().Values.FirstOrDefault(c => c.Print == print);
        }
    }

    public LibraryCard? ByPath(string path)
    {
        lock (_gate)
        {
            return Cards().GetValueOrDefault(Key(path));
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))!);
            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Cards().Values.ToList(), ReadingJson.Default.ListLibraryCard));
            File.Move(temp, file, overwrite: true);
        }
    }

    /// <summary>Best matches first. The title counts most, then the file name, then the opening pages.</summary>
    public IReadOnlyList<LibraryCard> Search(string query, int count = 5)
    {
        string[] terms = ArabicFolding.Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        lock (_gate)
        {
            var scored = new List<(LibraryCard Card, double Score)>();
            foreach (LibraryCard card in Cards().Values)
            {
                string title = ArabicFolding.Fold(card.Title);
                string name = ArabicFolding.Fold(System.IO.Path.GetFileNameWithoutExtension(card.Path));
                string opening = ArabicFolding.Fold(card.Opening);
                string author = ArabicFolding.Fold(card.Author ?? string.Empty);
                double score = 0;
                int hit = 0;
                foreach (string term in terms)
                {
                    double best = title.Contains(term, StringComparison.Ordinal) ? 5
                        : name.Contains(term, StringComparison.Ordinal) ? 3
                        : author.Contains(term, StringComparison.Ordinal) ? 3
                        : opening.Contains(term, StringComparison.Ordinal) ? 1 : 0;
                    score += best;
                    hit += best > 0 ? 1 : 0;
                }

                if (hit * 2 >= terms.Length && hit > 0)
                {
                    scored.Add((card, score));
                }
            }

            return scored.OrderByDescending(s => s.Score).ThenByDescending(s => s.Card.Written).Take(count).Select(s => s.Card).ToArray();
        }
    }

    private static string Key(string path) => ReadingPositions.Key(path);

    private Dictionary<string, LibraryCard> Cards()
    {
        if (_cards is not null)
        {
            return _cards;
        }

        _cards = new Dictionary<string, LibraryCard>(StringComparer.Ordinal);
        try
        {
            if (File.Exists(file) && JsonSerializer.Deserialize(File.ReadAllText(file), ReadingJson.Default.ListLibraryCard) is { } saved)
            {
                foreach (LibraryCard card in saved)
                {
                    _cards[Key(card.Path)] = card;
                }
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
        }

        return _cards;
    }
}

/// <summary>A numbered list that works by ear. Same-named books say where each one is.</summary>
public static class BookListText
{
    public static string List(IReadOnlyList<LibraryCard> cards, Common.Lang lang)
    {
        bool ar = lang == Common.Lang.Arabic;
        var lines = new List<string> { ar ? $"لقيت {cards.Count}." : $"I found {cards.Count}." };
        for (int i = 0; i < cards.Count; i++)
        {
            LibraryCard c = cards[i];
            string pages = c.Pages > 0 ? (ar ? $"، {c.Pages} صفحة" : $", {c.Pages} pages") : string.Empty;
            bool twin = cards.Count(o => string.Equals(o.Title, c.Title, StringComparison.OrdinalIgnoreCase)) > 1;
            string where = twin ? (ar ? "، في " : ", in ") + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(c.Path)) : string.Empty;
            lines.Add($"{i + 1}: {c.Title}{pages}{where}.");
        }

        lines.Add(ar ? "قول الرقم." : "Say the number.");
        return string.Join(' ', lines);
    }
}
