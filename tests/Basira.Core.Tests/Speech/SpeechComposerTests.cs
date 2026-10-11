using System.Xml.Linq;
using Basira.Core.Common;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.Core.Tests.Speech;

public class SpeechComposerTests
{
    [Fact]
    public void OneSsmlPerSentenceWithHistoryMarks()
    {
        var history = new SpeechHistory();
        history.AddUtterance("Earlier answer.", UtteranceKind.Answer);

        var sentences = SpeechComposer.Compose("مرحبا. كيف حالك؟\nأنا بخير & you?", Lang.Arabic, history, UtteranceKind.Answer);

        Assert.Equal(3, sentences.Count);
        Assert.Equal([2, 3, 4], sentences.Select(s => s.HistoryIndex));
        Assert.Equal("<speak><mark name=\"s2\"/><voice xml:lang=\"ar\">مرحبا.</voice></speak>", sentences[0].Ssml);
        Assert.Equal("<speak><mark name=\"s3\"/><voice xml:lang=\"ar\">كيف حالك؟</voice></speak>", sentences[1].Ssml);
        Assert.Equal(
            "<speak><mark name=\"s4\"/><voice xml:lang=\"ar\">أنا بخير و </voice><voice xml:lang=\"en\">you?</voice></speak>",
            sentences[2].Ssml);
        Assert.Equal("أنا بخير و you?", sentences[2].PlainText);

        var stored = history.LastUtterance();
        Assert.Equal(sentences.Select(s => s.HistoryIndex), stored.Select(i => i.Index));
        Assert.Equal(["مرحبا.", "كيف حالك؟", "أنا بخير & you?"], stored.Select(i => i.Text));
        Assert.Equal([0, 0, 1], stored.Select(i => i.ParagraphIndex));
    }

    [Fact]
    public void MarksMapBackToHistoryItems()
    {
        var history = new SpeechHistory();
        var sentences = SpeechComposer.Compose("One. Two. Three.", Lang.English, history, UtteranceKind.Reading);
        foreach (var sentence in sentences)
        {
            string mark = XElement.Parse(sentence.Ssml).Element("mark")!.Attribute("name")!.Value;
            Assert.True(SpeechComposer.TryParseMark(mark, out int index));
            history.MarkSpoken(index);
            Assert.Equal(sentence.HistoryIndex, history.Current!.Index);
        }
    }

    [Fact]
    public void MixedSentenceWithSpelledAcronym()
    {
        var sentence = Assert.Single(SpeechComposer.Compose(
            "افتح ملف PDF على Windows 10 من فضلك", Lang.Arabic, new SpeechHistory(), UtteranceKind.Answer));
        Assert.Equal(
            "<speak><mark name=\"s1\"/>"
            + "<voice xml:lang=\"ar\">افتح ملف </voice>"
            + "<voice xml:lang=\"en\"><say-as interpret-as=\"characters\">PDF</say-as></voice>"
            + "<voice xml:lang=\"ar\"> على </voice>"
            + "<voice xml:lang=\"en\">Windows </voice>"
            + "<voice xml:lang=\"ar\">10 من فضلك</voice></speak>",
            sentence.Ssml);
        Assert.Equal(5, sentence.Runs.Count);
        Assert.Equal(string.Concat(sentence.Runs.Select(r => r.Text)), sentence.PlainText);
    }

    [Fact]
    public void OptionsAndStyleArePassedThrough()
    {
        var sentence = Assert.Single(SpeechComposer.Compose(
            "x^2 = 4", Lang.English, new SpeechHistory(), UtteranceKind.Answer,
            new SpeechOptions(Mode: VerbalizeMode.Math), LangTagStyle.LangElement));
        Assert.Equal("<speak><mark name=\"s1\"/><lang xml:lang=\"en\">x squared equals 4</lang></speak>", sentence.Ssml);
    }

    [Fact]
    public void HistoryKeepsTheTextAsWritten()
    {
        var history = new SpeechHistory();
        SpeechComposer.Compose("Great 👍 & 50% done.", Lang.English, history, UtteranceKind.Narration);
        var item = Assert.Single(history.LastUtterance());
        Assert.Equal("Great 👍 & 50% done.", item.Text);
        Assert.Equal(UtteranceKind.Narration, item.Kind);
    }

    [Fact]
    public void SentenceThatSaysNothingStillCarriesItsMark()
    {
        var sentence = Assert.Single(SpeechComposer.Compose("🦩", Lang.Arabic, new SpeechHistory(), UtteranceKind.Reading));
        Assert.Equal("<speak><mark name=\"s1\"/></speak>", sentence.Ssml);
        Assert.Equal("", sentence.PlainText);
    }

    [Fact]
    public void EmptyTextComposesNothing()
    {
        var history = new SpeechHistory();
        Assert.Empty(SpeechComposer.Compose("", Lang.Arabic, history, UtteranceKind.System));
        Assert.Equal(0, history.Count);
    }

    [Theory]
    [InlineData(1, "s1")]
    [InlineData(42, "s42")]
    [InlineData(1234567, "s1234567")]
    public void MarkNamesRoundTrip(int index, string mark)
    {
        Assert.Equal(mark, SpeechComposer.MarkName(index));
        Assert.True(SpeechComposer.TryParseMark(mark, out int parsed));
        Assert.Equal(index, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("s")]
    [InlineData("x12")]
    [InlineData("s-3")]
    [InlineData("s 3")]
    [InlineData("s12a")]
    public void InvalidMarksDoNotParse(string? mark) => Assert.False(SpeechComposer.TryParseMark(mark, out _));

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => SpeechComposer.Compose(null!, Lang.Arabic, new SpeechHistory(), UtteranceKind.Answer));
        Assert.Throws<ArgumentNullException>(() => SpeechComposer.Compose("x", Lang.Arabic, null!, UtteranceKind.Answer));
    }
}
