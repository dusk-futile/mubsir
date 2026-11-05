using Mubsir.Core.Common;
using Mubsir.Core.Text;
using Xunit;

namespace Mubsir.Core.Tests.Text;

public sealed class TashkeelSpeechTests
{
    [Fact]
    public void StripTashkeel_removes_arabic_diacritics()
    {
        string vowelled = "السَّلَامُ عَلَيْكُمْ وَرَحْمَةُ اللهِ وَبَرَكَاتُهُ";
        string stripped = ArabicNormalizer.StripTashkeel(vowelled);

        Assert.Equal("السلام عليكم ورحمة الله وبركاته", stripped);
    }

    [Fact]
    public void StripTashkeel_returns_same_string_when_no_diacritics()
    {
        string plain = "كتاب القراءة";
        string stripped = ArabicNormalizer.StripTashkeel(plain);

        Assert.Same(plain, stripped);
    }

    [Fact]
    public void SpeechTextPipeline_strips_tashkeel_by_default_to_leave_for_voice()
    {
        string vowelled = "كَتَبَ الطَّالِبُ";
        var runs = SpeechTextPipeline.Prepare(vowelled, Lang.Arabic, new SpeechOptions(ReadFullTashkeel: false));
        string result = string.Concat(runs.Select(r => r.Text));

        Assert.Equal("كتب الطالب", result);
    }

    [Fact]
    public void SpeechTextPipeline_preserves_tashkeel_when_ReadFullTashkeel_is_true()
    {
        string vowelled = "كَتَبَ الطَّالِبُ";
        var runs = SpeechTextPipeline.Prepare(vowelled, Lang.Arabic, new SpeechOptions(ReadFullTashkeel: true));
        string result = string.Concat(runs.Select(r => r.Text));

        Assert.Equal("كَتَبَ الطَّالِبُ", result);
    }

    [Fact]
    public void SpeechTextPipeline_preserves_tashkeel_in_spell_mode()
    {
        string vowelled = "كَتَبَ";
        var runs = SpeechTextPipeline.Prepare(vowelled, Lang.Arabic, new SpeechOptions(Mode: VerbalizeMode.Spell, ReadFullTashkeel: false));
        string result = string.Concat(runs.Select(r => r.Text));

        // In spell mode, each letter and diacritic should be verbalized
        Assert.Contains("فتحة", result, StringComparison.Ordinal);
    }
}
