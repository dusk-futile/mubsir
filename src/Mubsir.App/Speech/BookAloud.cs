using System.IO.Compression;
using Mubsir.App.Screen.Documents;
using Mubsir.Core.Common;
using Mubsir.Core.Library;
using Mubsir.Core.Logging;
using Mubsir.Core.Reading;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Speech;

/// <summary>Connects books to the real voice, the saved places, the library and page repair.</summary>
internal sealed class BookAloud : IBookVoice
{
    private const int OcrPagesAtATime = 3;
    private const int MaxPhraseHits = 5;

    private readonly SpeechService _speech;
    private readonly ReadingPositions _positions;
    private readonly DocumentReader _documents;
    private readonly PageImageBudget _budget;
    private readonly Func<bool> _cloudAllowed;
    private readonly IEventLog _log;

    public BookAloud(
        SpeechService speech,
        ReadingPositions positions,
        BookNotes notes,
        BookLibrary library,
        DocumentReader documents,
        PageImageBudget budget,
        Func<bool> cloudAllowed,
        Action<bool> setReading,
        IEventLog log)
    {
        _speech = speech;
        _positions = positions;
        _documents = documents;
        _budget = budget;
        _cloudAllowed = cloudAllowed;
        _log = log;
        Library = library;
        Session = new BookSession(this, speech.Stop, positions, notes, setReading);
        speech.Stopped += Session.OnStopped;

        // This can arrive on NVDA's callback, so the file write and the next page happen elsewhere.
        speech.History.Spoken += index => ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Session.OnSpoken(index);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("book_spoken", e);
            }
        });
    }

    public BookSession Session { get; }

    public BookLibrary Library { get; }

    /// <summary>Set by the executor. The one function that sends a page image to the AI.</summary>
    public Func<byte[], Lang, CancellationToken, Task<string?>>? ImageReader { get; set; }

    /// <summary>Set by the executor. Asks the AI for search keywords, or null if it can't be asked.</summary>
    public Func<string, Lang, CancellationToken, Task<string?>>? KeywordReader { get; set; }

    public long Generation => _speech.Generation;

    /// <summary>Finds a book, with the AI's keywords when the plain search isn't clear. Works without the AI too.</summary>
    public Task<IReadOnlyList<LibraryCard>> FindBooksAsync(string query, Lang lang, CancellationToken ct)
    {
        Func<string, Task<string?>>? ask = KeywordReader is { } read && _cloudAllowed() ? prompt => read(prompt, lang, ct) : null;
        return BookSearch.FindAsync(Library, query, ask);
    }

    /// <summary>Looks for exact words inside the text of every book, on this PC. Pages that are only pictures are
    /// skipped: reading them all would take hours.</summary>
    public async Task<(IReadOnlyList<PhraseHit> Hits, int Searched)> FindPhraseAsync(string phrase, CancellationToken ct)
    {
        var hits = new List<PhraseHit>();
        int searched = 0;
        foreach (LibraryCard card in Library.All())
        {
            ct.ThrowIfCancellationRequested();
            if (card.Locked || await TextOfAsync(card, ct).ConfigureAwait(false) is not { } pages)
            {
                continue;
            }

            searched++;
            int page = PhraseSearch.FirstPage(pages, phrase);
            if (page > 0)
            {
                hits.Add(new PhraseHit(card, page));
                if (hits.Count == MaxPhraseHits)
                {
                    break;
                }
            }
        }

        return (hits, searched);
    }

    /// <summary>A book's text, page by page. Kept compressed on this PC after the first time, so the next search is quick.</summary>
    private async Task<IReadOnlyList<string>?> TextOfAsync(LibraryCard card, CancellationToken ct)
    {
        string? kept = card.Print is { } print ? Path.Combine(AppPaths.BookTextDir, print.Replace(':', '-') + ".gz") : null;
        try
        {
            if (kept is not null && File.Exists(kept))
            {
                using var reader = new StreamReader(new GZipStream(File.OpenRead(kept), CompressionMode.Decompress));
                return (await reader.ReadToEndAsync(ct).ConfigureAwait(false)).Split('\f');
            }

            DocumentText doc = await _documents.ReadAsync(card.Path, 1, 0, null, ct).ConfigureAwait(false);
            if (kept is not null)
            {
                Directory.CreateDirectory(AppPaths.BookTextDir);
                using var writer = new StreamWriter(new GZipStream(File.Create(kept), CompressionLevel.Fastest));
                await writer.WriteAsync(string.Join('\f', doc.Pages).AsMemory(), ct).ConfigureAwait(false);
            }

            return doc.Pages;
        }
        catch (Exception e) when (e is DocumentReadException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    public IReadOnlyList<SpokenSentence> Say(string text, Lang lang) => Say(text, lang, null);

    public IReadOnlyList<SpokenSentence> Say(string text, Lang lang, long generation) => Say(text, lang, (long?)generation);

    private IReadOnlyList<SpokenSentence> Say(string text, Lang lang, long? generation)
    {
        IReadOnlyList<int> indices = _speech.Enqueue(
            text, UtteranceKind.Reading, ScriptSegmenter.DominantLanguage(text, lang), interrupt: false, VerbalizeMode.Normal, done: null, generation);
        if (indices.Count == 0)
        {
            return [];
        }

        var wanted = indices.ToHashSet();
        return _speech.History.From(indices[0])
            .Where(item => wanted.Contains(item.Index))
            .Select(item => new SpokenSentence(item.Index, item.Text))
            .ToArray();
    }

    /// <summary>Starts a book whose pages are already loaded.</summary>
    public async Task<bool> StartAsync(IReadOnlyList<string> pages, IReadOnlyCollection<int> unread, string? path, int page, int offset, Lang lang)
    {
        IBookPages source = new DocumentPages(pages, unread, path, _documents, OcrPagesAtATime);
        if (path is not null)
        {
            source = new CheckedPages(source, FileFingerprint.Of(path) ?? path, new Repair(this, path, lang), _budget, _cloudAllowed, () => DateTimeOffset.UtcNow, lang);
        }

        bool started = await Session.StartAsync(source, path, path is null ? null : TitleOf(path), page, offset, lang).ConfigureAwait(false);
        if (started)
        {
            _log.Event(LogKind.Speech, tool: "read_book");
        }

        return started;
    }

    /// <summary>Opens a book from its file. Returns what to tell the reader if it can't be read, or null if it started.</summary>
    public async Task<string?> OpenAsync(string path, int? page, Lang lang, CancellationToken ct)
    {
        bool ar = lang == Lang.Arabic;
        if (!File.Exists(path))
        {
            return ar ? "الكتاب ده مبقاش على الجهاز." : "That book is no longer on this PC.";
        }

        ReadingPosition? saved = _positions.Get(path);
        int start = page ?? saved?.Page ?? 1;
        int offset = page is null ? saved?.Offset ?? 0 : 0;
        DocumentText doc;
        try
        {
            doc = await _documents.ReadAsync(path, start, OcrPagesAtATime, null, ct).ConfigureAwait(false);
        }
        catch (DocumentReadException e)
        {
            return e.Code switch
            {
                "pdf_encrypted" => ar ? "الكتاب ده مقفول بكلمة سر، فمقدرش أقراه." : "This book is locked with a password, so I can't read it.",
                "not_found" => ar ? "الكتاب ده مبقاش على الجهاز." : "That book is no longer on this PC.",
                "too_large" => ar ? "الملف ده كبير أوي عليا." : "That file is too large for me.",
                _ => ar ? "مقدرتش أفتح الكتاب ده." : "I couldn't open that book.",
            };
        }

        if (doc.Pages.Count == 0 || (doc.Pages.All(string.IsNullOrWhiteSpace) && doc.PendingOcrPages.Count == 0))
        {
            return ar ? "مش لاقي نص أقدر أقراه في الملف ده." : "I can't find any text to read in that file.";
        }

        return await StartAsync(doc.Pages, doc.PendingOcrPages.ToHashSet(), path, start, offset, lang).ConfigureAwait(false)
            ? null
            : ar ? "مقدرتش أقرأ الكتاب ده بصوت عالي." : "I couldn't read that book aloud.";
    }

    /// <summary>The most recent book that's still on this PC, looked for by its contents if it moved.</summary>
    public (ReadingPosition Place, string Title, string? Path)? LastPlace()
    {
        foreach (ReadingPosition place in _positions.Recent(5))
        {
            if (Locate(place) is { } path)
            {
                return (place, TitleOf(path), path);
            }
        }

        return null;
    }

    public IReadOnlyList<(ReadingPosition Place, string Title)> Journal(int count) =>
        _positions.Recent(count).Select(p => (p, Locate(p) is { } path ? TitleOf(path) : Path.GetFileNameWithoutExtension(p.Path ?? "?"))).ToArray();

    private string? Locate(ReadingPosition place)
    {
        if (place.Path is { } path && File.Exists(path))
        {
            return path;
        }

        return place.Print is { } print && Library.ByPrint(print) is { } card && File.Exists(card.Path) ? card.Path : null;
    }

    private string TitleOf(string path) => Library.ByPath(path)?.Title ?? Path.GetFileNameWithoutExtension(path);

    private sealed class Repair(BookAloud owner, string path, Lang lang) : IPageRepair
    {
        public Task<string?> OcrAsync(int page, CancellationToken ct) => owner._documents.OcrPageAsync(path, page, ct);

        public async Task<string?> ImageAsync(int page, string reason, CancellationToken ct)
        {
            if (owner.ImageReader is not { } read || await owner._documents.RenderPageJpegAsync(path, page, ct).ConfigureAwait(false) is not { } jpeg)
            {
                return null;
            }

            owner._log.Event(LogKind.ToolCall, tool: "page_image_" + reason);
            return await read(jpeg, lang, ct).ConfigureAwait(false);
        }
    }
}
