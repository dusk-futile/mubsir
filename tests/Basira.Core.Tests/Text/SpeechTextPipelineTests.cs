using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class SpeechTextPipelineTests
{
    private static string Prepare(string text, Lang lang, SpeechOptions? options = null) =>
        RunAssert.Describe(SpeechTextPipeline.Prepare(text, lang, options));

    [Fact]
    public void DefaultsSpeakEmojiTransliterateArabiziAndReadNormally()
    {
        var options = new SpeechOptions();
        Assert.True(options.TransliterateArabizi);
        Assert.True(options.SpeakEmoji);
        Assert.Equal(VerbalizeMode.Normal, options.Mode);
        Assert.Equal(Prepare("مبروك 7abibi 🎉", Lang.Arabic), Prepare("مبروك 7abibi 🎉", Lang.Arabic, options));
    }

    [Fact]
    public void EmptyTextHasNoRuns() => Assert.Empty(SpeechTextPipeline.Prepare("", Lang.Arabic));

    [Fact]
    public void NullThrows() => Assert.Throws<ArgumentNullException>(() => SpeechTextPipeline.Prepare(null!, Lang.Arabic));

    [Fact]
    public void PresentationFormsAreNormalizedFirst() =>
        Assert.Equal("ar:مرحبا لا", Prepare("ﻣﺮﺣﺒﺎ ﻻ", Lang.English));

    [Fact]
    public void MixedArabicSentence() =>
        Assert.Equal(
            "ar:افتح ملف |en*:PDF|ar: على |en:Windows |ar:10 من فضلك",
            Prepare("افتح ملف PDF على Windows 10 من فضلك", Lang.Arabic));

    [Fact]
    public void WhatsAppLineWithArabiziEmojiPercentAndAcronym() =>
        Assert.Equal(
            "ar:مبروك يا حبيبي (احتفال، ثلاث مرات) خصم 50 بالمئة على |en*:USB",
            Prepare("مبروك يا 7abibi 🎉🎉🎉 خصم 50% على USB", Lang.Arabic));

    [Fact]
    public void ArabiziChatBecomesArabicAndEnglishStaysEnglish() =>
        Assert.Equal(
            "ar:حبيبي (وجه يبكي من الضحك، مرتين) ازيك؟ |en:meeting at 5pm",
            Prepare("7abibi 😂😂 ezayak? meeting at 5pm", Lang.Arabic));

    [Fact]
    public void EmojiAreNamedInTheSentencesLanguage()
    {
        Assert.Equal("en:Great job (clapping hands) team", Prepare("Great job 👏 team", Lang.Arabic));
        Assert.Equal("ar:أحسنت (تصفيق) يا بطل", Prepare("أحسنت 👏 يا بطل", Lang.English));
    }

    [Fact]
    public void ArabiziCanBeTurnedOff() =>
        Assert.Equal("en:yalla 7abibi", Prepare("yalla 7abibi", Lang.Arabic, new SpeechOptions(TransliterateArabizi: false)));

    [Fact]
    public void EmojiCanBeSilenced()
    {
        Assert.Equal("en:ok", Prepare("ok 👍", Lang.Arabic, new SpeechOptions(SpeakEmoji: false)));
        Assert.Equal("ar:تمام شكرا", Prepare("تمام 👍👍 شكرا", Lang.Arabic, new SpeechOptions(SpeakEmoji: false)));
    }

    [Fact]
    public void SymbolsUseTheSentencesLanguage()
    {
        Assert.Equal("en:Tom and Jerry", Prepare("Tom & Jerry", Lang.Arabic));
        Assert.Equal("ar:أحمد و محمد", Prepare("أحمد & محمد", Lang.English));
        Assert.Equal("ar:الحرارة 30 درجة مئوية", Prepare("الحرارة 30°C", Lang.English));
    }

    [Fact]
    public void UrlInsideArabicIsReadWhole() =>
        Assert.Equal(
            "ar:زر الموقع |en:https://example.com/page?id=3 |ar:الآن",
            Prepare("زر الموقع https://example.com/page?id=3 الآن", Lang.Arabic));

    [Fact]
    public void EmailInsideArabicReadsAt() =>
        Assert.Equal(
            "ar:راسلني على |en:ahmed |ar:آت |en:example.com",
            Prepare("راسلني على ahmed@example.com", Lang.Arabic));

    [Fact]
    public void MathModeUsesTheDefaultLanguage()
    {
        var math = new SpeechOptions(Mode: VerbalizeMode.Math);
        Assert.Equal("en:x squared plus 1 equals 5", Prepare("x^2 + 1 = 5", Lang.English, math));
        Assert.Equal("en:x |ar:تربيع زائد 1 يساوي 5", Prepare("x^2 + 1 = 5", Lang.Arabic, math));
        Assert.Equal("ar:س تربيع زائد 1 يساوي 5", Prepare("س^2 + 1 = 5", Lang.Arabic, math));
    }

    [Fact]
    public void SpellModeReadsExactCharacters()
    {
        var spell = new SpeechOptions(Mode: VerbalizeMode.Spell);
        Assert.Equal("en:capital P, capital D, capital F", Prepare("PDF", Lang.English, spell));
        Assert.Equal("en:P |ar:كبير, |en:D |ar:كبير", Prepare("PD", Lang.Arabic, spell));

        // Arabizi is spelled as typed, not transliterated.
        Assert.Equal("ar:سبعة, |en:a", Prepare("7a", Lang.Arabic, spell));
        Assert.Equal("ar:حاء, باء", Prepare("حب", Lang.Arabic, spell));
    }

    [Fact]
    public void PipelineDoesNotReorderText()
    {
        // Reversed-line repair belongs to document reading, not to speech.
        var runs = SpeechTextPipeline.Prepare("نآلا Windows", Lang.Arabic);
        Assert.Equal("نآلا Windows", string.Concat(runs.Select(r => r.Text)));
    }
}
