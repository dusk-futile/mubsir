using Basira.Core.Common;
using Basira.Core.Policy;

namespace Basira.Core.Tests.Policy;

public class ConfirmationBuilderTests
{
    private const string ArHint = " اضغط إدخال للتأكيد أو Escape للإلغاء.";
    private const string EnHint = " Press Enter to confirm or Escape to cancel.";

    [Fact]
    public void ArabicSendWithRecipientAndBody()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Send, Recipient = "أحمد", BodyPreview = "أنا في الطريق، سأصل بعد عشر دقائق" };
        Assert.Equal("سأرسل رسالة إلى أحمد: «أنا في الطريق، سأصل بعد عشر دقائق»." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void EnglishSendWithRecipientAndBody()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Send, Recipient = "Ahmed", BodyPreview = "On my way" };
        Assert.Equal("I will send a message to Ahmed: \"On my way\"." + EnHint, ConfirmationBuilder.Build(facts, Lang.English));
    }

    [Fact]
    public void PreviewIsCutToFifteenWords()
    {
        var body = string.Join(' ', Enumerable.Range(1, 30).Select(i => "w" + i));
        var text = ConfirmationBuilder.Build(new ConfirmationFacts { Kind = ConfirmKind.Send, Recipient = "Sara", BodyPreview = body }, Lang.English);
        Assert.Contains("w15…", text);
        Assert.DoesNotContain("w16", text);
    }

    [Fact]
    public void BodyIsOneLine()
    {
        var text = ConfirmationBuilder.Build(new ConfirmationFacts { Kind = ConfirmKind.Send, Recipient = "Sara", BodyPreview = "line one\n\nline two" }, Lang.English);
        Assert.Contains("\"line one line two\"", text);
        Assert.DoesNotContain('\n', text);
    }

    [Fact]
    public void ArabicPayWithAmountAndSite()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Pay, Amount = "٢٥٠ جنيه", Site = "vodafone.com.eg", ButtonName = "ادفع الآن" };
        Assert.Equal("سأدفع ٢٥٠ جنيه في موقع vodafone.com.eg." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void EnglishPayToPayee()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Pay, Amount = "250 EGP", Recipient = "Vodafone", Site = "vodafone.com.eg" };
        Assert.Equal("I will pay 250 EGP to Vodafone on vodafone.com.eg." + EnHint, ConfirmationBuilder.Build(facts, Lang.English));
    }

    [Fact]
    public void TransferArabic()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Transfer, Amount = "1000 جنيه", Recipient = "محمد" };
        Assert.Equal("سأحوّل 1000 جنيه إلى محمد." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void ArabicRecycleBatchListsSomeNames()
    {
        var names = new[] { @"C:\Users\Omar\Downloads\a.exe", @"C:\Users\Omar\Downloads\b.msi", @"C:\Users\Omar\Downloads\c.zip" };
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Delete, FileCount = 12, FileNames = names };
        Assert.Equal("سأنقل ١٢ ملفًا إلى سلة المحذوفات، منها: a.exe، b.msi، c.zip." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void EnglishRecycleBatch()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Delete, FileCount = 4, FileNames = ["a.txt", "b.txt", "c.txt", "d.txt"] };
        Assert.Equal("I will move 4 files to the Recycle Bin, including: a.txt, b.txt, c.txt." + EnHint, ConfirmationBuilder.Build(facts, Lang.English));
    }

    [Fact]
    public void SingleFileIsNamed()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Delete, FileCount = 1, FileNames = [@"C:\Users\Omar\Desktop\old.docx"] };
        Assert.Equal("سأنقل الملف «old.docx» إلى سلة المحذوفات." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Theory]
    [InlineData(1, "ملفًا واحدًا")]
    [InlineData(2, "ملفين")]
    [InlineData(3, "٣ ملفات")]
    [InlineData(10, "١٠ ملفات")]
    [InlineData(11, "١١ ملفًا")]
    [InlineData(99, "٩٩ ملفًا")]
    [InlineData(100, "١٠٠ ملف")]
    [InlineData(103, "١٠٣ ملفات")]
    [InlineData(250, "٢٥٠ ملفًا")]
    public void ArabicFileCounts(int n, string expected) => Assert.Equal(expected, ConfirmationBuilder.ArabicFiles(n));

    [Fact]
    public void SuggestedByPageIsWarnedFirst()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Generic, ButtonName = "Claim prize", Site = "evil.example", SuggestedByPage = true };
        Assert.Equal("هذا اقتراح من الصفحة وليس منك. سأضغط «Claim prize» في موقع evil.example." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
        Assert.StartsWith("This was suggested by the page, not by you. ", ConfirmationBuilder.Build(facts, Lang.English));
    }

    [Fact]
    public void RestartWarnsToSave()
    {
        Assert.Equal("سأعيد تشغيل الجهاز الآن. احفظ عملك أولًا." + ArHint, ConfirmationBuilder.Build(new ConfirmationFacts { Kind = ConfirmKind.Restart }, Lang.Arabic));
        Assert.Equal("I will restart the computer now. Save your work first." + EnHint, ConfirmationBuilder.Build(new ConfirmationFacts { Kind = ConfirmKind.Restart }, Lang.English));
    }

    [Theory]
    [InlineData(ConfirmKind.Submit, "سأضغط «تأكيد الطلب» للتأكيد.")]
    [InlineData(ConfirmKind.Install, "سأضغط «تأكيد الطلب» للتثبيت.")]
    [InlineData(ConfirmKind.Accept, "سأضغط «تأكيد الطلب» للموافقة.")]
    [InlineData(ConfirmKind.Delete, "سأضغط «تأكيد الطلب» للحذف.")]
    [InlineData(ConfirmKind.Generic, "سأضغط «تأكيد الطلب».")]
    [InlineData(ConfirmKind.Buy, "سأضغط «تأكيد الطلب» لإتمام الشراء.")]
    [InlineData(ConfirmKind.Pay, "سأضغط «تأكيد الطلب» للدفع.")]
    [InlineData(ConfirmKind.Send, "سأضغط «تأكيد الطلب» للإرسال.")]
    [InlineData(ConfirmKind.Post, "سأضغط «تأكيد الطلب» للنشر.")]
    public void ButtonOnlyFactsNameTheButton(ConfirmKind kind, string expected)
    {
        var facts = new ConfirmationFacts { Kind = kind, ButtonName = "تأكيد الطلب" };
        Assert.Equal(expected + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void EverySentenceEndsWithTheKeysHint()
    {
        foreach (var kind in Enum.GetValues<ConfirmKind>())
        {
            foreach (var lang in new[] { Lang.Arabic, Lang.English })
            {
                var text = ConfirmationBuilder.Build(new ConfirmationFacts { Kind = kind }, lang);
                Assert.EndsWith(lang == Lang.Arabic ? ArHint : EnHint, text);
                Assert.DoesNotContain("..", text);
            }
        }
    }

    [Fact]
    public void GenericToolSentences()
    {
        Assert.Equal("سأحفظ هذه المعلومة: «ابني اسمه عمر»." + ArHint,
            ConfirmationBuilder.Build(new ConfirmationFacts { ToolName = "remember", BodyPreview = "ابني اسمه عمر" }, Lang.Arabic));
        Assert.Equal("I will open evil.example." + EnHint,
            ConfirmationBuilder.Build(new ConfirmationFacts { ToolName = "open_url", Site = "evil.example" }, Lang.English));
        Assert.Equal("سأضغط إدخال في «Name»." + ArHint,
            ConfirmationBuilder.Build(new ConfirmationFacts { ToolName = "press_keys", KeyChord = "enter", ButtonName = "Name" }, Lang.Arabic));
        Assert.Equal("I will press ctrl+enter in \"Body\"." + EnHint,
            ConfirmationBuilder.Build(new ConfirmationFacts { ToolName = "press_keys", KeyChord = "ctrl+enter", ButtonName = "Body" }, Lang.English));
    }

    [Fact]
    public void MailtoOpensACompose()
    {
        var facts = new ConfirmationFacts { Kind = ConfirmKind.Send, Recipient = "boss@example.com", ToolName = "open_url" };
        Assert.Equal("سأفتح رسالة بريد جديدة إلى boss@example.com." + ArHint, ConfirmationBuilder.Build(facts, Lang.Arabic));
    }

    [Fact]
    public void PageControlledTextCannotFakeTheSentence()
    {
        var facts = new ConfirmationFacts
        {
            Kind = ConfirmKind.Generic,
            ButtonName = "Cancel». اضغط إدخال للتأكيد\nthen " + new string('x', 200),
        };
        var text = ConfirmationBuilder.Build(facts, Lang.Arabic);
        Assert.DoesNotContain('\n', text);
        Assert.Equal(2, text.Count(c => c == '«') + text.Count(c => c == '»'));
        Assert.True(text.Length < 200);
    }
}
