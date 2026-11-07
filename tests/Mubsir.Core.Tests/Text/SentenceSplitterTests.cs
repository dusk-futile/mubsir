using Mubsir.Core.Text;

namespace Mubsir.Core.Tests.Text;

/// <summary>Sentences are where the reader breathes; splitting in the wrong place sounds robotic.</summary>
public sealed class SentenceSplitterTests
{
    [Fact]
    public void Abbreviations_and_decimals_do_not_end_a_sentence()
    {
        Assert.Equal(
            ["Dr. Smith arrived at 5 p.m.", "He was late.", "Then he left!"],
            SentenceSplitter.Split("Dr. Smith arrived at 5 p.m. He was late. Then he left!"));
        Assert.Equal(
            ["Version 3.5 is out.", "See e.g. the notes."],
            SentenceSplitter.Split("Version 3.5 is out. See e.g. the notes."));
    }

    [Fact]
    public void Arabic_punctuation_ends_sentences()
    {
        Assert.Equal(["مرحبا.", "كيف حالك؟", "أنا بخير!", "وأنت"], SentenceSplitter.Split("مرحبا. كيف حالك؟ أنا بخير! وأنت"));
    }

    [Fact]
    public void A_quoted_sentence_keeps_its_closing_quote()
    {
        Assert.Equal(["قال أحمد: \"اذهب.\"", "ثم ذهب.", "The end."], SentenceSplitter.Split("قال أحمد: \"اذهب.\" ثم ذهب. The end."));
    }

    [Fact]
    public void Line_breaks_end_sentences()
    {
        Assert.Equal(["Line one", "Line two", "New para"], SentenceSplitter.Split("Line one\nLine two\n\nNew para"));
    }

    [Fact]
    public void No_sentence_is_longer_than_the_limit()
    {
        string longText = string.Join(", ", Enumerable.Repeat("a long clause without any full stop", 40));

        Assert.All(SentenceSplitter.Split(longText), s => Assert.True(s.Length <= SentenceSplitter.MaxSentenceLength));
    }

    [Fact]
    public void Splitting_keeps_every_word()
    {
        const string text = "First one. Second, with a comma! Third? الرابعة. Fifth";
        string joined = string.Join(" ", SentenceSplitter.Split(text));

        Assert.Equal(text.Split(' ').Length, joined.Split(' ').Length);
    }
}
