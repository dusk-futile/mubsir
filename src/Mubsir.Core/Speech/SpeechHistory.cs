using Mubsir.Core.Text;

namespace Mubsir.Core.Speech;

public enum UtteranceKind
{
    Answer,

    Reading,

    Narration,

    System,
}

public sealed record SpeechItem(int Index, int UtteranceId, int ParagraphIndex, string Text, UtteranceKind Kind);

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

    public SpeechItem? LastSentence()
    {
        lock (_gate)
        {
            return _items.Count > 0 ? _items[^1] : null;
        }
    }

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

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _cursor = null;
        }
    }

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
