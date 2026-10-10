using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class EmojiNamesTests
{
    [Theory]
    [InlineData("😂", "وجه يبكي من الضحك", "face with tears of joy")]
    [InlineData("😊", "وجه مبتسم بخجل", "smiling face with smiling eyes")]
    [InlineData("😢", "وجه يبكي", "crying face")]
    [InlineData("❤", "قلب أحمر", "red heart")]
    [InlineData("💔", "قلب مكسور", "broken heart")]
    [InlineData("👍", "إبهام للأعلى", "thumbs up")]
    [InlineData("🙏", "يدان مضمومتان", "folded hands")]
    [InlineData("🤲", "كفان مرفوعتان", "palms up together")]
    [InlineData("✅", "علامة صح", "check mark")]
    [InlineData("❌", "علامة خطأ", "cross mark")]
    [InlineData("⚠", "تحذير", "warning")]
    [InlineData("🔥", "نار", "fire")]
    [InlineData("🎉", "احتفال", "party popper")]
    [InlineData("📞", "سماعة هاتف", "telephone receiver")]
    [InlineData("📷", "كاميرا", "camera")]
    [InlineData("🎂", "كعكة عيد ميلاد", "birthday cake")]
    [InlineData("🌹", "وردة", "rose")]
    [InlineData("🕌", "مسجد", "mosque")]
    [InlineData("☪", "هلال ونجمة", "star and crescent")]
    [InlineData("🫶", "يدان على شكل قلب", "heart hands")]
    public void CommonEmojiHaveNamesInBothLanguages(string emoji, string arabic, string english)
    {
        Assert.Equal($"({arabic})", EmojiNames.Replace(emoji, Lang.Arabic));
        Assert.Equal($"({english})", EmojiNames.Replace(emoji, Lang.English));
    }

    [Theory]
    [InlineData("🇪🇬", "علم مصر", "flag of Egypt")]
    [InlineData("🇸🇦", "علم السعودية", "flag of Saudi Arabia")]
    [InlineData("🇱🇾", "علم ليبيا", "flag of Libya")]
    [InlineData("🇦🇪", "علم الإمارات", "flag of the United Arab Emirates")]
    [InlineData("🇲🇦", "علم المغرب", "flag of Morocco")]
    [InlineData("🇯🇴", "علم الأردن", "flag of Jordan")]
    [InlineData("🇵🇸", "علم فلسطين", "flag of Palestine")]
    [InlineData("🇸🇩", "علم السودان", "flag of Sudan")]
    [InlineData("🇧🇷", "علم", "flag")]
    public void FlagsNameTheirCountry(string flag, string arabic, string english)
    {
        Assert.Equal($"({arabic})", EmojiNames.Replace(flag, Lang.Arabic));
        Assert.Equal($"({english})", EmojiNames.Replace(flag, Lang.English));
    }

    [Theory]
    [InlineData("❤️", "❤")]
    [InlineData("⚠️", "⚠")]
    [InlineData("☪️", "☪")]
    [InlineData("👍🏽", "👍")]
    [InlineData("👋🏿", "👋")]
    [InlineData("🙏🏻", "🙏")]
    public void VariationSelectorsAndSkinTonesAreIgnored(string decorated, string plain) =>
        Assert.Equal(EmojiNames.Replace(plain, Lang.Arabic), EmojiNames.Replace(decorated, Lang.Arabic));

    [Theory]
    [InlineData("😂😂😂", Lang.Arabic, "(وجه يبكي من الضحك، ثلاث مرات)")]
    [InlineData("😂😂😂", Lang.English, "(face with tears of joy, 3 times)")]
    [InlineData("👍👍", Lang.Arabic, "(إبهام للأعلى، مرتين)")]
    [InlineData("👍👍", Lang.English, "(thumbs up, twice)")]
    [InlineData("😂 😂", Lang.Arabic, "(وجه يبكي من الضحك، مرتين)")]
    [InlineData("❤️❤️❤️❤️❤️", Lang.Arabic, "(قلب أحمر، خمس مرات)")]
    [InlineData("👍🏻👍🏿", Lang.English, "(thumbs up, twice)")]
    [InlineData("🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹", Lang.Arabic, "(وردة، 12 مرة)")]
    [InlineData("🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹🌹", Lang.English, "(rose, 12 times)")]
    public void RepeatsAreCollapsedWithACount(string text, Lang lang, string expected) =>
        Assert.Equal(expected, EmojiNames.Replace(text, lang));

    [Fact]
    public void DifferentEmojiAreNotCollapsed() =>
        Assert.Equal("(وجه يبكي من الضحك) (إبهام للأعلى)", EmojiNames.Replace("😂👍", Lang.Arabic));

    [Theory]
    [InlineData("hi 🦩 there", "hi there")]
    [InlineData("ok 🦩", "ok")]
    [InlineData("🦩 ok", "ok")]
    [InlineData("a🦩b", "a b")]
    [InlineData("🦩🦩", "")]
    [InlineData("مرحبا 🫠 بك", "مرحبا بك")]
    public void UnknownEmojiAreRemovedSilently(string text, string expected) =>
        Assert.Equal(expected, EmojiNames.Replace(text, Lang.Arabic));

    [Theory]
    [InlineData("Hello, world!")]
    [InlineData("مرحبا بك في بصيرة")]
    [InlineData("© 2024 — a→b ← ½ ™")]
    [InlineData("")]
    public void TextWithoutEmojiIsUnchanged(string text) => Assert.Equal(text, EmojiNames.Replace(text, Lang.Arabic));

    [Fact]
    public void TextSymbolsWithANameAreSpoken() =>
        Assert.Equal("التقييم (نجمة ممتلئة) (علامة صح)", EmojiNames.Replace("التقييم ★ ✓", Lang.Arabic));

    [Fact]
    public void PlainSymbolsStayUnlessShownAsEmoji()
    {
        Assert.Equal("©2024 a→b", EmojiNames.Replace("©2024 a→b", Lang.English));
        Assert.Equal("1 first", EmojiNames.Replace("1️⃣ first", Lang.English));
    }

    [Theory]
    [InlineData("👨‍👩‍👧 family", "(family) family")]
    [InlineData("🤷‍♂️", "(shrug)")]
    [InlineData("❤️‍🔥", "(heart on fire)")]
    public void ZwjSequencesAreNamed(string text, string expected) => Assert.Equal(expected, EmojiNames.Replace(text, Lang.English));

    [Fact]
    public void WhatsAppLineReadsNaturally()
    {
        Assert.Equal(
            "صباح الخير (وردة، مرتين) كيفك؟ (وجه يبكي من الضحك، ثلاث مرات) (قلب أحمر)",
            EmojiNames.Replace("صباح الخير 🌹🌹 كيفك؟ 😂😂😂 ❤️", Lang.Arabic));
        Assert.Equal(
            "Happy birthday (birthday cake) (party popper, twice)!",
            EmojiNames.Replace("Happy birthday 🎂🎉🎉!", Lang.English));
        Assert.Equal("مبروك (علم مصر)!", EmojiNames.Replace("مبروك 🇪🇬!", Lang.Arabic));
    }

    [Fact]
    public void NameIsSeparatedFromGluedWords()
    {
        Assert.Equal("تمام (إبهام للأعلى) شكرا", EmojiNames.Replace("تمام👍شكرا", Lang.Arabic));
    }

    [Fact]
    public void BareNamesForMathReading() =>
        Assert.Equal("x thumbs up", EmojiNames.Replace("x 👍", Lang.English, parenthesize: false));

    [Fact]
    public void RemoveDropsAllEmojiButKeepsKeycapDigits()
    {
        Assert.Equal("ok", EmojiNames.Remove("ok 👍🏽"));
        Assert.Equal("مبروك يا أحمد", EmojiNames.Remove("مبروك 🎉🎉 يا أحمد"));
        Assert.Equal("1 first", EmojiNames.Remove("1️⃣ first"));
    }

    [Fact]
    public void TryGetNameLooksUpSingleEmoji()
    {
        Assert.True(EmojiNames.TryGetName("👍🏽", Lang.English, out var name));
        Assert.Equal("thumbs up", name);
        Assert.False(EmojiNames.TryGetName("👍👍", Lang.English, out _));
        Assert.False(EmojiNames.TryGetName("a", Lang.English, out _));
        Assert.False(EmojiNames.TryGetName("", Lang.English, out _));
    }

    [Fact]
    public void TableIsLargeAndComplete()
    {
        Assert.True(EmojiNames.Count >= 120, $"only {EmojiNames.Count} emoji");
        foreach (var (emoji, name) in EmojiNames.Names)
        {
            Assert.False(string.IsNullOrWhiteSpace(name.Ar), emoji);
            Assert.False(string.IsNullOrWhiteSpace(name.En), emoji);
            Assert.Contains(name.Ar, c => c is >= '؀' and <= 'ۿ');
            Assert.DoesNotContain('️', emoji);
            Assert.True(EmojiNames.TryGetName(emoji, Lang.Arabic, out var found), $"{emoji} is not recognized as one emoji");
            Assert.Equal(name.Ar, found);
        }
    }

    [Fact]
    public void NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => EmojiNames.Replace(null!, Lang.Arabic));
        Assert.Throws<ArgumentNullException>(() => EmojiNames.Remove(null!));
    }
}
