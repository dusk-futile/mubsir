using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class ArabiziTests
{
    [Theory]
    [InlineData("7abibi")]
    [InlineData("3ala")]
    [InlineData("ma3a")]
    [InlineData("2ahlan")]
    [InlineData("5alas")]
    [InlineData("la2")]
    [InlineData("sa7bi")]
    [InlineData("ma3rafsh")]
    [InlineData("3amalt")]
    [InlineData("6ayeb")]
    [InlineData("8alb")]
    [InlineData("9a7")]
    [InlineData("3'ali")]
    [InlineData("7'alas")]
    [InlineData("ezayak?")]
    [InlineData("(7abibi)")]
    public void DigitWordsLookLikeArabizi(string word) => Assert.True(Arabizi.LooksLikeArabizi(word));

    [Theory]
    [InlineData("enta")]
    [InlineData("enti")]
    [InlineData("ana")]
    [InlineData("ezayak")]
    [InlineData("ezayek")]
    [InlineData("yalla")]
    [InlineData("Yalla")]
    [InlineData("inshallah")]
    [InlineData("habibi")]
    [InlineData("habibti")]
    [InlineData("shukran")]
    [InlineData("mashallah")]
    [InlineData("salam")]
    [InlineData("keda")]
    [InlineData("leh")]
    [InlineData("eh")]
    [InlineData("aywa")]
    [InlineData("tamam")]
    [InlineData("wallah")]
    [InlineData("sabah")]
    [InlineData("masa2")]
    [InlineData("kolo")]
    [InlineData("kaman")]
    [InlineData("mesh")]
    [InlineData("msh")]
    [InlineData("bas")]
    [InlineData("3arabi")]
    public void DictionaryWordsLookLikeArabizi(string word) => Assert.True(Arabizi.LooksLikeArabizi(word));

    [Theory]
    [InlineData("hello")]
    [InlineData("file")]
    [InlineData("computer")]
    [InlineData("shams")]
    [InlineData("2nd")]
    [InlineData("3rd")]
    [InlineData("5pm")]
    [InlineData("3d")]
    [InlineData("8GB")]
    [InlineData("mp3")]
    [InlineData("MP3")]
    [InlineData("H2O")]
    [InlineData("win7")]
    [InlineData("ios9")]
    [InlineData("2024")]
    [InlineData("b4")]
    [InlineData("gr8")]
    [InlineData("2x")]
    [InlineData("USB")]
    [InlineData("7")]
    [InlineData("")]
    [InlineData("...")]
    [InlineData("a.b@c.com")]
    [InlineData("https://7abibi.com")]
    [InlineData("حبيبي")]
    public void OtherTokensDoNotLookLikeArabizi(string word) => Assert.False(Arabizi.LooksLikeArabizi(word));

    [Theory]
    [InlineData("7abibi", "حبيبي")]
    [InlineData("yalla", "يلا")]
    [InlineData("shukran", "شكرا")]
    [InlineData("inshallah", "إن شاء الله")]
    [InlineData("mashallah", "ما شاء الله")]
    [InlineData("5alas", "خلاص")]
    [InlineData("2ahlan", "أهلا")]
    [InlineData("habibti", "حبيبتي")]
    [InlineData("la2", "لأ")]
    [InlineData("masa2", "مساء")]
    [InlineData("3arabi", "عربي")]
    public void CommonWordsMapToReadableArabic(string word, string expected) => Assert.Equal(expected, Arabizi.ToArabic(word));

    [Theory]
    [InlineData("3ala", new[] { "على", "علا" })]
    [InlineData("ma3a", new[] { "مع", "معا" })]
    public void WordsWithTwoAcceptableSpellings(string word, string[] accepted) => Assert.Contains(Arabizi.ToArabic(word), accepted);

    [Theory]
    [InlineData("ma3rafsh", "معرفش")]
    [InlineData("3amalt", "عملت")]
    [InlineData("6ayeb", "طيب")]
    [InlineData("8alb", "قلب")]
    [InlineData("9a7", "صح")]
    [InlineData("3'ali", "غلي")]
    [InlineData("akh", "أخ")]
    [InlineData("shams", "شمس")]
    [InlineData("ghali", "غلي")]
    [InlineData("sa2al", "سأل")]
    [InlineData("ra2y", "رأي")]
    [InlineData("so2al", "سؤل")]
    [InlineData("mo2men", "مؤمن")]
    [InlineData("2ana", "أنا")]
    [InlineData("2ism", "إسم")]
    [InlineData("yalla!", "يلا!")]
    [InlineData("ezayak?", "ازيك؟")]
    [InlineData("5alas,", "خلاص،")]
    public void RulesTransliterateUnknownWords(string word, string expected) => Assert.Equal(expected, Arabizi.ToArabic(word));

    [Theory]
    [InlineData("7'", "خ")]
    [InlineData("3'", "غ")]
    [InlineData("5", "خ")]
    [InlineData("6", "ط")]
    [InlineData("7", "ح")]
    [InlineData("8", "ق")]
    [InlineData("9", "ص")]
    [InlineData("3", "ع")]
    public void DigitLetters(string digit, string expected) => Assert.StartsWith(expected, Arabizi.ToArabic(digit + "ar"));

    [Theory]
    [InlineData("sh", "ش")]
    [InlineData("ch", "ش")]
    [InlineData("kh", "خ")]
    [InlineData("gh", "غ")]
    [InlineData("th", "ث")]
    [InlineData("dh", "ذ")]
    public void Digraphs(string digraph, string expected) => Assert.Equal("ب" + expected, Arabizi.ToArabic("b" + digraph));

    [Theory]
    [InlineData("baab", "باب")]
    [InlineData("beer", "بير")]
    [InlineData("fool", "فول")]
    [InlineData("nour", "نور")]
    [InlineData("kalb", "كلب")]
    [InlineData("kibir", "كيبير")]
    [InlineData("salma", "سلما")]
    [InlineData("hadi", "هدي")]
    [InlineData("omar", "أمر")]
    [InlineData("ibrahim", "ابرهيم")]
    public void VowelRules(string word, string expected) => Assert.Equal(expected, Arabizi.ToArabic(word));

    [Theory]
    [InlineData("7abibi ezayak? ana kwayes el7amdulillah", "حبيبي ازيك؟ أنا كويس الحمد لله")]
    [InlineData("yalla bina 3ala el beit", "يلا بينا على البيت")]
    [InlineData("msh 3aref a3mel eh", "مش عارف أعمل إيه")]
    [InlineData("ana w enta", "أنا وانت")]
    [InlineData("5alas, tamam!", "خلاص، تمام!")]
    [InlineData("tamam ya basha", "تمام يا باشا")]
    [InlineData("تمام 7abibi", "تمام حبيبي")]
    [InlineData("mabrook ya 3arees 🎉", "مبروك يا عريس 🎉")]
    public void ChatLinesAreTransliterated(string text, string expected) =>
        Assert.Equal(expected, Arabizi.TransliterateArabiziWords(text));

    [Theory]
    [InlineData("hello my friend, open the file on the computer")]
    [InlineData("Meeting at 5pm, ok?")]
    [InlineData("Ana went home")]
    [InlineData("bas")]
    [InlineData("Buy 2 x 8GB RAM for Win7, see the 3rd shelf")]
    [InlineData("مرحبا بك")]
    [InlineData("")]
    public void EnglishAndOrdinaryTextIsLeftAlone(string text) => Assert.Equal(text, Arabizi.TransliterateArabiziWords(text));

    [Fact]
    public void OnlyArabiziTokensChangeInAMixedLine()
    {
        Assert.Equal("open the file يلا", Arabizi.TransliterateArabiziWords("open the file yalla"));
        Assert.Equal("يلا https://site.com/3ala", Arabizi.TransliterateArabiziWords("yalla https://site.com/3ala"));
        Assert.Equal("حبيبي a.b3@x.com", Arabizi.TransliterateArabiziWords("7abibi a.b3@x.com"));
    }

    [Fact]
    public void ToArabicConvertsEveryLatinWord()
    {
        Assert.Equal("يلا بينا على البيت", Arabizi.ToArabic("yalla bina 3ala el beit"));
        Assert.Equal("هلو", Arabizi.ToArabic("hello"));
        Assert.Equal("مرحبا 123", Arabizi.ToArabic("مرحبا 123"));
    }

    [Fact]
    public void NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => Arabizi.LooksLikeArabizi(null!));
        Assert.Throws<ArgumentNullException>(() => Arabizi.ToArabic(null!));
        Assert.Throws<ArgumentNullException>(() => Arabizi.TransliterateArabiziWords(null!));
    }
}
