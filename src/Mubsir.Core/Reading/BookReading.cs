using Mubsir.Core.Common;

namespace Mubsir.Core.Reading;

public sealed record SpokenSentence(int Index, string Text);

/// <summary>The voice a book is read in. <see cref="Generation"/> changes whenever speech is stopped.</summary>
public interface IBookVoice
{
    long Generation { get; }

    /// <summary>Queues text after whatever is playing. Returns its sentences, or nothing if speech is off.</summary>
    IReadOnlyList<SpokenSentence> Say(string text, Lang lang);
}

public interface IBookPages
{
    int Count { get; }

    /// <summary>The text of a page, counting from 1. Null if it can't be read, such as a failed scan.</summary>
    Task<string?> GetAsync(int page, CancellationToken ct);
}

/// <summary>
/// One book being read aloud with no AI involved. One page is queued ahead of the page being heard, and the
/// place is saved only when a sentence is really reached, so it's never ahead of what the user heard.
/// </summary>
public sealed class BookReading(IBookVoice voice, IBookPages pages, Action<int, int>? savePlace, Action? ended, Lang lang)
{
    private const int MaxUnreadableInARow = 3;

    private readonly object _gate = new();
    private readonly Dictionary<int, (int Page, int Offset)> _places = [];
    private readonly CancellationTokenSource _cts = new();
    private long _generation;
    private int _nextPage;
    private int _lastFedPage;
    private int _endIndex = -1;
    private bool _active;
    private bool _feeding;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    /// <summary>Queues the first page. Returns false if nothing could be read aloud.</summary>
    public async Task<bool> StartAsync(int startPage, int startOffset)
    {
        lock (_gate)
        {
            _generation = voice.Generation;
            _active = true;
            _feeding = true;
            _nextPage = Math.Clamp(startPage, 1, Math.Max(1, pages.Count));
        }

        await FeedAsync(startOffset, firstHeader: true).ConfigureAwait(false);
        lock (_gate)
        {
            return _places.Count > 0 || _endIndex >= 0;
        }
    }

    /// <summary>A sentence has just started playing. Anything that isn't part of this book is ignored.</summary>
    public void OnSpoken(int index)
    {
        bool feed = false;
        (int Page, int Offset) place;
        lock (_gate)
        {
            if (index == _endIndex)
            {
                place = (1, 0);
            }
            else if (!_places.TryGetValue(index, out place))
            {
                return;
            }

            if (_active && voice.Generation == _generation && place.Page == _lastFedPage && !_feeding && index != _endIndex)
            {
                _feeding = true;
                feed = true;
            }
        }

        savePlace?.Invoke(place.Page, place.Offset);
        if (index == _endIndex)
        {
            Stop();
        }
        else if (feed)
        {
            _ = FeedAsync(0, firstHeader: false);
        }
    }

    /// <summary>Speech was stopped. The place stays where it was last heard.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!_active)
            {
                return;
            }

            _active = false;
        }

        _cts.Cancel();
        ended?.Invoke();
    }

    private async Task FeedAsync(int offset, bool firstHeader)
    {
        int unreadable = 0;
        int page;
        lock (_gate)
        {
            page = _nextPage;
        }

        try
        {
            while (true)
            {
                if (page > pages.Count)
                {
                    SayEnd();
                    return;
                }

                string? text = await pages.GetAsync(page, _cts.Token).ConfigureAwait(false);
                if (text is null)
                {
                    if (++unreadable >= MaxUnreadableInARow)
                    {
                        Say(BookText.Unreadable(page, lang));
                        Stop();
                        return;
                    }

                    if (!StillReading() || Say(BookText.PageUnreadable(page, lang)).Count == 0)
                    {
                        return;
                    }

                    page++;
                    offset = 0;
                    continue;
                }

                unreadable = 0;
                if (offset >= text.Length)
                {
                    offset = 0;
                }

                if (!string.IsNullOrWhiteSpace(text[offset..]))
                {
                    QueuePage(page, text, offset, firstHeader);
                    return;
                }

                page++;
                offset = 0;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Stop();
        }
        finally
        {
            lock (_gate)
            {
                _feeding = false;
            }
        }
    }

    /// <summary>The lock is held across queueing so a sentence can't start playing before its place is known.</summary>
    private void QueuePage(int page, string text, int offset, bool firstHeader)
    {
        bool silent = false;
        lock (_gate)
        {
            if (!_active || voice.Generation != _generation)
            {
                silent = true;
            }
            else
            {
                string header = firstHeader ? BookText.FirstHeader(page, pages.Count, lang) : BookText.Header(page, lang);
                foreach (SpokenSentence s in voice.Say(header, lang))
                {
                    _places[s.Index] = (page, offset);
                }

                IReadOnlyList<SpokenSentence> sentences = voice.Say(text[offset..], lang);
                int[] offsets = SentenceLocator.Locate(text, sentences.Select(s => s.Text).ToArray(), offset);
                for (int i = 0; i < offsets.Length; i++)
                {
                    _places[sentences[i].Index] = (page, offsets[i]);
                }

                _lastFedPage = page;
                _nextPage = page + 1;
                silent = sentences.Count == 0;
            }
        }

        if (silent)
        {
            Stop();
        }
    }

    private void SayEnd()
    {
        IReadOnlyList<SpokenSentence> sentences;
        lock (_gate)
        {
            if (!_active || voice.Generation != _generation)
            {
                return;
            }

            // Every sentence of the last page can reach here, and the end is only said once.
            if (_endIndex >= 0)
            {
                return;
            }

            sentences = voice.Say(BookText.End(lang), lang);
            if (sentences.Count > 0)
            {
                _endIndex = sentences[^1].Index;
            }
        }

        if (sentences.Count == 0)
        {
            Stop();
        }
    }

    private IReadOnlyList<SpokenSentence> Say(string text)
    {
        lock (_gate)
        {
            return StillReadingLocked() ? voice.Say(text, lang) : [];
        }
    }

    private bool StillReading()
    {
        lock (_gate)
        {
            return StillReadingLocked();
        }
    }

    private bool StillReadingLocked() => _active && voice.Generation == _generation;
}

internal static class BookText
{
    public static string FirstHeader(int page, int total, Lang lang) =>
        lang == Lang.Arabic ? $"صفحة {page} من {total}." : $"Page {page} of {total}.";

    public static string Header(int page, Lang lang) => lang == Lang.Arabic ? $"صفحة {page}." : $"Page {page}.";

    public static string PageUnreadable(int page, Lang lang) =>
        lang == Lang.Arabic ? $"مقدرتش أقرأ صفحة {page}." : $"I couldn't read page {page}.";

    public static string Unreadable(int page, Lang lang) =>
        lang == Lang.Arabic ? $"مقدرتش أقرأ الصفحات لحد صفحة {page}، فوقفت هنا." : $"I couldn't read the pages up to page {page}, so I stopped here.";

    public static string End(Lang lang) => lang == Lang.Arabic ? "خلص الكتاب." : "That's the end of the book.";
}
