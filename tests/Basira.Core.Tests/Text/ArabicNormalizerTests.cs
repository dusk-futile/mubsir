using System.Globalization;
using System.Text;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class ArabicNormalizerTests
{
    /// <summary>Real sentences of different kinds: news, UI messages, greetings, religious text, chat.</summary>
    public static readonly TheoryData<string> NormalSentences =
    [
        "ذهب الولد إلى المدرسة في الصباح",
        "السلام عليكم ورحمة الله وبركاته",
        "هذا الكتاب مفيد جدا للطلاب",
        "يقع المسجد في وسط المدينة القديمة",
        "قال المعلم إن الامتحان سيكون يوم الأحد",
        "كيف حالك اليوم يا صديقي",
        "أنا أحب القراءة والكتابة",
        "تم تحديث البرنامج بنجاح",
        "يرجى إدخال كلمة المرور الصحيحة",
        "بسم الله الرحمن الرحيم",
        "هل تريد حفظ التغييرات قبل الإغلاق",
        "مرحبا بكم في موقعنا الإلكتروني",
        "أهلا وسهلا بكم في بيتنا",
        "انتظر قليلا ثم حاول مجددا",
        "كان الطريق طويلا جدا",
        "ذهبت إلى السوق واشتريت خبزا وحليبا",
        "الطقس حار اليوم في القاهرة",
        "سافر أبي إلى مكة لأداء العمرة",
        "اضغط على زر البدء لفتح القائمة",
        "لا يوجد اتصال بالإنترنت",
        "سيتم إغلاق النافذة خلال دقيقة",
        "وصلت رسالة جديدة من أحمد",
        "قرأ الطالب الدرس مرتين",
        "شكرا جزيلا على مساعدتك",
        "كتب محمد رسالة طويلة لصديقه",
        "افتح ملف PDF على Windows 10 الآن",
    ];

    private static string ReverseChars(string s)
    {
        char[] chars = s.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    // ------------------------------------------------------------ Presentation forms

    [Theory]
    [InlineData("ﻣﺮﺣﺒﺎ", "مرحبا")]
    [InlineData("ﻻ", "لا")]
    [InlineData("ﻼ", "لا")]
    [InlineData("ﻷ", "لأ")]
    [InlineData("ﻹ", "لإ")]
    [InlineData("ﻵ", "لآ")]
    [InlineData("ﷲ", "الله")]
    [InlineData("ﺍﻟﺴﻼﻡ", "السلام")]
    [InlineData("ﺓ", "ة")]
    [InlineData("ﻰ", "ى")]
    public void PresentationFormsBecomeOrdinaryLetters(string text, string expected) =>
        Assert.Equal(expected, ArabicNormalizer.NormalizePresentationForms(text));

    [Theory]
    [InlineData("مرحبا بك hello ١٢٣ 123")]
    [InlineData("ﬁ ﬀ")]
    [InlineData("Ａ ２ ² ½ ™ …")]
    [InlineData("آ")]
    [InlineData("")]
    public void TextOutsideThePresentationBlocksIsUntouched(string text) =>
        Assert.Equal(text, ArabicNormalizer.NormalizePresentationForms(text));

    [Fact]
    public void MappingMatchesNfkcForEveryPresentationForm()
    {
        foreach (var (first, last) in new[] { (0xFB50, 0xFDFF), (0xFE70, 0xFEFF) })
        {
            for (int cp = first; cp <= last; cp++)
            {
                string s = ((char)cp).ToString();
                string nfkc = s.Normalize(NormalizationForm.FormKC);

                // Isolated harakat forms keep only the mark; NFKC adds a space that would split the word.
                if (nfkc.Length > 1 && nfkc[0] == ' ' && CharUnicodeInfo.GetUnicodeCategory(nfkc[1]) == UnicodeCategory.NonSpacingMark)
                {
                    nfkc = nfkc[1..];
                }

                Assert.True(nfkc == ArabicNormalizer.NormalizePresentationForms(s), $"U+{cp:X4}");
            }
        }
    }

    [Fact]
    public void MixedTextNormalizesOnlyTheForms() =>
        Assert.Equal("قال: مرحبا hello ﬁ", ArabicNormalizer.NormalizePresentationForms("ﻗﺎﻞ: ﻣﺮﺣﺒﺎ hello ﬁ"));

    // ------------------------------------------------------------ Reversed lines

    [Theory]
    [MemberData(nameof(NormalSentences))]
    public void NormalArabicIsNotReversed(string sentence) => Assert.False(ArabicNormalizer.LooksReversed(sentence));

    [Theory]
    [MemberData(nameof(NormalSentences))]
    public void CharacterReversedArabicIsDetected(string sentence) =>
        Assert.True(ArabicNormalizer.LooksReversed(ReverseChars(sentence)));

    [Theory]
    [InlineData("أهلا وسهلا ومرحبا")]
    [InlineData("قليلا قليلا يا صديقي")]
    [InlineData("أولا ثانيا ثالثا رابعا")]
    [InlineData("عمل طويلا وكثيرا وجميلا")]
    [InlineData("مثلا كاملا شاملا")]
    [InlineData("الحمد لله رب العالمين")]
    [InlineData("إياك نعبد وإياك نستعين")]
    [InlineData("ملفات المستخدمين والمعلمين")]
    [InlineData("فاطمة وعائشة وخديجة")]
    [InlineData("سماء صافية وهواء نقي")]
    [InlineData("كتاب، قلم، دفتر، مسطرة")]
    [InlineData("1. افتح القائمة 2. اختر الإعدادات")]
    [InlineData("ﺍﻟﺴﻼﻡ ﻋﻠﻴﻜﻢ ﻭﺭﺣﻤﺔ ﺍﻟﻠﻪ")]
    public void HardNormalLinesAreNotReversed(string line) => Assert.False(ArabicNormalizer.LooksReversed(line));

    [Theory]
    [InlineData("")]
    [InlineData("Hello world from Basira")]
    [InlineData("ملف جديد")]
    [InlineData("دجم فلم")]
    [InlineData("12345 67890")]
    public void LinesWithFewerThanThreeArabicWordsAreNotReversed(string line) =>
        Assert.False(ArabicNormalizer.LooksReversed(line));

    [Theory]
    [MemberData(nameof(NormalSentences))]
    public void FixingAReversedPlainArabicLineRestoresIt(string sentence)
    {
        if (sentence.Any(c => c < 128 && char.IsLetterOrDigit(c)))
        {
            return; // covered by the embedded-LTR tests
        }

        Assert.Equal(sentence, ArabicNormalizer.FixReversedLine(ReverseChars(sentence)));
    }

    [Fact]
    public void FixKeepsEmbeddedLatinAndNumbersLeftToRight()
    {
        // As a PDF stores "افتح ملف PDF على Windows 10 الآن" in visual order.
        const string visual = "نآلا Windows 10 ىلع PDF فلم حتفا";
        Assert.True(ArabicNormalizer.LooksReversed(visual));
        Assert.Equal("افتح ملف PDF على Windows 10 الآن", ArabicNormalizer.FixReversedLine(visual));
    }

    [Theory]
    [InlineData("ةحفص 300 نم 12 ةحفصلا", "الصفحة 12 من 300 صفحة")]
    [InlineData("ينورتكلإلا ديربلا ali@example.com ىلع لسرأ", "أرسل على ali@example.com البريد الإلكتروني")]
    [InlineData("2026/10/11 خيراتب", "بتاريخ 2026/10/11")]
    public void FixKeepsNumbersEmailsAndDatesIntact(string visual, string logical) =>
        Assert.Equal(logical, ArabicNormalizer.FixReversedLine(visual));

    [Fact]
    public void FixKeepsDiacriticsOnTheirLetters()
    {
        const string logical = "بِسْمِ اللَّهِ الرَّحْمَنِ";
        var elements = new List<string>();
        var e = StringInfo.GetTextElementEnumerator(logical);
        while (e.MoveNext())
        {
            elements.Add(e.GetTextElement());
        }

        elements.Reverse();
        Assert.Equal(logical, ArabicNormalizer.FixReversedLine(string.Concat(elements)));
    }

    [Fact]
    public void FixMirrorsBrackets() => Assert.Equal("(مرحبا) [بك]", ArabicNormalizer.FixReversedLine("[كب] (ابحرم)"));

    [Fact]
    public void FixKeepsLamAlefLigaturesInOrderWhenNormalizedAfterwards()
    {
        // "السلام" in visual order with presentation forms: final meem first, lam-alef ligature second.
        const string visual = "ﻢﻼﺴﻟﺍ";
        Assert.Equal("السلام", ArabicNormalizer.NormalizePresentationForms(ArabicNormalizer.FixReversedLine(visual)));
    }

    [Theory]
    [InlineData("افتح ملف PDF على Windows 10 الآن")]
    [InlineData("مرحبا (hello world) 123 بك")]
    [InlineData("a")]
    [InlineData("")]
    public void FixIsItsOwnInverse(string line) =>
        Assert.Equal(line, ArabicNormalizer.FixReversedLine(ArabicNormalizer.FixReversedLine(line)));

    [Fact]
    public void NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => ArabicNormalizer.NormalizePresentationForms(null!));
        Assert.Throws<ArgumentNullException>(() => ArabicNormalizer.LooksReversed(null!));
        Assert.Throws<ArgumentNullException>(() => ArabicNormalizer.FixReversedLine(null!));
    }
}
