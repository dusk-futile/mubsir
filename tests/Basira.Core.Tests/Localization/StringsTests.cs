using System.Globalization;
using Basira.Core.Common;
using Basira.Core.Localization;

namespace Basira.Core.Tests.Localization;

public class StringsTests
{
    public static TheoryData<StringKey> AllKeys()
    {
        var data = new TheoryData<StringKey>();
        foreach (StringKey k in Enum.GetValues<StringKey>())
        {
            data.Add(k);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryKeyHasTextInBothLanguages(StringKey key)
    {
        Assert.True(Strings.Has(key));
        string ar = Strings.Get(key, Lang.Arabic, "X", "Y");
        string en = Strings.Get(key, Lang.English, "X", "Y");

        Assert.False(string.IsNullOrWhiteSpace(ar));
        Assert.False(string.IsNullOrWhiteSpace(en));
        Assert.NotEqual(ar, en);
        Assert.Contains(ar, c => c is >= 'ء' and <= 'ي'); // real Arabic letters
        Assert.DoesNotContain(en, c => c is >= '؀' and <= 'ۿ');
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void BothLanguagesTakeTheSameArguments(StringKey key)
    {
        string ar = Strings.Get(key, Lang.Arabic, "{0}", "{1}");
        string en = Strings.Get(key, Lang.English, "{0}", "{1}");

        Assert.Equal(ar.Contains("{0}"), en.Contains("{0}"));
        Assert.Equal(ar.Contains("{1}"), en.Contains("{1}"));
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void MissingArgumentsNeverThrowOrLeavePlaceholders(StringKey key)
    {
        string ar = Strings.Get(key, Lang.Arabic);
        string en = Strings.Get(key, Lang.English);

        Assert.DoesNotContain("{", ar);
        Assert.DoesNotContain("{", en);
        Assert.DoesNotContain("  ", ar);
    }

    [Fact]
    public void SpecifiedTexts()
    {
        Assert.Equal("بصيرة جاهزة", Strings.Get(StringKey.Ready, Lang.Arabic));
        Assert.Equal("حسب الذكاء الاصطناعي:", Strings.Get(StringKey.AiLabel, Lang.Arabic));
        Assert.Equal("هذا اقتراح من الصفحة وليس منك.", Strings.Get(StringKey.SuggestedByPage, Lang.Arabic));
        Assert.StartsWith("وضع البنك: الذكاء الاصطناعي متوقف في هذه الصفحة", Strings.Get(StringKey.BankModeOn, Lang.Arabic));
        Assert.StartsWith("لم أسمع شيئًا. تأكد من إعداد خصوصية الميكروفون أو زر كتم الصوت", Strings.Get(StringKey.MicSilent, Lang.Arabic));
        Assert.StartsWith("توقفت مؤقتًا. اضغط إدخال للمتابعة أو Escape للإلغاء", Strings.Get(StringKey.TaskPaused, Lang.Arabic));
        Assert.StartsWith("هذه نافذة مسؤول. سيقرؤها NVDA، ولا أستطيع التحكم فيها. اضغط Alt مع Tab للخروج منها", Strings.Get(StringKey.AdminWindow, Lang.Arabic));
        Assert.StartsWith("اضغط إدخال للتأكيد أو Escape للإلغاء", Strings.Get(StringKey.ConfirmSuffix, Lang.Arabic));
        Assert.StartsWith("حقل كلمة مرور: اكتب كلمة المرور ثم اضغط إدخال", Strings.Get(StringKey.PasswordField, Lang.Arabic));
        Assert.Equal("لم تتم إضافة مفتاح الذكاء الاصطناعي بعد. القراءة المحلية تعمل.", Strings.Get(StringKey.NoApiKey, Lang.Arabic));
        Assert.Contains("اضغط إدخال للسماح بدولار إضافي", Strings.Get(StringKey.BudgetReached, Lang.Arabic));
    }

    [Fact]
    public void FormatArgumentsAreApplied()
    {
        Assert.Contains("6.4 دولار", Strings.Get(StringKey.BudgetWarning80, Lang.Arabic, 6.40m));
        Assert.Contains("6.4 dollars", Strings.Get(StringKey.BudgetWarning80, Lang.English, 6.4m));
        Assert.Contains("7.25", Strings.Get(StringKey.BudgetWarning80, Lang.English, 7.254m));
        Assert.Contains("1.2.3", Strings.Get(StringKey.UpdateReady, Lang.Arabic, "1.2.3"));
        Assert.Contains("version 1.2.3", Strings.Get(StringKey.UpdateReady, Lang.English, "1.2.3"));
        Assert.Equal("أنت في Chrome، ويكيبيديا.", Strings.Get(StringKey.WhereAmI, Lang.Arabic, "Chrome", "ويكيبيديا"));
        Assert.Equal("The battery is at 85 percent.", Strings.Get(StringKey.BatteryLevel, Lang.English, 85));
        Assert.Equal("رمز التحقق هو 1 2 3 4. قرأته على جهازك فقط ولم أرسله.", Strings.Get(StringKey.OtpLocal, Lang.Arabic, "1 2 3 4"));
    }

    [Fact]
    public void FormattingIsCultureInvariant()
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal comma
            Assert.Contains("6.4", Strings.Get(StringKey.BudgetWarning80, Lang.English, 6.4m));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void PartialArgumentsAreTidied()
    {
        Assert.Equal("You are in Chrome,.", Strings.Get(StringKey.WhereAmI, Lang.English, "Chrome"));
        Assert.Equal("Opening.", Strings.Get(StringKey.OpeningApp, Lang.English));
        Assert.Equal("أفتح.", Strings.Get(StringKey.OpeningApp, Lang.Arabic, null!));
    }

    [Fact]
    public void ExtraArgumentsAreIgnored()
    {
        Assert.Equal("Basira is ready.", Strings.Get(StringKey.Ready, Lang.English, "unused"));
    }

    [Fact]
    public void UnknownKeyFallsBackToItsName()
    {
        Assert.Equal("999", Strings.Get((StringKey)999, Lang.Arabic));
    }

    [Fact]
    public void LayerHelpNamesEveryLayerKey()
    {
        string en = Strings.Get(StringKey.LayerHelp, Lang.English);
        foreach (string word in new[] { "Space", "Left arrow", "Right arrow", "Up arrow", "Down arrow", "Period", "Enter", "Escape", "F1" })
        {
            Assert.Contains(word, en);
        }

        string ar = Strings.Get(StringKey.LayerHelp, Lang.Arabic);
        foreach (string word in new[] { "المسافة", "السهم الأيسر", "السهم الأيمن", "السهم الأعلى", "السهم الأسفل", "النقطة", "إدخال", "Escape", "F1" })
        {
            Assert.Contains(word, ar);
        }
    }

    [Fact]
    public void PlaceholderCount()
    {
        Assert.Equal(0, Strings.PlaceholderCount("no args"));
        Assert.Equal(1, Strings.PlaceholderCount("{0:0.##} dollars"));
        Assert.Equal(2, Strings.PlaceholderCount("{1} then {0}"));
    }
}
