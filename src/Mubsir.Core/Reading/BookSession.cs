using Mubsir.Core.Common;

namespace Mubsir.Core.Reading;

/// <summary>
/// The book the reader is on, and everything they can say about it: pages, place, notes and the sleep timer.
/// All of it runs on this PC with no AI. It remembers the book after reading stops, so "continue" works.
/// </summary>
public sealed class BookSession(IBookVoice voice, Action stopVoice, ReadingPositions positions, BookNotes notes, Action<bool> setReading)
{
    private readonly object _gate = new();
    private BookReading? _reading;
    private IBookPages? _pages;
    private string? _path;
    private string? _title;
    private Lang _lang = Lang.English;
    private int _page = 1;
    private int _offset;
    private Timer? _timer;

    public bool HasBook => Volatile.Read(ref _pages) is not null;

    public bool IsActive => Volatile.Read(ref _reading)?.IsActive == true;

    public DateTimeOffset LastHeardAt { get; private set; }

    public DateTimeOffset StoppedAt { get; private set; }

    public async Task<bool> StartAsync(IBookPages pages, string? path, string? title, int page, int offset, Lang lang)
    {
        if (IsActive)
        {
            stopVoice();
        }

        var reading = new BookReading(voice, pages, (p, o) => OnPlace(p, o), () => setReading(false), lang);
        lock (_gate)
        {
            _reading?.Stop();
            _reading = reading;
            _pages = pages;
            _path = path;
            _title = title;
            _lang = lang;
            _page = Math.Clamp(page, 1, Math.Max(1, pages.Count));
            _offset = offset;
            LastHeardAt = DateTimeOffset.UtcNow;
        }

        bool started = await reading.StartAsync(_page, offset).ConfigureAwait(false);
        if (started && reading.IsActive)
        {
            setReading(true);
        }

        return started;
    }

    /// <summary>A sentence just started playing.</summary>
    public void OnSpoken(int index) => Volatile.Read(ref _reading)?.OnSpoken(index);

    /// <summary>Speech was stopped. The book and the place stay, ready for "continue".</summary>
    public void OnStopped()
    {
        BookReading? reading = Volatile.Read(ref _reading);
        if (reading?.IsActive == true)
        {
            StoppedAt = DateTimeOffset.UtcNow;
        }

        reading?.Stop();
    }

    /// <summary>True while a book is the thing in hand: reading now, or stopped within the last half hour.</summary>
    public bool Engaged => IsActive || (HasBook && DateTimeOffset.UtcNow - LastHeardAt < TimeSpan.FromMinutes(30));

    /// <summary>True if talking to Mubsir just interrupted the book, so a short reply can go back to it.</summary>
    public bool JustInterrupted => HasBook && !IsActive && DateTimeOffset.UtcNow - StoppedAt < TimeSpan.FromSeconds(90);

    /// <summary>One line for the AI, so "what does that mean?" is about the book. Only the title and the page.</summary>
    public string? ContextHint()
    {
        lock (_gate)
        {
            return _pages is null || !Engaged
                ? null
                : $"The user is reading a book through this app: \"{_title}\", page {_page} of {_pages.Count}. For a question about it, call read_document with the question and a null path.";
        }
    }

    public Task<string?> ContinueAsync() => WithBook(p => Restart(p, _page, _offset, null));

    /// <summary>The place to speak from, for an answer such as a question about this page.</summary>
    public (string? Path, int Page) Current
    {
        get
        {
            lock (_gate)
            {
                return (_path, _page);
            }
        }
    }

    /// <summary>The page being read and how many there are, while a book is being read aloud; otherwise null.</summary>
    public (int Page, int Pages)? Reading
    {
        get
        {
            lock (_gate)
            {
                return IsActive && _pages is { } pages ? (_page, pages.Count) : null;
            }
        }
    }

    public Task<string?> NextPageAsync() => WithBook(p => _page >= p.Count
        ? Task.FromResult<string?>(_lang == Lang.Arabic ? "دي آخر صفحة." : "This is the last page.")
        : Restart(p, _page + 1, 0, null));

    public Task<string?> PreviousPageAsync() => WithBook(p => _page <= 1
        ? Task.FromResult<string?>(_lang == Lang.Arabic ? "دي أول صفحة." : "This is the first page.")
        : Restart(p, _page - 1, 0, null));

    public Task<string?> RepeatPageAsync() => WithBook(p => Restart(p, _page, 0, null));

    public Task<string?> GoToPageAsync(int page) => WithBook(p => page < 1 || page > p.Count
        ? Task.FromResult<string?>(_lang == Lang.Arabic ? $"الكتاب {p.Count} صفحة بس. أروح لأي صفحة؟" : $"There are only {p.Count} pages. Which one?")
        : Restart(p, page, 0, null));

    public string Where()
    {
        lock (_gate)
        {
            if (_pages is not { } pages)
            {
                return NoBook();
            }

            int percent = pages.Count == 0 ? 0 : (int)Math.Round(100.0 * _page / pages.Count);
            string title = string.IsNullOrWhiteSpace(_title) ? string.Empty : _title + ". ";
            int left = pages.Count - _page;
            return _lang == Lang.Arabic
                ? $"{title}صفحة {_page} من {pages.Count}، يعني {percent} في المية. فاضل {left} صفحة."
                : $"{title}Page {_page} of {pages.Count}, {percent} percent. {left} pages left.";
        }
    }

    /// <summary>Marks the sentence being heard, with an optional note in the reader's own words.</summary>
    public string Mark(string? text)
    {
        lock (_gate)
        {
            if (_pages is null)
            {
                return NoBook();
            }

            if (_path is null)
            {
                return _lang == Lang.Arabic ? "مقدرش أحفظ علامة لنص من الشاشة، لازم يكون ملف." : "I can only keep notes for a file, not for text on the screen.";
            }

            bool saved = notes.Add(_path, _page, _offset, text, DateTimeOffset.UtcNow);
            if (!saved)
            {
                return (_lang == Lang.Arabic, text is null) switch
                {
                    (true, true) => "لم أستطع حفظ العلامة.",
                    (true, false) => "لم أستطع حفظ الملاحظة.",
                    (false, true) => "I couldn't save the bookmark.",
                    _ => "I couldn't save the note.",
                };
            }

            return (_lang == Lang.Arabic, text is null) switch
            {
                (true, true) => $"علمت صفحة {_page}.",
                (true, false) => $"سجلت الملاحظة على صفحة {_page}.",
                (false, true) => $"Marked page {_page}.",
                _ => $"Noted on page {_page}.",
            };
        }
    }

    public string ReadNotes()
    {
        lock (_gate)
        {
            if (_pages is null)
            {
                return NoBook();
            }

            IReadOnlyList<BookNote> all = _path is null ? [] : notes.For(_path);
            if (all.Count == 0)
            {
                return _lang == Lang.Arabic ? "مفيش ملاحظات على الكتاب ده لسه." : "There are no notes on this book yet.";
            }

            bool ar = _lang == Lang.Arabic;
            IEnumerable<string> lines = all.Select(n => (ar ? $"صفحة {n.Page}" : $"Page {n.Page}") + (n.Text is null ? (ar ? ": علامة." : ": a bookmark.") : ": " + n.Text + "."));
            return string.Join(' ', lines);
        }
    }

    /// <summary>Stops reading after a while, keeping the place.</summary>
    public string SetTimer(int minutes)
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = new Timer(_ => OnTimer(), null, TimeSpan.FromMinutes(minutes), Timeout.InfiniteTimeSpan);
            return _lang == Lang.Arabic ? $"تمام، هقف بعد {minutes} دقيقة." : $"All right, I'll stop in {minutes} minutes.";
        }
    }

    public string CancelTimer()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            return _lang == Lang.Arabic ? "لغيت المؤقت." : "Timer cancelled.";
        }
    }

    /// <summary>The sleep timer ran out. Says nothing if reading has already stopped.</summary>
    public void OnTimer()
    {
        if (!IsActive)
        {
            return;
        }

        stopVoice();
        voice.Say(_lang == Lang.Arabic ? $"خلص الوقت. وقفنا عند صفحة {_page}." : $"Time's up. We stopped on page {_page}.", _lang);
    }

    private Task<string?> WithBook(Func<IBookPages, Task<string?>> run)
    {
        IBookPages? pages = Volatile.Read(ref _pages);
        return pages is null ? Task.FromResult<string?>(NoBook()) : run(pages);
    }

    private async Task<string?> Restart(IBookPages pages, int page, int offset, string? message)
    {
        string? path;
        string? title;
        Lang lang;
        lock (_gate)
        {
            path = _path;
            title = _title;
            lang = _lang;
        }

        bool ok = await StartAsync(pages, path, title, page, offset, lang).ConfigureAwait(false);
        return ok ? message : (lang == Lang.Arabic ? "مقدرتش أقرأ من هنا." : "I couldn't read from there.");
    }

    private void OnPlace(int page, int offset)
    {
        string? path;
        int total;
        lock (_gate)
        {
            _page = page;
            _offset = offset;
            LastHeardAt = DateTimeOffset.UtcNow;
            path = _path;
            total = _pages?.Count ?? 0;
        }

        if (path is not null)
        {
            positions.Set(path, page, offset, total);
        }
    }

    private string NoBook() => _lang == Lang.Arabic ? "مفيش كتاب مفتوح دلوقتي." : "No book is open right now.";
}
