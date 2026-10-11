using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class SentenceSplitterTests
{
    private static string Split(string text) => string.Join(" ‖ ", SentenceSplitter.Split(text));

    [Theory]
    [InlineData("", "")]
    [InlineData("   \n\t  ", "")]
    [InlineData("Hello", "Hello")]
    [InlineData("  Hello there.  ", "Hello there.")]
    public void TrimsAndDropsEmpties(string text, string expected) => Assert.Equal(expected, Split(text));

    [Fact]
    public void NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => SentenceSplitter.Split(null!));
        Assert.Throws<ArgumentNullException>(() => SentenceSplitter.Paragraphs(null!));
    }

    [Theory]
    [InlineData("It works. Really! Are you sure? Yes…", "It works. ‖ Really! ‖ Are you sure? ‖ Yes…")]
    [InlineData("Wow!! Really?! OK.", "Wow!! ‖ Really?! ‖ OK.")]
    [InlineData("Is it done?? Yes.", "Is it done?? ‖ Yes.")]
    public void EnglishTerminators(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("كيف حالك؟ أنا بخير. والحمد لله!", "كيف حالك؟ ‖ أنا بخير. ‖ والحمد لله!")]
    [InlineData("كيف حالك؟أنا بخير", "كيف حالك؟ ‖ أنا بخير")]
    [InlineData("انتهى الدرس.ابدأ التمرين", "انتهى الدرس. ‖ ابدأ التمرين")]
    [InlineData("انتظر… ثم تابع", "انتظر… ‖ ثم تابع")]
    public void ArabicTerminators(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("مرحبا، كيف حالك؛ أنا بخير. وأنت؟", "مرحبا، كيف حالك؛ أنا بخير. ‖ وأنت؟")]
    [InlineData("أولا، افتح الملف؛ ثانيا، احفظه", "أولا، افتح الملف؛ ثانيا، احفظه")]
    public void ArabicCommaAndSemicolonDoNotEndSentences(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("line one\nline two\n\nline three", "line one ‖ line two ‖ line three")]
    [InlineData("سطر أول\r\nسطر ثان", "سطر أول ‖ سطر ثان")]
    [InlineData("no period here\nnext", "no period here ‖ next")]
    public void NewlinesEndSentences(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("Pi is 3.14 roughly. Next.", "Pi is 3.14 roughly. ‖ Next.")]
    [InlineData("Pi is 3.14. ok", "Pi is 3.14. ‖ ok")]
    [InlineData("السعر ٣٫١٤ جنيه. شكرا", "السعر ٣٫١٤ جنيه. ‖ شكرا")]
    [InlineData("الوزن ٣.٥ كيلو فقط", "الوزن ٣.٥ كيلو فقط")]
    [InlineData("Version 2.0.1 is out. Update now.", "Version 2.0.1 is out. ‖ Update now.")]
    public void DecimalsDoNotSplit(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("Visit www.example.com. Then rest.", "Visit www.example.com. ‖ Then rest.")]
    [InlineData("Open https://site.com/page.html?id=3&x=y. Done!", "Open https://site.com/page.html?id=3&x=y. ‖ Done!")]
    [InlineData("Email me at a.b@mail.example.com. Thanks", "Email me at a.b@mail.example.com. ‖ Thanks")]
    [InlineData("زر الموقع example.com/help?x=1 الآن", "زر الموقع example.com/help?x=1 الآن")]
    public void UrlsAndEmailsDoNotSplit(string text, string expected) => Assert.Equal(expected, Split(text));

    [Theory]
    [InlineData("Dr. Smith went home. He slept.", "Dr. Smith went home. ‖ He slept.")]
    [InlineData("Mr. and Mrs. Jones came. Nice.", "Mr. and Mrs. Jones came. ‖ Nice.")]
    [InlineData("Use tools, e.g. a hammer. Then rest.", "Use tools, e.g. a hammer. ‖ Then rest.")]
    [InlineData("Some fruit, i.e. apples. Fine.", "Some fruit, i.e. apples. ‖ Fine.")]
    [InlineData("Apples, pears, etc. are fruit.", "Apples, pears, etc. are fruit.")]
    [InlineData("Apples, pears etc. Then milk.", "Apples, pears etc. ‖ Then milk.")]
    [InlineData("Real vs. fake. Hmm.", "Real vs. fake. ‖ Hmm.")]
    [InlineData("See No. 5 now.", "See No. 5 now.")]
    [InlineData("The U.S. is big. It is.", "The U.S. is big. ‖ It is.")]
    [InlineData("قال د. أحمد إن الدواء مفيد. شكرا", "قال د. أحمد إن الدواء مفيد. ‖ شكرا")]
    public void AbbreviationsDoNotSplit(string text, string expected) => Assert.Equal(expected, Split(text));

    [Fact]
    public void InitialsDoNotSplit() =>
        Assert.Equal("J. K. Rowling wrote it. ‖ Then rest.", Split("J. K. Rowling wrote it. Then rest."));

    [Theory]
    [InlineData("Wait... what? Really.", "Wait... what? ‖ Really.")]
    [InlineData("Wait... Then go.", "Wait... ‖ Then go.")]
    [InlineData("So… we wait.", "So… we wait.")]
    [InlineData("The end...", "The end...")]
    public void EllipsisRuns(string text, string expected) => Assert.Equal(expected, Split(text));

    [Fact]
    public void ClosingQuotesStayWithTheirSentence() =>
        Assert.Equal("He said \"Hi.\" ‖ Then left.", Split("He said \"Hi.\" Then left."));

    [Theory]
    [InlineData("1. Buy milk\n2. Sell bread", "1. Buy milk ‖ 2. Sell bread")]
    [InlineData("1. Buy milk. 2. Sell bread.", "1. Buy milk. ‖ 2. Sell bread.")]
    public void ListNumbersDoNotSplit(string text, string expected) => Assert.Equal(expected, Split(text));

    [Fact]
    public void VeryLongSentencesAreBrokenForRewind()
    {
        string text = string.Join("، ", Enumerable.Repeat("هذه جملة طويلة جدا بدون نقطة", 40));
        var sentences = SentenceSplitter.Split(text);
        Assert.True(sentences.Count > 1);
        Assert.All(sentences, s => Assert.InRange(s.Length, 1, SentenceSplitter.MaxSentenceLength));
        Assert.Equal(
            text.Replace(" ", "", StringComparison.Ordinal),
            string.Concat(sentences).Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public void ParagraphsSplitOnSingleAndBlankLines()
    {
        Assert.Equal(["a", "b", "c", "d"], SentenceSplitter.Paragraphs("a\nb\n\n\nc\r\nd  \n  "));
        Assert.Equal(["الفقرة الأولى. فيها جملتان.", "الفقرة الثانية"], SentenceSplitter.Paragraphs("  الفقرة الأولى. فيها جملتان.\n\nالفقرة الثانية\n"));
        Assert.Empty(SentenceSplitter.Paragraphs(""));
        Assert.Empty(SentenceSplitter.Paragraphs("\n\n  \n"));
    }
}
