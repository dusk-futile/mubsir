using System.Globalization;

namespace Mubsir.Core.Reading;

public enum PageKind
{
    Clean,
    Symbols,
    Broken,
}

/// <summary>Tells apart a page that can go straight to the voice from one whose text came out wrong.</summary>
public static class PageQuality
{
    private const int MinLetters = 20;

    public static PageKind Check(string text)
    {
        int letters = 0;
        int bad = 0;
        int garbled = 0;
        int symbols = 0;
        foreach (char c in text)
        {
            if (c == '\uFFFD' || c is >= '\uE000' and <= '\uF8FF' || char.IsSurrogate(c) && char.GetUnicodeCategory(c) == UnicodeCategory.PrivateUse)
            {
                bad++;
            }
            else if (c is >= '\u00C0' and <= '\u00FF' && char.IsLetter(c))
            {
                // Arabic in the wrong encoding comes out as accented Latin, such as "ÇáßÊÇÈ".
                garbled++;
                letters++;
            }
            else if (c is >= '\u0250' and <= '\u02AF' or >= '\u2200' and <= '\u22FF' or >= '\u0370' and <= '\u03FF')
            {
                symbols++;
            }
            else if (char.IsLetter(c))
            {
                letters++;
            }
        }

        int total = letters + bad;
        if (total < MinLetters)
        {
            return bad >= 3 ? PageKind.Broken : PageKind.Clean;
        }

        if (bad * 50 >= total || garbled * 3 >= total || HasScatteredLetters(text))
        {
            return PageKind.Broken;
        }

        return symbols * 50 >= total ? PageKind.Symbols : PageKind.Clean;
    }

    /// <summary>"ا ل ك ت ا ب": a text layer that lost its words, one letter at a time.</summary>
    private static bool HasScatteredLetters(string text)
    {
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 20)
        {
            return false;
        }

        int single = words.Count(w => w.Length == 1 && char.IsLetter(w[0]));
        return single * 2 >= words.Length;
    }
}

/// <summary>The only two ways to repair a page. Local OCR is free. <see cref="ImageAsync"/> is the one function
/// that sends a page image to the AI.</summary>
public interface IPageRepair
{
    Task<string?> OcrAsync(int page, CancellationToken ct);

    Task<string?> ImageAsync(int page, string reason, CancellationToken ct);
}

/// <summary>
/// The locks on page images, each independent of the others. A repaired page is never paid for twice, more than
/// three images in a row or ten for one book in a day trips the breaker, and the count survives a restart.
/// </summary>
public sealed class PageImageBudget(string? file = null)
{
    public const int MaxInARow = 3;
    public const int MaxPerBookPerDay = 10;

    private readonly object _gate = new();
    private readonly Dictionary<string, string> _repaired = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _spent = new(StringComparer.Ordinal);
    private int _inARow;
    private bool _loaded;

    public string? Repaired(string book, int page)
    {
        lock (_gate)
        {
            Load();
            return _repaired.GetValueOrDefault(book + "|" + page);
        }
    }

    public void NoteGoodPage()
    {
        lock (_gate)
        {
            _inARow = 0;
        }
    }

    /// <summary>Takes one image from the budget. False means the breaker tripped, so no image may be sent.</summary>
    public bool TryTake(string book, DateTimeOffset now)
    {
        lock (_gate)
        {
            Load();
            string day = book + "|" + now.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int today = _spent.GetValueOrDefault(day);
            if (_inARow >= MaxInARow || today >= MaxPerBookPerDay)
            {
                return false;
            }

            _inARow++;
            _spent[day] = today + 1;
            Save();
            return true;
        }
    }

    public void Keep(string book, int page, string text)
    {
        lock (_gate)
        {
            Load();
            _repaired[book + "|" + page] = text;
            Save();
        }
    }

    private void Load()
    {
        if (_loaded || file is null)
        {
            _loaded = true;
            return;
        }

        _loaded = true;
        try
        {
            if (File.Exists(file) && System.Text.Json.JsonSerializer.Deserialize(File.ReadAllText(file), ReadingJson.Default.PageImageFile) is { } saved)
            {
                foreach (var kv in saved.Repaired ?? [])
                {
                    _repaired[kv.Key] = kv.Value;
                }

                foreach (var kv in saved.Spent ?? [])
                {
                    _spent[kv.Key] = kv.Value;
                }
            }
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
        }
    }

    private void Save()
    {
        if (file is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            string temp = file + ".tmp";
            File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(new PageImageFile { Repaired = _repaired, Spent = _spent }, ReadingJson.Default.PageImageFile));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

}

internal sealed class PageImageFile
{
    public Dictionary<string, string>? Repaired { get; set; }

    public Dictionary<string, int>? Spent { get; set; }
}

/// <summary>Pages that are checked before they reach the voice. A broken page tries local OCR first, then one
/// image if the budget allows, and otherwise reads the best text it has, with a warning.</summary>
public sealed class CheckedPages(IBookPages inner, string book, IPageRepair repair, PageImageBudget budget, Func<bool> cloudAllowed, Func<DateTimeOffset> now, Common.Lang lang) : IBookPages
{
    public int Count => inner.Count;

    public async Task<string?> GetAsync(int page, CancellationToken ct)
    {
        string? text = await inner.GetAsync(page, ct).ConfigureAwait(false);
        if (text is null || PageQuality.Check(text) != PageKind.Broken)
        {
            if (text is not null)
            {
                budget.NoteGoodPage();
            }

            return text;
        }

        if (budget.Repaired(book, page) is { } kept)
        {
            return kept;
        }

        string? ocr = await repair.OcrAsync(page, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(ocr) && PageQuality.Check(ocr) != PageKind.Broken)
        {
            budget.Keep(book, page, ocr);
            return ocr;
        }

        if (cloudAllowed() && budget.TryTake(book, now()))
        {
            string? fixedText = await repair.ImageAsync(page, "page_text_broken", ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fixedText))
            {
                budget.Keep(book, page, fixedText);
                return fixedText;
            }
        }

        string warning = lang == Common.Lang.Arabic ? "الصفحة دي صعبة في القراءة، ممكن كلمات تطلع غلط. " : "This page is hard to read; some words may be wrong. ";
        return warning + (string.IsNullOrWhiteSpace(ocr) ? text : ocr);
    }
}
