using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class ArabicFoldingTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إسلام", "اسلام")]
    [InlineData("آخر", "اخر")]
    [InlineData("ٱلله", "الله")]
    [InlineData("مكتبة", "مكتبه")]
    [InlineData("مستشفى", "مستشفي")]
    [InlineData("مؤتمر", "موتمر")]
    [InlineData("مسائل", "مسايل")]
    [InlineData("فارسی", "فارسي")]
    [InlineData("کتاب", "كتاب")]
    public void NormalizesLetterVariants(string input, string expected)
    {
        Assert.Equal(expected, ArabicFolding.Fold(input));
    }

    [Theory]
    [InlineData("مُحَمَّدٌ", "محمد")]
    [InlineData("كَتَبَ", "كتب")]
    [InlineData("قُرْآنٌ", "قران")]
    [InlineData("هٰذا", "هذا")] // superscript alef U+0670
    [InlineData("شُكْرًا", "شكرا")]
    public void RemovesTashkeel(string input, string expected)
    {
        Assert.Equal(expected, ArabicFolding.Fold(input));
    }

    [Fact]
    public void RemovesTatweel()
    {
        Assert.Equal("جميل", ArabicFolding.Fold("جـــميـــل"));
    }

    [Theory]
    [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
    [InlineData("۰۱۲۳۴۵۶۷۸۹", "0123456789")]
    [InlineData("الساعة ٣:٤٥", "الساعه 3 45")]
    public void ConvertsDigitsToAscii(string input, string expected)
    {
        Assert.Equal(expected, ArabicFolding.Fold(input));
    }

    [Theory]
    [InlineData("Read The SCREEN", "read the screen")]
    [InlineData("What's the time?", "whats the time")]
    [InlineData("Hello, World!", "hello world")]
    [InlineData("  stop   ", "stop")]
    [InlineData("go\tdown\nnow", "go down now")]
    public void LowercasesLatin_StripsPunctuation_CollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, ArabicFolding.Fold(input));
    }

    [Theory]
    [InlineData("اقرأ الشاشة؟", "اقرا الشاشه")]
    [InlineData("لا، شكرًا.", "لا شكرا")]
    [InlineData("نعم؛ تمام!", "نعم تمام")]
    [InlineData("«مرحبا»", "مرحبا")]
    [InlineData("كم الساعة؟؟؟", "كم الساعه")]
    [InlineData("٪٥٠", "50")]
    public void StripsArabicPunctuation(string input, string expected)
    {
        Assert.Equal(expected, ArabicFolding.Fold(input));
    }

    [Fact]
    public void RemovesInvisibleFormatCharacters()
    {
        // RLM, LRM, ZWJ, ZWNJ, BOM and the Arabic letter mark.
        Assert.Equal("اقرا الشاشه", ArabicFolding.Fold("‏اقرأ‌ ‍الشاشة‎﻿؜"));
    }

    [Fact]
    public void FoldsPresentationForms()
    {
        // "لا" as the lam-alef ligature and isolated letter forms, as copied from some PDFs.
        Assert.Equal("لا", ArabicFolding.Fold("ﻻ"));
        Assert.Equal("سلام", ArabicFolding.Fold("ﺳﻟﺎﻡ"));
    }

    [Fact]
    public void StripsEmojiAndSymbols()
    {
        Assert.Equal("اسكت", ArabicFolding.Fold("اسكت 🙏"));
        Assert.Equal("a b", ArabicFolding.Fold("a + b = "));
    }

    [Fact]
    public void MixedArabicAndEnglish()
    {
        Assert.Equal("افتح google chrome", ArabicFolding.Fold("إفتَح Google-Chrome"));
    }

    [Fact]
    public void EmptyAndPunctuationOnly()
    {
        Assert.Equal(string.Empty, ArabicFolding.Fold(string.Empty));
        Assert.Equal(string.Empty, ArabicFolding.Fold("   "));
        Assert.Equal(string.Empty, ArabicFolding.Fold("؟!..،"));
        Assert.Equal(string.Empty, ArabicFolding.Fold("ًٌٍَُِّْ"));
        Assert.Throws<ArgumentNullException>(() => ArabicFolding.Fold(null!));
    }

    [Fact]
    public void IsIdempotent()
    {
        string once = ArabicFolding.Fold("إقرأْ لي الشّاشةَ، من فضلك! ١٢٣ ABC");
        Assert.Equal(once, ArabicFolding.Fold(once));
    }

    [Fact]
    public void SpellingVariantsFoldTogether()
    {
        string[] variants = ["اقرأ الشاشة", "إقرأ الشاشه", "اقرا الشّاشة", "اقرَأ  الشاشة.", "اقرأ الشاشـة"];
        Assert.Single(variants.Select(ArabicFolding.Fold).Distinct());
    }
}
