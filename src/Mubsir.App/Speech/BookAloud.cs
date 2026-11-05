using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Mubsir.Core.Reading;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Speech;

/// <summary>Connects a book being read aloud to the real voice, the saved places and the reading keys.</summary>
internal sealed class BookAloud : IBookVoice
{
    private readonly SpeechService _speech;
    private readonly ReadingPositions _positions;
    private readonly Action<bool> _setReading;
    private readonly IEventLog _log;
    private BookReading? _current;

    public BookAloud(SpeechService speech, ReadingPositions positions, Action<bool> setReading, IEventLog log)
    {
        _speech = speech;
        _positions = positions;
        _setReading = setReading;
        _log = log;
        speech.Stopped += () => Volatile.Read(ref _current)?.Stop();

        // This can arrive on NVDA's callback, so the file write and the next page happen elsewhere.
        speech.History.Spoken += index => ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Volatile.Read(ref _current)?.OnSpoken(index);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("book_spoken", e);
            }
        });
    }

    public long Generation => _speech.Generation;

    public async Task<bool> StartAsync(IBookPages pages, string? path, int startPage, int startOffset, Lang lang)
    {
        if (Volatile.Read(ref _current)?.IsActive == true)
        {
            _speech.Stop();
        }

        Action<int, int>? save = path is null ? null : (page, offset) => _positions.Set(path, page, offset);
        var reading = new BookReading(this, pages, save, () => _setReading(false), lang);
        Volatile.Write(ref _current, reading);
        bool started = await reading.StartAsync(startPage, startOffset).ConfigureAwait(false);
        if (started && reading.IsActive)
        {
            _setReading(true);
            _log.Event(LogKind.Speech, tool: "read_book");
        }

        return started;
    }

    public IReadOnlyList<SpokenSentence> Say(string text, Lang lang)
    {
        IReadOnlyList<int> indices = _speech.Enqueue(
            text, UtteranceKind.Reading, ScriptSegmenter.DominantLanguage(text, lang), interrupt: false, VerbalizeMode.Normal, done: null);
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
}
