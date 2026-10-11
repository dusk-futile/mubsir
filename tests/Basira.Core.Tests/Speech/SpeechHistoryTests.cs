using Basira.Core.Speech;

namespace Basira.Core.Tests.Speech;

public class SpeechHistoryTests
{
    /// <summary>Two paragraphs: (1, 2, 3) and (4, 5).</summary>
    private const string TwoParagraphs = "First one. Second one. Third one.\nFourth one. Fifth one.";

    [Fact]
    public void AddUtteranceSplitsParagraphsAndSentences()
    {
        var history = new SpeechHistory();
        int id = history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);

        var items = history.ItemsOf(id);
        Assert.Equal(["First one.", "Second one.", "Third one.", "Fourth one.", "Fifth one."], items.Select(i => i.Text));
        Assert.Equal([0, 0, 0, 1, 1], items.Select(i => i.ParagraphIndex));
        Assert.Equal([1, 2, 3, 4, 5], items.Select(i => i.Index));
        Assert.All(items, i => Assert.Equal(id, i.UtteranceId));
        Assert.All(items, i => Assert.Equal(UtteranceKind.Reading, i.Kind));
        Assert.Equal(5, history.Count);
    }

    [Fact]
    public void ArabicUtterance()
    {
        var history = new SpeechHistory();
        int id = history.AddUtterance("مرحبا بك. كيف حالك؟\n\nأنا بصيرة، مساعدك.", UtteranceKind.Answer);
        Assert.Equal(["مرحبا بك.", "كيف حالك؟", "أنا بصيرة، مساعدك."], history.ItemsOf(id).Select(i => i.Text));
        Assert.Equal([0, 0, 1], history.ItemsOf(id).Select(i => i.ParagraphIndex));
    }

    [Fact]
    public void IndicesAndIdsAreGlobalAndMonotonic()
    {
        var history = new SpeechHistory();
        int a = history.AddUtterance("One. Two.", UtteranceKind.Answer);
        int b = history.AddUtterance("Three.", UtteranceKind.System);
        history.Clear();
        int c = history.AddUtterance("Four. Five.", UtteranceKind.Narration);

        Assert.True(a < b && b < c);
        Assert.Empty(history.ItemsOf(a));
        Assert.Equal([4, 5], history.ItemsOf(c).Select(i => i.Index));
    }

    [Fact]
    public void EmptyTextAddsAnUtteranceWithoutSentences()
    {
        var history = new SpeechHistory();
        int id = history.AddUtterance("  \n ", UtteranceKind.System);
        Assert.Empty(history.ItemsOf(id));
        Assert.Equal(0, history.Count);
        Assert.Equal(id + 1, history.AddUtterance("x", UtteranceKind.System));
    }

    [Fact]
    public void CursorStartsEmptyAndFollowsSpokenMarks()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        Assert.Null(history.Current);

        history.MarkSpoken(2);
        Assert.Equal("Second one.", history.Current!.Text);

        history.MarkSpoken(999); // unknown marks are ignored
        Assert.Equal(2, history.Current!.Index);
        history.MarkSpoken(0);
        Assert.Equal(2, history.Current!.Index);
    }

    [Fact]
    public void PreviousAndNextMoveOneSentenceAndStopAtTheEnds()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        history.MarkSpoken(2);

        Assert.Equal(1, history.Previous()!.Index);
        Assert.Null(history.Previous());
        Assert.Equal(1, history.Current!.Index); // no move past the start

        Assert.Equal(2, history.Next()!.Index);
        Assert.Equal(3, history.Next()!.Index);
        Assert.Equal(4, history.Next()!.Index);
        Assert.Equal(5, history.Next()!.Index);
        Assert.Null(history.Next());
        Assert.Equal(5, history.Current!.Index); // no move past the end
    }

    [Fact]
    public void BeforeAnythingIsSpokenPreviousGivesTheLastSentence()
    {
        var history = new SpeechHistory();
        Assert.Null(history.Previous());
        Assert.Null(history.Next());
        Assert.Null(history.PreviousParagraph());
        Assert.Null(history.NextParagraph());

        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        Assert.Null(history.Next());
        Assert.Equal(5, history.Previous()!.Index);
        Assert.Equal(5, history.Current!.Index);
    }

    [Fact]
    public void PreviousParagraphGoesToTheStartOfTheCurrentParagraphFirst()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        history.MarkSpoken(5);

        Assert.Equal(4, history.PreviousParagraph()!.Index); // start of the current paragraph
        Assert.Equal(1, history.PreviousParagraph()!.Index); // start of the previous paragraph
        Assert.Null(history.PreviousParagraph());
        Assert.Equal(1, history.Current!.Index);

        history.MarkSpoken(3);
        Assert.Equal(1, history.PreviousParagraph()!.Index);
    }

    [Fact]
    public void NextParagraphGoesToTheFirstSentenceOfTheNextParagraph()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        history.MarkSpoken(2);

        Assert.Equal(4, history.NextParagraph()!.Index);
        Assert.Null(history.NextParagraph());
        Assert.Equal(4, history.Current!.Index);
    }

    [Fact]
    public void ParagraphsDoNotSpanUtterances()
    {
        var history = new SpeechHistory();
        int first = history.AddUtterance("A one. A two.", UtteranceKind.Answer);
        int second = history.AddUtterance("B one. B two.", UtteranceKind.Answer);
        Assert.All(history.ItemsOf(first).Concat(history.ItemsOf(second)), i => Assert.Equal(0, i.ParagraphIndex));

        history.MarkSpoken(1);
        Assert.Equal("B one.", history.NextParagraph()!.Text);
        Assert.Equal("A one.", history.PreviousParagraph()!.Text);
        history.MarkSpoken(4);
        Assert.Equal("B one.", history.PreviousParagraph()!.Text);
    }

    [Fact]
    public void BeforeAnythingIsSpokenPreviousParagraphGoesToTheLastParagraph()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        Assert.Equal(4, history.PreviousParagraph()!.Index);
    }

    [Fact]
    public void LastSentenceAndLastUtteranceDoNotMoveTheCursor()
    {
        var history = new SpeechHistory();
        Assert.Null(history.LastSentence());
        Assert.Empty(history.LastUtterance());

        history.AddUtterance("Old answer.", UtteranceKind.Answer);
        int id = history.AddUtterance("New one. New two.", UtteranceKind.Answer);
        history.AddUtterance("", UtteranceKind.System);

        Assert.Equal("New two.", history.LastSentence()!.Text);
        Assert.Equal(["New one.", "New two."], history.LastUtterance().Select(i => i.Text));
        Assert.All(history.LastUtterance(), i => Assert.Equal(id, i.UtteranceId));
        Assert.Null(history.Current);
    }

    [Fact]
    public void FromReturnsTheRestForSayAll()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);

        Assert.Equal([3, 4, 5], history.From(3).Select(i => i.Index));
        Assert.Equal([5], history.From(5).Select(i => i.Index));
        Assert.Empty(history.From(6));
        Assert.Equal(5, history.From(-10).Count);
        Assert.Null(history.Current);
    }

    [Fact]
    public void OldestSentencesAreDroppedAtCapacity()
    {
        var history = new SpeechHistory(capacity: 4);
        int first = history.AddUtterance("One. Two. Three.", UtteranceKind.Reading);
        history.MarkSpoken(1);
        int second = history.AddUtterance("Four. Five. Six.", UtteranceKind.Reading);

        Assert.Equal(4, history.Count);
        Assert.Equal(["Three."], history.ItemsOf(first).Select(i => i.Text));
        Assert.Equal(3, history.ItemsOf(second).Count);
        Assert.Equal(3, history.Current!.Index); // the cursor moves to the oldest kept sentence
        Assert.Null(history.Previous());
        Assert.Equal([3, 4, 5, 6], history.From(1).Select(i => i.Index));

        history.MarkSpoken(2); // dropped: ignored
        Assert.Equal(3, history.Current!.Index);
    }

    [Fact]
    public void UtteranceLargerThanCapacityKeepsItsLastSentences()
    {
        var history = new SpeechHistory(capacity: 2);
        var (id, created) = history.AddUtteranceWithItems("One. Two. Three. Four.", UtteranceKind.Reading);
        Assert.Equal(4, created.Count);
        Assert.Equal(["Three.", "Four."], history.ItemsOf(id).Select(i => i.Text));
    }

    [Fact]
    public void DefaultCapacityIs500()
    {
        var history = new SpeechHistory();
        Assert.Equal(500, history.Capacity);
        history.AddUtterance(string.Join(' ', Enumerable.Range(1, 600).Select(i => $"Sentence {i}.")), UtteranceKind.Reading);
        Assert.Equal(500, history.Count);
        Assert.Equal("Sentence 101.", history.From(0)[0].Text);
        Assert.Equal("Sentence 600.", history.LastSentence()!.Text);
    }

    [Fact]
    public void ClearForgetsSentencesAndCursor()
    {
        var history = new SpeechHistory();
        history.AddUtterance(TwoParagraphs, UtteranceKind.Reading);
        history.MarkSpoken(3);
        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.Null(history.Current);
        Assert.Null(history.LastSentence());
        Assert.Empty(history.From(0));
        Assert.Null(history.Previous());
    }

    [Fact]
    public void InvalidCapacityThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpeechHistory(0));
        Assert.Throws<ArgumentNullException>(() => new SpeechHistory().AddUtterance(null!, UtteranceKind.Answer));
    }

    [Fact]
    public async Task ConcurrentUseKeepsIndicesUniqueAndContiguous()
    {
        var history = new SpeechHistory(capacity: 10_000);
        var writers = Enumerable.Range(0, 8).Select(w => Task.Run(() =>
        {
            for (int k = 0; k < 50; k++)
            {
                history.AddUtterance($"Writer {w} line {k}. Second sentence.", UtteranceKind.Narration);
            }
        }));
        var readers = Enumerable.Range(0, 4).Select(r => Task.Run(() =>
        {
            for (int k = 0; k < 500; k++)
            {
                if (history.LastSentence() is { } last)
                {
                    history.MarkSpoken(last.Index);
                }

                history.Previous();
                history.NextParagraph();
                history.PreviousParagraph();
                _ = history.Current;
                _ = history.From(k);
                _ = history.LastUtterance();
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        var all = history.From(0);
        Assert.Equal(800, all.Count);
        Assert.Equal(Enumerable.Range(1, 800), all.Select(i => i.Index));
        Assert.All(all.GroupBy(i => i.UtteranceId), g => Assert.Equal(2, g.Count()));
    }
}
