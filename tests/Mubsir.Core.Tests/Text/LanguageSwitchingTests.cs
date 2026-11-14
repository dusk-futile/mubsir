using Mubsir.Core.Common;
using Mubsir.Core.Text;

namespace Mubsir.Core.Tests.Text;

/// <summary>
/// Arabic and English mixed in one sentence must reach the right voice word by word, with nothing lost and
/// nothing read twice, the way a bilingual person reads it aloud.
/// </summary>
public sealed class LanguageSwitchingTests
{
    private static IReadOnlyList<TextRun> Runs(string text, Lang defaultLang = Lang.Arabic) =>
        SpeechTextPipeline.Prepare(text, defaultLang);

    private static string Shape(IReadOnlyList<TextRun> runs) =>
        string.Join(" | ", runs.Select(r => (r.Lang == Lang.Arabic ? "ar:" : "en:") + r.Text.Trim()));

    [Theory]
    [InlineData("افتح Google Chrome وابحث عن prayer times", "ar:افتح | en:Google Chrome | ar:وابحث عن | en:prayer times")]
    [InlineData("I love القاهرة so much", "en:I love | ar:القاهرة | en:so much")]
    [InlineData("WhatsApp و Telegram و YouTube", "en:WhatsApp | ar:و | en:Telegram | ar:و | en:YouTube")]
    [InlineData("مرحبا،كيف الحال؟I'm fine.", "ar:مرحبا،كيف الحال؟ | en:I'm fine.")]
    public void Each_word_goes_to_the_voice_of_its_language(string text, string expected)
    {
        Assert.Equal(expected, Shape(Runs(text)));
        Assert.Equal(expected, Shape(Runs(text, Lang.English)));
    }

    [Theory]
    [InlineData("اقرأ الصفحة 12 من chapter 3", "ar:اقرأ الصفحة 12 من | en:chapter 3")]
    [InlineData("الـ iPhone 15 Pro سعره 1,299", "ar:الـ | en:iPhone 15 Pro | ar:سعره 1,299")]
    [InlineData("Windows 11 جميل", "en:Windows 11 | ar:جميل")]
    [InlineData("اتصل على 01012345678 الساعة 3:30", "ar:اتصل على 01012345678 الساعة 3:30")]
    public void A_number_is_read_in_the_language_of_its_label(string text, string expected)
    {
        Assert.Equal(expected, Shape(Runs(text)));
    }

    [Theory]
    [InlineData("قال: \"hello world\" ثم مشى.", "ar:قال: | en:\"hello world\" | ar:ثم مشى.")]
    [InlineData("يا (Ahmed) عامل إيه؟", "ar:يا | en:(Ahmed) | ar:عامل إيه؟")]
    public void Brackets_and_quotes_stay_with_the_words_they_wrap(string text, string expected)
    {
        Assert.Equal(expected, Shape(Runs(text)));
    }

    [Theory]
    [InlineData("افتح Google Chrome وابحث عن prayer times")]
    [InlineData("قال: \"hello world\" ثم مشى.")]
    [InlineData("الـ iPhone 15 Pro سعره 1,299 جنيه")]
    [InlineData("كَتَبَ الطَّالِبُ the lesson في الفصل")]
    public void Switching_never_drops_or_repeats_text(string text)
    {
        Assert.Equal(text, string.Concat(ScriptSegmenter.Segment(text, Lang.Arabic).Select(r => r.Text)));
    }

    [Fact]
    public void The_voice_changes_only_where_the_language_does()
    {
        var runs = Runs("افتح Chrome وبعدين open YouTube وشغل music");

        for (int i = 1; i < runs.Count; i++)
        {
            Assert.True(runs[i - 1].Lang != runs[i].Lang || runs[i - 1].SpellOut != runs[i].SpellOut);
        }
    }

    [Fact]
    public void Arabizi_is_read_as_arabic()
    {
        var runs = Runs("3abeer 7abibi ezayak", Lang.English);

        Assert.Single(runs);
        Assert.Equal(Lang.Arabic, runs[0].Lang);
        Assert.Equal("عبير حبيبي ازيك", runs[0].Text);
    }

    [Fact]
    public void Links_and_emails_stay_in_english_inside_arabic()
    {
        var runs = Runs("ابعت على omar.k@gmail.com بسرعة");

        Assert.Contains(runs, r => r.Lang == Lang.English && r.Text.Contains("gmail.com", StringComparison.Ordinal));
        Assert.Equal(Lang.Arabic, runs[0].Lang);
        Assert.Equal(Lang.Arabic, runs[^1].Lang);
    }

    [Fact]
    public void An_english_acronym_in_arabic_is_spelled()
    {
        var runs = Runs("حمل ملف PDF من فضلك");

        Assert.Contains(runs, r => r.SpellOut && r.Text.Trim() == "PDF");
    }

    [Fact]
    public void Text_with_no_letters_takes_the_default_language()
    {
        Assert.Equal(Lang.Arabic, Runs("123 456", Lang.Arabic).Single().Lang);
        Assert.Equal(Lang.English, Runs("123 456", Lang.English).Single().Lang);
    }
}
