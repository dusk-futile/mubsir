using Mubsir.Core.Common;
using Mubsir.Core.Text;

namespace Mubsir.Core.Tests.Text;

/// <summary>Every symbol in ordinary text is said by name, in the language around it, so nothing is skipped.</summary>
public sealed class SymbolReadingTests
{
    private static string Read(string text, Lang lang = Lang.English) =>
        string.Concat(SpeechTextPipeline.Prepare(text, lang).Select(r => r.Text));

    [Theory]
    [InlineData("x² + y² = z²", "x squared + y squared = z squared")]
    [InlineData("√16 = 4", "square root of 16 = 4")]
    [InlineData("α + β = γ", "alpha + beta = gamma")]
    [InlineData("a ≤ b ≠ c", "a less than or equal to b not equal to c")]
    [InlineData("±5", "plus or minus 5")]
    [InlineData("x → ∞", "x right arrow infinity")]
    [InlineData("A ∪ B ∩ C", "A union B intersection C")]
    [InlineData("50% off", "50 percent off")]
    [InlineData("-5°C or 23°F", "-5 degrees Celsius or 23 degrees Fahrenheit")]
    [InlineData("1°C", "1 degree Celsius")]
    [InlineData("§ 3.2 ¶ 4", "section 3.2 paragraph mark 4")]
    [InlineData("© 2026 Mubsir™", "copyright 2026 Mubsir trademark")]
    [InlineData("✗ failed", "cross mark failed")]
    public void Symbols_are_named_in_english(string text, string expected)
    {
        Assert.Equal(expected, Read(text));
    }

    [Theory]
    [InlineData("الحرارة 30°C اليوم", "درجة مئوية")]
    [InlineData("الخصم 50% النهارده", "بالمئة")]
    [InlineData("قيمة π تقريبا", "باي")]
    [InlineData("اقرأ § 3", "فقرة")]
    [InlineData("√9 يساوي 3", "الجذر التربيعي")]
    public void Symbols_are_named_in_arabic_inside_arabic(string text, string expectedWord)
    {
        Assert.Contains(expectedWord, Read(text, Lang.Arabic), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("email me at omar.k@gmail.com", "omar.k at gmail.com")]
    [InlineData("follow @mubsir", "at mubsir")]
    [InlineData("#accessibility", "hashtag accessibility")]
    [InlineData("issue #42", "number 42")]
    public void Web_symbols_are_spoken(string text, string expected)
    {
        Assert.Contains(expected, Read(text), StringComparison.Ordinal);
    }

    [Fact]
    public void Emoji_are_described_not_skipped()
    {
        string read = Read("ok 😊", Lang.Arabic);

        Assert.DoesNotContain("😊", read, StringComparison.Ordinal);
        Assert.True(read.Length > "ok ".Length + 2);
    }

    [Fact]
    public void Arabic_punctuation_is_left_to_the_voice()
    {
        Assert.Equal("مرحبا،كيف الحال؟", Read("مرحبا،كيف الحال؟", Lang.Arabic));
    }

    [Fact]
    public void Plain_text_comes_through_unchanged()
    {
        const string text = "The quick brown fox jumps over the lazy dog.";
        Assert.Equal(text, Read(text));
    }

    [Fact]
    public void Nothing_unnamed_is_left_from_a_maths_line()
    {
        string read = Read("∀x ∈ S: x² ≥ 0 ∴ √(x²) = |x|");

        foreach (char c in "∀∈≥∴√²")
        {
            Assert.DoesNotContain(c.ToString(), read, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ipa_between_slashes_is_read_sound_by_sound()
    {
        string read = Read("The word /ʃɪp/ has three sounds.");

        Assert.Contains("sh sound", read, StringComparison.Ordinal);
        Assert.DoesNotContain("ʃ", read, StringComparison.Ordinal);
        Assert.EndsWith("has three sounds.", read, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_ipa_transcription_leaves_no_raw_symbol()
    {
        string read = Read("/fəˈnɒlədʒi/");

        foreach (char c in "əˈɒʒ")
        {
            Assert.DoesNotContain(c.ToString(), read, StringComparison.Ordinal);
        }

        Assert.Contains("main stress", read, StringComparison.Ordinal);
    }

    [Fact]
    public void Tashkeel_is_kept_for_the_voice_when_full_tashkeel_is_on()
    {
        const string vowelled = "كَتَبَ الطَّالِبُ الدَّرْسَ";
        Assert.Equal(vowelled, Read(vowelled, Lang.Arabic));
    }

    [Fact]
    public void Presentation_forms_from_old_pdfs_become_normal_letters()
    {
        // "سلام" stored as presentation forms, the way some old PDFs keep Arabic.
        const string old = "ﺳﻼﻡ";
        string read = Read(old, Lang.Arabic);

        Assert.DoesNotContain(read, c => c is >= 'ﭐ' and <= '﻿');
    }

    [Fact]
    public void Spell_mode_reads_every_character_including_spaces_and_marks()
    {
        var runs = SpeechTextPipeline.Prepare("a b!", Lang.English, new SpeechOptions(Mode: VerbalizeMode.Spell));
        string read = string.Concat(runs.Select(r => r.Text));

        Assert.Contains("space", read, StringComparison.Ordinal);
        Assert.Contains("exclamation mark", read, StringComparison.Ordinal);
    }
}
