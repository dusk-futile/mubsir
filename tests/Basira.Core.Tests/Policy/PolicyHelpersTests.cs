using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Policy;

namespace Basira.Core.Tests.Policy;

public class PolicyHelpersTests
{
    [Theory]
    [InlineData("Ctrl+Enter", "ctrl+enter")]
    [InlineData("ctrl + t", "ctrl+t")]
    [InlineData("Win+R", "win+r")]
    [InlineData("windows+r", "win+r")]
    [InlineData("LWin + R", "win+r")]
    [InlineData("shift+ctrl+enter", "ctrl+shift+enter")]
    [InlineData("Alt+S", "alt+s")]
    [InlineData("Return", "enter")]
    [InlineData("Page Down", "pagedown")]
    [InlineData("PgUp", "pageup")]
    [InlineData("Esc", "escape")]
    [InlineData("ctrl++", "ctrl+plus")]
    public void NormalizesChords(string raw, string expected) => Assert.Equal([expected], KeyChords.Parse(raw));

    [Fact]
    public void ParsesSequences() => Assert.Equal(["alt+d", "shift+enter"], KeyChords.Parse("Alt+D, Shift+Enter"));

    [Theory]
    [InlineData("a", true)]
    [InlineData("shift+a", true)]
    [InlineData("1", true)]
    [InlineData("space", true)]
    [InlineData("ctrl+a", false)]
    [InlineData("enter", false)]
    [InlineData("pagedown", false)]
    public void TypingChords(string chord, bool typing) => Assert.Equal(typing, KeyChords.IsTyping(chord));

    [Theory]
    [InlineData(@"C:\Users\Omar\Desktop\a.txt", @"c:\users\omar\desktop\a.txt")]
    [InlineData("C:/Users/Omar/Desktop/", @"c:\users\omar\desktop")]
    [InlineData(@"C:\Users\Omar\Documents\..\Desktop\.\x", @"c:\users\omar\desktop\x")]
    [InlineData(@"C:\Windows.\System32", @"c:\windows\system32")]
    [InlineData(@"\\server\share\folder", @"\\server\share\folder")]
    [InlineData(@"C:\", @"c:\")]
    public void NormalizesWindowsPaths(string raw, string expected) => Assert.Equal(expected, PolicyRules.NormalizePath(raw));

    [Theory]
    [InlineData("Desktop")]
    [InlineData(@"Users\Omar")]
    [InlineData(@"C:folder")]
    [InlineData(@"%APPDATA%\x")]
    [InlineData(@"\\?\C:\Windows")]
    [InlineData(@"C:\a\..\..\x")]
    [InlineData(@"C:\file.txt:secret")]
    [InlineData(@"\\server")]
    [InlineData(@"\\server\share\..\..\x")]
    [InlineData("file:///C:/x")]
    [InlineData(@"C:\a\...\b")]
    [InlineData(@"C:\a\b*")]
    [InlineData("")]
    public void RejectsUnsafeOrRelativePaths(string raw) => Assert.Null(PolicyRules.NormalizePath(raw));

    [Theory]
    [InlineData("https://www.YouTube.com/watch?v=1", "https", "youtube.com")]
    [InlineData("youtube.com", "https", "youtube.com")]
    [InlineData("example.com:8080/x", "https", "example.com")]
    [InlineData("JAVASCRIPT:alert(1)", "javascript", "")]
    [InlineData("file:///C:/Users/x.exe", "file", "")]
    [InlineData(@"C:\Users\x.exe", "file", "")]
    [InlineData("mailto:a@b.com?subject=hi", "mailto", "b.com")]
    [InlineData("search-ms:query=x", "search-ms", "")]
    public void ParsesUrls(string raw, string scheme, string host)
    {
        Assert.True(PolicyRules.TryParseUrl(raw, allowSchemeless: true, out var url));
        Assert.Equal(scheme, url.Scheme);
        Assert.Equal(host, url.Host);
    }

    [Fact]
    public void MailtoAddressIsExtracted()
    {
        Assert.True(PolicyRules.TryParseUrl("mailto:boss@example.com?subject=hi", allowSchemeless: true, out var url));
        Assert.Equal("boss@example.com", url.MailAddress);
    }

    [Theory]
    [InlineData("java\tscript:alert(1)")]
    [InlineData("hello world")]
    [InlineData("")]
    public void RejectsInvalidUrls(string raw) => Assert.False(PolicyRules.TryParseUrl(raw, allowSchemeless: true, out _));

    [Theory]
    [InlineData("setup.exe", "exe")]
    [InlineData(@"C:\x\invoice.pdf.exe", "exe")]
    [InlineData("Report.PDF", "pdf")]
    [InlineData("README", null)]
    [InlineData("name.", null)]
    public void Extensions(string name, string? ext) => Assert.Equal(ext, PolicyRules.ExtensionOf(name));

    [Theory]
    [InlineData("Watch Messi's best goals", "شغل فيديو اهداف ميسي, Messi best goals", true)]
    [InlineData("Home", "go home", true)]
    [InlineData("Click here to verify your account", "check my account", false)]
    [InlineData("Claim your free prize now", "read the page", false)]
    [InlineData("أخبار الرياضة", "افتح أخبار الرياضة", true)]
    [InlineData("", "anything", false)]
    public void NamedInUtterance(string name, string utterance, bool named) =>
        Assert.Equal(named, PolicyRules.NamedInUtterance(name, utterance));

    [Theory]
    [InlineData("My son's name is Omar", "remember that my son's name is Omar", true)]
    [InlineData("ابني اسمه عمر", "افتكر إن ابني اسمه عمر", true)]
    [InlineData("The user's bank PIN is 1234", "remember my son is Omar", false)]
    [InlineData("", "remember", false)]
    public void FactFromUtterance(string fact, string utterance, bool fromUser) =>
        Assert.Equal(fromUser, PolicyRules.FactFromUtterance(fact, utterance));

    [Theory]
    [InlineData("www.youtube.com", "open youtube", true)]
    [InlineData("m.youtube.com", "افتح يوتيوب", true)]
    [InlineData("vodafone.com.eg", "open vodafone please", true)]
    [InlineData("evil.example", "open youtube", false)]
    [InlineData("youtube.com.evil.example", "open youtube", false)]
    public void UserNamedDomains(string host, string utterance, bool named) =>
        Assert.Equal(named, PolicyRules.IsUserNamedDomain(host, new TaskContext { UserUtterance = utterance }));

    [Fact]
    public void ListedDomainsCountAsNamed()
    {
        var task = new TaskContext { UserUtterance = "افتح موقع الجامعة", UserNamedDomains = new HashSet<string> { "cu.edu.eg" } };
        Assert.True(PolicyRules.IsUserNamedDomain("www.cu.edu.eg", task));
        Assert.True(PolicyRules.IsUserNamedDomain("portal.cu.edu.eg", task));
        Assert.False(PolicyRules.IsUserNamedDomain("cu.edu.eg.evil.example", task));
    }

    [Theory]
    [InlineData("chrome.exe", true)]
    [InlineData(@"C:\Program Files\WindowsApps\WhatsApp.exe", true)]
    [InlineData("OUTLOOK.EXE", true)]
    [InlineData("AcroRd32.exe", true)]
    [InlineData("WINWORD.EXE", true)]
    [InlineData("explorer.exe", false)]
    [InlineData("SystemSettings.exe", false)]
    public void UntrustedSources(string process, bool untrusted) =>
        Assert.Equal(untrusted, PolicyRules.IsUntrustedSource(new ScreenContext { ProcessName = process }));

    [Fact]
    public void ArabicDigitsHelper() => Assert.Equal("١٢٣", PolicyText.ToArabicDigits("123"));

    [Fact]
    public void FoldingUnifiesArabicLetters() =>
        Assert.Equal("ارسال الرساله الي مدرسه", PolicyText.Fold("إرْسَال  الرِّسالة إلى مدرسة"));

    [Fact]
    public void TokensSplitCamelCaseAndSymbols() =>
        Assert.Equal(["btn", "send", "message", "cvv2"], PolicyText.Tokens("btnSend-Message_cvv2"));

    [Fact]
    public void LangIsUsedByConfirmations() => Assert.Equal(Lang.Arabic, new TaskContext().Lang);
}
