using Mubsir.Core.Common;
using Mubsir.Core.Speech;

namespace Mubsir.Core.Tests.Speech;

public sealed class SpeechCompositionTests
{
    [Fact]
    public void Mixed_speech_becomes_one_voice_element_per_language_run()
    {
        var sentences = SpeechComposer.Compose("افتح Chrome now. Then قول hello.", Lang.Arabic, new SpeechHistory(), UtteranceKind.Answer);

        Assert.Equal(2, sentences.Count);
        Assert.Equal("<speak><mark name=\"s1\"/><voice xml:lang=\"ar\">افتح </voice><voice xml:lang=\"en\">Chrome now.</voice></speak>", sentences[0].Ssml);
        Assert.Equal("<speak><mark name=\"s2\"/><voice xml:lang=\"en\">Then </voice><voice xml:lang=\"ar\">قول </voice><voice xml:lang=\"en\">hello.</voice></speak>", sentences[1].Ssml);
    }

    [Fact]
    public void Ssml_escapes_markup_characters()
    {
        var sentence = SpeechComposer.Compose("Use <b> & \"quotes\"", Lang.English, new SpeechHistory(), UtteranceKind.Answer).Single();

        Assert.DoesNotContain("<b>", sentence.Ssml, StringComparison.Ordinal);
        Assert.Contains("&lt;", sentence.Ssml, StringComparison.Ordinal);
    }

    [Fact]
    public void Marks_round_trip()
    {
        Assert.True(SpeechComposer.TryParseMark(SpeechComposer.MarkName(42), out int index));
        Assert.Equal(42, index);
        Assert.False(SpeechComposer.TryParseMark("x42", out _));
    }
}
