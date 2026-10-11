using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class SymbolVerbalizerTests
{
    private static string Normal(string text, Lang lang) => SymbolVerbalizer.Verbalize(text, lang, VerbalizeMode.Normal);

    private static string Math(string text, Lang lang) => SymbolVerbalizer.Verbalize(text, lang, VerbalizeMode.Math);

    private static string Spell(string text, Lang lang) => SymbolVerbalizer.Verbalize(text, lang, VerbalizeMode.Spell);

    [Fact]
    public void NullThrows() => Assert.Throws<ArgumentNullException>(() => SymbolVerbalizer.Verbalize(null!, Lang.Arabic, VerbalizeMode.Normal));

    // ------------------------------------------------------------ Normal

    [Theory]
    [InlineData("خصم 50% على كل شيء", Lang.Arabic, "خصم 50 بالمئة على كل شيء")]
    [InlineData("خصم ٥٠٪ اليوم", Lang.Arabic, "خصم ٥٠ بالمئة اليوم")]
    [InlineData("50% off", Lang.English, "50 percent off")]
    [InlineData("up 12.5 % today", Lang.English, "up 12.5 percent today")]
    [InlineData("the % sign", Lang.English, "the % sign")]
    public void PercentAfterANumber(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("Tom & Jerry", Lang.English, "Tom and Jerry")]
    [InlineData("أحمد & محمد", Lang.Arabic, "أحمد و محمد")]
    [InlineData("R&D team", Lang.English, "R and D team")]
    [InlineData("a&b=1&c=2", Lang.English, "a&b=1&c=2")]
    [InlineData("x && y", Lang.English, "x && y")]
    public void Ampersand(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("email me at a.b@gmail.com", Lang.English, "email me at a.b at gmail.com")]
    [InlineData("راسلني على ahmed@example.com الآن", Lang.Arabic, "راسلني على ahmed آت example.com الآن")]
    [InlineData("follow @basira_app", Lang.English, "follow at basira_app")]
    [InlineData("تابع @basira_app", Lang.Arabic, "تابع آت basira_app")]
    [InlineData("meet @ 5", Lang.English, "meet @ 5")]
    public void AtSignInEmailsAndHandles(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("#رمضان_كريم", Lang.Arabic, "هاشتاج رمضان_كريم")]
    [InlineData("love #coding today", Lang.English, "love hashtag coding today")]
    [InlineData("#1 song", Lang.English, "number 1 song")]
    [InlineData("C# code", Lang.English, "C# code")]
    public void HashBeforeAWord(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("x² + y³", Lang.English, "x squared + y cubed")]
    [InlineData("مساحة 5 م²", Lang.Arabic, "مساحة 5 م تربيع")]
    [InlineData("حجم 3 سم³", Lang.Arabic, "حجم 3 سم تكعيب")]
    public void Superscripts(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("½ cup", Lang.English, "one half cup")]
    [InlineData("2½ cups", Lang.English, "2 and a half cups")]
    [InlineData("¼ hour", Lang.English, "one quarter hour")]
    [InlineData("¾ كوب", Lang.Arabic, "ثلاثة أرباع كوب")]
    [InlineData("½ ساعة", Lang.Arabic, "نصف ساعة")]
    [InlineData("¼ ساعة", Lang.Arabic, "رُبع ساعة")]
    [InlineData("3½ كيلو", Lang.Arabic, "3 ونصف كيلو")]
    public void CommonFractions(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("الحرارة 30°C اليوم", Lang.Arabic, "الحرارة 30 درجة مئوية اليوم")]
    [InlineData("الحرارة 86°F", Lang.Arabic, "الحرارة 86 درجة فهرنهايت")]
    [InlineData("It is 25 °C", Lang.English, "It is 25 degrees Celsius")]
    [InlineData("It is 1°C", Lang.English, "It is 1 degree Celsius")]
    [InlineData("It is 70°F.", Lang.English, "It is 70 degrees Fahrenheit.")]
    [InlineData("angle 90°", Lang.English, "angle 90 degrees")]
    [InlineData("زاوية 45°", Lang.Arabic, "زاوية 45 درجة")]
    [InlineData("water boils at 100℃", Lang.English, "water boils at 100 degrees Celsius")]
    public void Degrees(string text, Lang lang, string expected) => Assert.Equal(expected, Normal(text, lang));

    [Theory]
    [InlineData("Hello, world! (yes) - ok? \"quoted\" [x] {y} a/b; c: d...", Lang.English)]
    [InlineData("مرحبا، كيف حالك؟ (بخير) - شكرا!", Lang.Arabic)]
    [InlineData("1 + 1 = 2 and 3 * 4 < 20", Lang.English)]
    [InlineData("visit https://x.com/a?b=1&c=2%20 now", Lang.English)]
    [InlineData("www.site.com/#top", Lang.English)]
    [InlineData("", Lang.Arabic)]
    public void NormalPunctuationAndUrlsAreLeftAlone(string text, Lang lang) => Assert.Equal(text, Normal(text, lang));

    // ------------------------------------------------------------ Math

    [Theory]
    [InlineData("x^2 + 3x - 5 = 0", "x squared plus 3 x minus 5 equals 0")]
    [InlineData("a × b ÷ c", "a times b divided by c")]
    [InlineData("3 * 4 / 2", "3 times 4 divided by 2")]
    [InlineData("10 − 4", "10 minus 4")]
    [InlineData("-5 + 3", "negative 5 plus 3")]
    [InlineData("5 - -3", "5 minus negative 3")]
    [InlineData("x ≠ y", "x not equal to y")]
    [InlineData("a < b ≤ c > d ≥ e", "a less than b less than or equal to c greater than d greater than or equal to e")]
    [InlineData("2^10", "2 to the power of 10")]
    [InlineData("x^3", "x cubed")]
    [InlineData("x^n", "x to the power of n")]
    [InlineData("√16 = 4", "square root of 16 equals 4")]
    [InlineData("π ≈ 3.14", "pi approximately equal to 3 point 1 4")]
    [InlineData("∞", "infinity")]
    [InlineData("∑ x", "sum x")]
    [InlineData("∫ f(x) dx", "integral f open bracket x close bracket d x")]
    [InlineData("f'(x) = 2x", "f prime open bracket x close bracket equals 2 x")]
    [InlineData("f″(x)", "f double prime open bracket x close bracket")]
    [InlineData("±5", "plus or minus 5")]
    [InlineData("[a, b] {c}", "open square bracket a comma b close square bracket open curly bracket c close curly bracket")]
    [InlineData("x₁ + x₂", "x sub 1 plus x sub 2")]
    [InlineData("٣ + ٤ = ٧", "3 plus 4 equals 7")]
    [InlineData("1,000 + 2", "1000 plus 2")]
    [InlineData("e^{2x}", "e to the power of 2 x")]
    [InlineData("x² + y²", "x squared plus y squared")]
    public void MathInEnglish(string text, string expected) => Assert.Equal(expected, Math(text, Lang.English));

    [Theory]
    [InlineData("x^2 + 3x - 5 = 0", "x تربيع زائد 3 x ناقص 5 يساوي 0")]
    [InlineData("a × b ÷ c", "a ضرب b على c")]
    [InlineData("3 * 4 / 2", "3 ضرب 4 على 2")]
    [InlineData("-5 + 3", "سالب 5 زائد 3")]
    [InlineData("x ≠ y", "x لا يساوي y")]
    [InlineData("a < b ≤ c > d ≥ e", "a أصغر من b أصغر من أو يساوي c أكبر من d أكبر من أو يساوي e")]
    [InlineData("2^10", "2 أس 10")]
    [InlineData("x^3", "x تكعيب")]
    [InlineData("√16 = 4", "الجذر التربيعي لـ 16 يساوي 4")]
    [InlineData("π ≈ 3.14", "باي يساوي تقريبا 3 فاصلة 14")]
    [InlineData("∞", "ما لا نهاية")]
    [InlineData("∑ x", "مجموع x")]
    [InlineData("∫ f(x) dx", "تكامل f فتح قوس x إغلاق قوس d x")]
    [InlineData("f'(x)", "f شرطة فتح قوس x إغلاق قوس")]
    [InlineData("f″(x)", "f شرطتان فتح قوس x إغلاق قوس")]
    [InlineData("±5", "زائد أو ناقص 5")]
    [InlineData("[a] {b}", "فتح قوس مربع a إغلاق قوس مربع فتح قوس معقوف b إغلاق قوس معقوف")]
    [InlineData("x₁ + x₂", "x واحد سفلي زائد x اثنان سفلي")]
    [InlineData("س + ص = ع", "س زائد ص يساوي ع")]
    [InlineData("٢س − ٣ = ٧", "2 س ناقص 3 يساوي 7")]
    [InlineData("٣٫٥ × ٢", "3 فاصلة 5 ضرب 2")]
    public void MathInArabic(string text, string expected) => Assert.Equal(expected, Math(text, Lang.Arabic));

    [Theory]
    [InlineData("x^2+3x-5=0")]
    [InlineData("  (a + b)  ×  c  ")]
    [InlineData("f'(x) = 3x² − 2x + 1")]
    public void MathOutputIsASingleSpacedPhrase(string text)
    {
        foreach (Lang lang in new[] { Lang.Arabic, Lang.English })
        {
            string spoken = Math(text, lang);
            Assert.Equal(spoken.Trim(), spoken);
            Assert.DoesNotContain("  ", spoken, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MathWithoutSpacesReadsTheSameAsWithSpaces() =>
        Assert.Equal(Math("x^2 + 3x - 5 = 0", Lang.English), Math("x^2+3x-5=0", Lang.English));

    [Fact]
    public void MathFunctionsAreNamed()
    {
        Assert.Equal("sine x plus cosine x", Math("sin x + cos x", Lang.English));
        Assert.Equal("جا x زائد جتا x", Math("sin x + cos x", Lang.Arabic));
    }

    // ------------------------------------------------------------ Spell

    [Theory]
    [InlineData("PDF a1", Lang.English, "capital P, capital D, capital F, space, a, one")]
    [InlineData("PDF a1", Lang.Arabic, "P كبير, D كبير, F كبير, مسافة, a, واحد")]
    [InlineData("x@y.com", Lang.English, "x, at sign, y, dot, c, o, m")]
    [InlineData("x@y.com", Lang.Arabic, "x, علامة آت, y, نقطة, c, o, m")]
    public void SpellLatinLettersDigitsAndSymbols(string text, Lang lang, string expected) => Assert.Equal(expected, Spell(text, lang));

    [Theory]
    [InlineData("أحمد", Lang.Arabic, "ألف همزة فوق, حاء, ميم, دال")]
    [InlineData("مدرسة", Lang.Arabic, "ميم, دال, راء, سين, تاء مربوطة")]
    [InlineData("آئؤإىء", Lang.Arabic, "ألف مد, ياء همزة, واو همزة, ألف همزة تحت, ألف مقصورة, همزة")]
    [InlineData("أحمد", Lang.English, "alef with hamza above, hah, meem, dal")]
    [InlineData("مدرسة", Lang.English, "meem, dal, reh, seen, teh marbuta")]
    public void SpellArabicLetterNames(string text, Lang lang, string expected) => Assert.Equal(expected, Spell(text, lang));

    [Theory]
    [InlineData("بِسْمِ", Lang.Arabic, "باء, كسرة, سين, سكون, ميم, كسرة")]
    [InlineData("\u0645\u064E\u062F\u0651\u064E", Lang.Arabic, "ميم, فتحة, دال, شدة, فتحة")]
    [InlineData("كتابٌ", Lang.Arabic, "كاف, تاء, ألف, باء, تنوين ضم")]
    [InlineData("كُتُبًا", Lang.Arabic, "كاف, ضمة, تاء, ضمة, باء, تنوين فتح, ألف")]
    [InlineData("بِسْمِ", Lang.English, "beh, kasra, seen, sukun, meem, kasra")]
    public void SpellTashkeel(string text, Lang lang, string expected) => Assert.Equal(expected, Spell(text, lang));

    [Theory]
    [InlineData("١٢٣", Lang.Arabic, "واحد, اثنان, ثلاثة")]
    [InlineData("٤٥", Lang.English, "four, five")]
    [InlineData("a b", Lang.Arabic, "a, مسافة, b")]
    [InlineData("a b", Lang.English, "a, space, b")]
    [InlineData("؟!", Lang.Arabic, "علامة استفهام, علامة تعجب")]
    [InlineData("%#", Lang.English, "percent sign, hash")]
    public void SpellDigitsSpacesAndPunctuation(string text, Lang lang, string expected) => Assert.Equal(expected, Spell(text, lang));

    [Fact]
    public void SpellNamesEmojiAndSkipsInvisibleMarks()
    {
        Assert.Equal("o, k, space, thumbs up", Spell("ok 👍🏽", Lang.English));
        Assert.Equal("o, k, مسافة, إبهام للأعلى", Spell("ok 👍", Lang.Arabic));
        Assert.Equal("a, b", Spell("a‍b", Lang.English));
    }

    [Fact]
    public void EveryArabicLetterHasANameInBothLanguages()
    {
        const string alphabet = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي" + "ةىءأإآؤئ";
        foreach (char c in alphabet)
        {
            Assert.True(SymbolTables.ArabicLetterNames.TryGetValue(c, out var name), $"missing {c}");
            Assert.False(string.IsNullOrWhiteSpace(name.Ar));
            Assert.False(string.IsNullOrWhiteSpace(name.En));
        }

        foreach (char mark in "ًٌٍَُِّْ")
        {
            Assert.True(SymbolTables.TashkeelNames.ContainsKey(mark), $"missing U+{(int)mark:X4}");
        }
    }

    [Fact]
    public void SpellIsOneItemPerCharacter()
    {
        string text = "ab, 12";
        Assert.Equal(text.Length, Spell(text, Lang.English).Split(", ").Length);
    }
}
