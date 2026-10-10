using Basira.Core.Text;

namespace Basira.Core.Speech;

/// <summary>What produced an utterance.</summary>
public enum UtteranceKind
{
    /// <summary>An AI answer.</summary>
    Answer,

    /// <summary>Text read from the screen or a document.</summary>
    Reading,

    /// <summary>A short description of an agent step.</summary>
    Narration,

    /// <summary>Basira's own messages (errors, confirmations, status).</summary>
    System,
}

/// <summary>One spoken sentence.</summary>
/// <param name="Index">Global, monotonic position in the history; also the SSML mark ("s{Index}").</param>
/// <param name="UtteranceId">The utterance this sentence belongs to.</param>
/// <param name="ParagraphIndex">Paragraph number within the utterance, from 0.</param>
/// <param name="Text">The sentence as written (before symbol and emoji verbalization).</param>
/// <param name="Kind">What produced the utterance.</param>
public sealed record SpeechItem(int Index, int UtteranceId, int ParagraphIndex, string Text, UtteranceKind Kind);

/// <summary>
/// Everything Basira said recently, sentence by sentence, with a cursor for rewind, repeat and say-all.
/// Kept in memory only. Thread-safe.
/// </summary>
/// <remarks>
/// Indices start at 1 and never repeat, even after <see cref="Clear"/> or when old sentences are dropped.
/// The cursor moves when a sentence is spoken (<see cref="MarkSpoken"/>) or by navigation. When nothing has been
/// spoken yet, the cursor sits after the last sentence: <see cref="Previous"/> returns the last sentence and
/// <see cref="Next"/> returns null.
/// </remarks>
public sealed class SpeechHistory
{
    public const int DefaultCapacity = 500;

    private readonly object _gate = new();
    private readonly List<SpeechItem> _items = [];
    private int _nextIndex = 1;
    private int _nextUtteranceId = 1;
    private int? _cursor;

    public SpeechHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    /// <summary>Maximum number of sentences kept; the oldest are dropped first.</summary>
    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>The sentence at the cursor, or null when nothing has been spoken or navigated to.</summary>
    public SpeechItem? Current
    {
        get
        {
            lock (_gate)
            {
                return _cursor is int c ? ItemAt(c) : null;
            }
        }
    }

    /// <summary>Splits <paramref name="text"/> into paragraphs and sentences and stores them; returns the utterance id.</summary>
    public int AddUtterance(string text, UtteranceKind kind) => AddUtteranceWithItems(text, kind).UtteranceId;

    /// <summary>
    /// Like <see cref="AddUtterance"/>, also returning every sentence created, including any already dropped
    /// because the utterance alone exceeds <see cref="Capacity"/>.
    /// </summary>
    public (int UtteranceId, IReadOnlyList<SpeechItem> Items) AddUtteranceWithItems(string text, UtteranceKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sentences = new List<(int Paragraph, string Text)>();
        var paragraphs = SentenceSplitter.Paragraphs(text);
        for (int p = 0; p < paragraphs.Count; p++)
        {
            foreach (string sentence in SentenceSplitter.Split(paragraphs[p]))
            {
                sentences.Add((p, sentence));
            }
        }

        lock (_gate)
        {
            int id = _nextUtteranceId++;
            var created = new List<SpeechItem>(sentences.Count);
            foreach (var (paragraph, sentence) in sentences)
            {
                var item = new SpeechItem(_nextIndex++, id, paragraph, sentence, kind);
                created.Add(item);
                _items.Add(item);
            }

            int overflow = _items.Count - Capacity;
            if (overflow > 0)
            {
                _items.RemoveRange(0, overflow);
                if (_cursor is int c && c < _items[0].Index)
                {
                    _cursor = _items[0].Index;
                }
            }

            return (id, created);
        }
    }

    /// <summary>The stored sentences of one utterance, in order.</summary>
    public IReadOnlyList<SpeechItem> ItemsOf(int utteranceId)
    {
        lock (_gate)
        {
            return _items.Where(i => i.UtteranceId == utteranceId).ToArray();
        }
    }

    /// <summary>Called when the voice reaches a sentence's mark; moves the cursor there. Unknown indices are ignored.</summary>
    public void MarkSpoken(int index)
    {
        lock (_gate)
        {
            if (ItemAt(index) is not null)
            {
                _cursor = index;
            }
        }
    }

    /// <summary>Moves to the previous sentence; null (and no move) at the first one.</summary>
    public SpeechItem? Previous()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                return null;
            }

            if (_cursor is not int c)
            {
                return MoveTo(_items.Count - 1);
            }

            int pos = PositionOf(c);
            return pos > 0 ? MoveTo(pos - 1) : null;
        }
    }

    /// <summary>Moves to the next sentence; null (and no move) at the last one or when nothing was spoken yet.</summary>
    public SpeechItem? Next()
    {
        lock (_gate)
        {
            if (_cursor is not int c)
            {
                return null;
            }

            int pos = PositionOf(c);
            return pos < _items.Count - 1 ? MoveTo(pos + 1) : null;
        }
    }

    /// <summary>
    /// Moves to the start of the current paragraph, or, when already there, to the start of the previous one.
    /// </summary>
    public SpeechItem? PreviousParagraph()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                return null;
            }

            if (_cursor is not int c)
            {
                return MoveTo(ParagraphStart(_items.Count - 1));
            }

            int pos = PositionOf(c);
            int start = ParagraphStart(pos);
            if (start < pos)
            {
                return MoveTo(start);
            }

            return start > 0 ? MoveTo(ParagraphStart(start - 1)) : null;
        }
    }

    /// <summary>Moves to the first sentence of the next paragraph; null (and no move) when there is none.</summary>
    public SpeechItem? NextParagraph()
    {
        lock (_gate)
        {
            if (_cursor is not int c)
            {
                return null;
            }

            int pos = PositionOf(c);
            for (int p = pos + 1; p < _items.Count; p++)
            {
                if (!SameParagraph(_items[p], _items[pos]))
                {
                    return MoveTo(p);
                }
            }

            return null;
        }
    }

    /// <summary>The most recently added sentence (does not move the cursor).</summary>
    public SpeechItem? LastSentence()
    {
        lock (_gate)
        {
            return _items.Count > 0 ? _items[^1] : null;
        }
    }

    /// <summary>The stored sentences of the most recent utterance that has any.</summary>
    public IReadOnlyList<SpeechItem> LastUtterance()
    {
        lock (_gate)
        {
            if (_items.Count == 0)
            {
                return [];
            }

            int id = _items[^1].UtteranceId;
            int start = _items.Count - 1;
            while (start > 0 && _items[start - 1].UtteranceId == id)
            {
                start--;
            }

            return _items.GetRange(start, _items.Count - start).ToArray();
        }
    }

    /// <summary>All stored sentences from <paramref name="index"/> on (for continuing a say-all).</summary>
    public IReadOnlyList<SpeechItem> From(int index)
    {
        lock (_gate)
        {
            if (_items.Count == 0 || index > _items[^1].Index)
            {
                return [];
            }

            int pos = Math.Max(0, index - _items[0].Index);
            return _items.GetRange(pos, _items.Count - pos).ToArray();
        }
    }

    /// <summary>Forgets everything; indices and utterance ids keep counting up.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _cursor = null;
        }
    }

    // Indices are contiguous (sentences are only dropped from the front), so position = index - first index.
    private int PositionOf(int index) => index - _items[0].Index;

    private SpeechItem? ItemAt(int index)
    {
        if (_items.Count == 0)
        {
            return null;
        }

        int pos = PositionOf(index);
        return pos >= 0 && pos < _items.Count ? _items[pos] : null;
    }

    private SpeechItem MoveTo(int position)
    {
        var item = _items[position];
        _cursor = item.Index;
        return item;
    }

    private int ParagraphStart(int position)
    {
        while (position > 0 && SameParagraph(_items[position - 1], _items[position]))
        {
            position--;
        }

        return position;
    }

    private static bool SameParagraph(SpeechItem a, SpeechItem b) =>
        a.UtteranceId == b.UtteranceId && a.ParagraphIndex == b.ParagraphIndex;
}
