using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class ScriptSegmenterTests
{
    private static string Seg(string text, Lang defaultLang = Lang.Arabic)
    {
        var runs = ScriptSegmenter.Segment(text, defaultLang);
        RunAssert.Lossless(text, runs);
        return RunAssert.Describe(runs);
    }

    [Fact]
    public void EmptyTextHasNoRuns()
    {
        Assert.Empty(ScriptSegmenter.Segment("", Lang.Arabic));
        Assert.Empty(ScriptSegmenter.Segment("", Lang.English));
    }

    [Fact]
    public void NullThrows() => Assert.Throws<ArgumentNullException>(() => ScriptSegmenter.Segment(null!, Lang.Arabic));

    [Theory]
    [InlineData("   ", Lang.Arabic, "ar:   ")]
    [InlineData(" \t\n ", Lang.English, "en: \t\n ")]
    public void WhitespaceOnlyIsOneRunInTheDefaultLanguage(string text, Lang lang, string expected) =>
        Assert.Equal(expected, Seg(text, lang));

    [Theory]
    [InlineData("مرحبا بك في بصيرة", Lang.English, "ar:مرحبا بك في بصيرة")]
    [InlineData("Hello and welcome", Lang.Arabic, "en:Hello and welcome")]
    [InlineData("café naïve résumé", Lang.Arabic, "en:café naïve résumé")]
    [InlineData("بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ", Lang.English, "ar:بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ")]
    public void SingleLanguageTextIsOneRun(string text, Lang lang, string expected) => Assert.Equal(expected, Seg(text, lang));

    [Fact]
    public void ArabicSentenceWithAcronymProductAndNumber()
    {
        Assert.Equal(
            "ar:افتح ملف |en*:PDF|ar: على |en:Windows |ar:10 من فضلك",
            Seg("افتح ملف PDF على Windows 10 من فضلك"));
    }

    [Fact]
    public void EnglishSentenceWithArabicName()
    {
        Assert.Equal("en:Meeting at 5 |ar:مع أحمد", Seg("Meeting at 5 مع أحمد"));
        Assert.Equal("en:Meeting at 5 |ar:مع أحمد", Seg("Meeting at 5 مع أحمد", Lang.English));
    }

    [Theory]
    [InlineData("السعر 25 دولار", "ar:السعر 25 دولار")]
    [InlineData("السعر ٢٥ دولار", "ar:السعر ٢٥ دولار")]
    [InlineData("The price is 25 dollars", "en:The price is 25 dollars")]
    [InlineData("The price is ٢٥ dollars", "en:The price is ٢٥ dollars")]
    [InlineData("The price is ۲۵ dollars", "en:The price is ۲۵ dollars")]
    public void DigitsFollowTheDominantLanguage(string text, string expected) => Assert.Equal(expected, Seg(text));

    [Theory]
    [InlineData("123 456", Lang.Arabic, "ar:123 456")]
    [InlineData("123 456", Lang.English, "en:123 456")]
    [InlineData("٣٫١٤ + 2 = ?", Lang.English, "en:٣٫١٤ + 2 = ?")]
    [InlineData("!!! ... ???", Lang.Arabic, "ar:!!! ... ???")]
    public void TextWithoutLettersUsesTheDefaultLanguage(string text, Lang lang, string expected) =>
        Assert.Equal(expected, Seg(text, lang));

    [Fact]
    public void LetterCountTiesGoToTheDefaultLanguage()
    {
        // Two Latin letters, two Arabic letters: the digit follows the default language.
        Assert.Equal("en:ok |ar:5 لا", Seg("ok 5 لا", Lang.Arabic));
        Assert.Equal("en:ok 5 |ar:لا", Seg("ok 5 لا", Lang.English));
    }

    [Fact]
    public void DigitsFollowEachSentencesOwnDominantLanguage()
    {
        var runs = ScriptSegmenter.Segment("Call me at 5. سأتصل بك الساعة 6.", Lang.Arabic);
        RunAssert.Lossless("Call me at 5. سأتصل بك الساعة 6.", runs);
        Assert.Equal("en:Call me at 5. |ar:سأتصل بك الساعة 6.", RunAssert.Describe(runs));
    }

    [Theory]
    [InlineData("مرحبا, hello", "ar:مرحبا, |en:hello")]
    [InlineData("hello, مرحبا", "en:hello, |ar:مرحبا")]
    [InlineData("مرحبا! hello", "ar:مرحبا! |en:hello")]
    [InlineData("كتب (Hello) ثم ذهب", "ar:كتب (|en:Hello) |ar:ثم ذهب")]
    public void PunctuationBetweenLanguagesJoinsThePrecedingRun(string text, string expected) =>
        Assert.Equal(expected, Seg(text));

    [Theory]
    [InlineData("مرحبا - أهلا، كيف الحال؟", "ar:مرحبا - أهلا، كيف الحال؟")]
    [InlineData("one, two; three!", "en:one, two; three!")]
    public void PunctuationBetweenSameLanguageStaysInTheRun(string text, string expected) =>
        Assert.Equal(expected, Seg(text));

    [Theory]
    [InlineData("  (hello)", "en:  (hello)")]
    [InlineData("« مرحبا »", "ar:« مرحبا »")]
    public void LeadingAndTrailingNeutralsJoinTheOnlyRun(string text, string expected) => Assert.Equal(expected, Seg(text));

    [Theory]
    [InlineData("شغل USB الآن", "ar:شغل |en*:USB|ar: الآن")]
    [InlineData("منظمة WHO للصحة", "ar:منظمة |en*:WHO|ar: للصحة")]
    [InlineData("وصلت USB3 اليوم", "ar:وصلت |en*:USB|en:3 |ar:اليوم")]
    public void ShortCapitalAcronymsInArabicAreSpelled(string text, string expected) => Assert.Equal(expected, Seg(text));

    [Fact]
    public void AcronymsAreJudgedAgainstTheRestOfTheSentence()
    {
        // The non-acronym letters are Arabic, so both acronyms are spelled even though they have more letters.
        Assert.Equal("en*:USB|ar: و |en*:PDF", Seg("USB و PDF"));
        Assert.Equal("ar:شغل |en*:USB", Seg("شغل USB", Lang.English));
    }

    [Theory]
    [InlineData("Open the PDF file", "en:Open the PDF file")]
    [InlineData("I like USB و", "en:I like USB |ar:و")]
    public void AcronymsInEnglishAreNotSpelled(string text, string expected) => Assert.Equal(expected, Seg(text));

    [Theory]
    [InlineData("افتح iPhone الآن", "ar:افتح |en:iPhone |ar:الآن")]
    [InlineData("حرف A فقط", "ar:حرف |en:A |ar:فقط")]
    [InlineData("اكتب NETFLIX الآن", "ar:اكتب |en:NETFLIX |ar:الآن")]
    public void OtherLatinWordsAreNotSpelled(string text, string expected) => Assert.Equal(expected, Seg(text));

    [Fact]
    public void UrlInsideArabicStaysWholeInOneEnglishRun()
    {
        const string url = "https://site.com/v2/item?id=42";
        string text = $"من فضلك افتح الرابط التالي في المتصفح الآن {url} وشكرا جزيلا";
        var runs = ScriptSegmenter.Segment(text, Lang.Arabic);
        RunAssert.Lossless(text, runs);
        Assert.Contains(runs, r => r.Lang == Lang.English && r.Text.Trim() == url);
        Assert.Equal(3, runs.Count);
    }

    [Theory]
    [InlineData("زر الموقع https://example.com/page?id=3 الآن", "https://example.com/page?id=3")]
    [InlineData("افتح www.google.com من فضلك", "www.google.com")]
    [InlineData("راسلني على ahmed.ali99@example.com لو سمحت", "ahmed.ali99@example.com")]
    [InlineData("افتح https://WHO.int/USB الآن", "https://WHO.int/USB")]
    public void UrlsAndEmailsAreNotSplitOrSpelled(string text, string token)
    {
        var runs = ScriptSegmenter.Segment(text, Lang.Arabic);
        RunAssert.Lossless(text, runs);
        var run = Assert.Single(runs, r => r.Text.Contains(token, StringComparison.Ordinal));
        Assert.Equal(Lang.English, run.Lang);
        Assert.False(run.SpellOut);
    }

    [Fact]
    public void UrlLettersDoNotDecideTheLanguageOfNumbers()
    {
        // Without the URL the sentence is Arabic, so the price is read in Arabic.
        Assert.Equal(
            "ar:السعر 5 في |en:https://shop.example.com/products/item ",
            Seg("السعر 5 في https://shop.example.com/products/item "));
        Assert.Equal(Lang.Arabic, ScriptSegmenter.DominantLanguage("راسلني على ahmed@example.com", Lang.English));
    }

    [Fact]
    public void NumberGluedToAWordFollowsThatWord() =>
        Assert.Equal("ar:وصل |en:Win10 |ar:و 2026م", Seg("وصل Win10 و 2026م"));

    [Fact]
    public void TashkeelStaysWithItsArabicLetters() =>
        Assert.Equal("ar:بِسْمِ اللَّهِ |en*:PDF", Seg("بِسْمِ اللَّهِ PDF"));

    [Theory]
    [InlineData("مرحبا 😂 hello 👍🏽")]
    [InlineData("a\r\nب\r\nc")]
    [InlineData("سعر الـ iPhone هو $999.99 (تقريبا)!")]
    [InlineData("‏مرحبا‎ hello‏")]
    [InlineData("é́ بّ x")]
    [InlineData("١٢٣ abc ۴۵۶ أبج 789")]
    [InlineData("ﻣﺮﺣﺒﺎ ﻻ hello")]
    [InlineData("𝐀𝐁𝐂 عربي 🇪🇬 text")]
    [InlineData("tab\there\tوهنا")]
    public void NeverLosesOrReordersCharacters(string text)
    {
        RunAssert.Lossless(text, ScriptSegmenter.Segment(text, Lang.Arabic));
        RunAssert.Lossless(text, ScriptSegmenter.Segment(text, Lang.English));
    }

    [Fact]
    public void LongMixedParagraphIsLossless()
    {
        const string text = "قال أحمد: \"I'll send the PDF at 5pm\". ثم أرسل الملف عبر WhatsApp إلى 3 أشخاص، وكتب: Thanks! 🙏\n"
            + "Visit https://basira.app/help?lang=ar للمساعدة. السعر 9.99$ فقط، أي 50% خصم.";
        RunAssert.Lossless(text, ScriptSegmenter.Segment(text, Lang.Arabic));
        RunAssert.Lossless(text, ScriptSegmenter.Segment(text, Lang.English));
    }

    [Theory]
    [InlineData("مرحبا بالعالم hello", Lang.English, Lang.Arabic)]
    [InlineData("hello world مرحبا", Lang.Arabic, Lang.English)]
    [InlineData("ab جد", Lang.English, Lang.English)]
    [InlineData("12345", Lang.Arabic, Lang.Arabic)]
    public void DominantLanguageCountsLetters(string text, Lang defaultLang, Lang expected) =>
        Assert.Equal(expected, ScriptSegmenter.DominantLanguage(text, defaultLang));
}
