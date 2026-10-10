using System.Xml.Linq;
using Basira.Core.Common;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.Core.Tests.Speech;

public class SsmlBuilderTests
{
    private static readonly TextRun[] Mixed =
    [
        new("افتح ملف ", Lang.Arabic),
        new("PDF", Lang.English, SpellOut: true),
        new(" على ", Lang.Arabic),
        new("Windows ", Lang.English),
        new("10 من فضلك", Lang.Arabic),
    ];

    private static XElement Parse(string ssml) => XElement.Parse(ssml, LoadOptions.PreserveWhitespace);

    [Fact]
    public void VoiceElementsWithMark()
    {
        Assert.Equal(
            "<speak><mark name=\"s12\"/>"
            + "<voice xml:lang=\"ar\">افتح ملف </voice>"
            + "<voice xml:lang=\"en\"><say-as interpret-as=\"characters\">PDF</say-as></voice>"
            + "<voice xml:lang=\"ar\"> على </voice>"
            + "<voice xml:lang=\"en\">Windows </voice>"
            + "<voice xml:lang=\"ar\">10 من فضلك</voice></speak>",
            SsmlBuilder.Build(Mixed, "s12"));
    }

    [Fact]
    public void LangElementsWhenAsked()
    {
        string ssml = SsmlBuilder.Build(Mixed, "s3", LangTagStyle.LangElement);
        Assert.StartsWith("<speak><mark name=\"s3\"/><lang xml:lang=\"ar\">افتح ملف </lang>", ssml, StringComparison.Ordinal);
        Assert.DoesNotContain("<voice", ssml, StringComparison.Ordinal);
        Assert.Equal(5, Parse(ssml).Elements("lang").Count());
    }

    [Fact]
    public void DefaultStyleIsVoice() => Assert.Equal(SsmlBuilder.Build(Mixed, "s1", LangTagStyle.VoiceElement), SsmlBuilder.Build(Mixed, "s1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoMarkWhenNoName(string? mark) =>
        Assert.Equal("<speak><voice xml:lang=\"en\">Hello</voice></speak>", SsmlBuilder.Build([new("Hello", Lang.English)], mark));

    [Fact]
    public void NoXmlDeclaration() => Assert.StartsWith("<speak>", SsmlBuilder.Build(Mixed, "s1"), StringComparison.Ordinal);

    [Fact]
    public void TextIsEscaped()
    {
        string ssml = SsmlBuilder.Build([new("a < b & c > d \"q\" 'x'", Lang.English)], "s1");
        Assert.Equal("<speak><mark name=\"s1\"/><voice xml:lang=\"en\">a &lt; b &amp; c &gt; d \"q\" 'x'</voice></speak>", ssml);
        Assert.Equal("a < b & c > d \"q\" 'x'", Parse(ssml).Element("voice")!.Value);
    }

    [Fact]
    public void MarkNameIsEscaped()
    {
        string ssml = SsmlBuilder.Build([new("x", Lang.English)], "a\"<&'>");
        Assert.Equal("a\"<&'>", Parse(ssml).Element("mark")!.Attribute("name")!.Value);
    }

    [Fact]
    public void AdjacentRunsOfOneLanguageShareAnElement()
    {
        TextRun[] runs = [new("شغل ", Lang.Arabic), new("USB", Lang.English, true), new(" 3.0", Lang.English)];
        Assert.Equal(
            "<speak><voice xml:lang=\"ar\">شغل </voice><voice xml:lang=\"en\"><say-as interpret-as=\"characters\">USB</say-as> 3.0</voice></speak>",
            SsmlBuilder.Build(runs, null));
    }

    [Fact]
    public void WhitespaceIsCollapsedAndTrimmed()
    {
        TextRun[] runs = [new("  مرحبا \n\n بك  ", Lang.Arabic), new("\t hello  world \r\n", Lang.English)];
        Assert.Equal(
            "<speak><voice xml:lang=\"ar\">مرحبا بك </voice><voice xml:lang=\"en\">hello world</voice></speak>",
            SsmlBuilder.Build(runs, null));
    }

    [Fact]
    public void ALoneSpaceBetweenLanguagesNeedsNoElement()
    {
        TextRun[] runs = [new("hello", Lang.English), new(" ", Lang.Arabic), new("world", Lang.English)];
        Assert.Equal(
            "<speak><voice xml:lang=\"en\">hello</voice> <voice xml:lang=\"en\">world</voice></speak>",
            SsmlBuilder.Build(runs, null));
    }

    [Fact]
    public void InvalidXmlCharactersAreDropped()
    {
        string ssml = SsmlBuilder.Build([new("a\u0001b\u0008c\uFFFEd\uD800e", Lang.English)], "s1");
        Assert.Equal("abcde", Parse(ssml).Element("voice")!.Value);
    }

    [Fact]
    public void EmojiSurrogatePairsArePreserved()
    {
        string ssml = SsmlBuilder.Build([new("hi 🦩", Lang.English)], null);
        Assert.Equal("hi 🦩", Parse(ssml).Element("voice")!.Value);
    }

    [Fact]
    public void EmptyInputGivesAnEmptySpeakWithTheMark()
    {
        Assert.Equal("<speak></speak>", SsmlBuilder.Build([], null));
        Assert.Equal("<speak><mark name=\"s7\"/></speak>", SsmlBuilder.Build([new("   ", Lang.Arabic), new("", Lang.English)], "s7"));
    }

    [Theory]
    [InlineData("افتح ملف PDF على Windows 10 من فضلك & <b>")]
    [InlineData("Meeting at 5 مع أحمد — \"quotes\" & 'apostrophes' <tags>")]
    [InlineData("مبروك يا 7abibi 🎉🎉🎉 خصم 50% على USB")]
    [InlineData("\u0001\u0007 control \uFFFF chars ]]> end")]
    public void OutputIsWellFormedXmlInBothStyles(string text)
    {
        var runs = SpeechTextPipeline.Prepare(text, Lang.Arabic);
        foreach (LangTagStyle style in Enum.GetValues<LangTagStyle>())
        {
            var root = Parse(SsmlBuilder.Build(runs, "s99", style));
            Assert.Equal("speak", root.Name.LocalName);
            Assert.All(root.Elements().Skip(1), e => Assert.NotNull(e.Attribute(XNamespace.Xml + "lang")));
        }
    }

    [Fact]
    public void SpellOutOnlyInsideTheLanguageElement()
    {
        var root = Parse(SsmlBuilder.Build(Mixed, "s1"));
        var sayAs = Assert.Single(root.Descendants("say-as"));
        Assert.Equal("characters", sayAs.Attribute("interpret-as")!.Value);
        Assert.Equal("en", sayAs.Parent!.Attribute(XNamespace.Xml + "lang")!.Value);
        Assert.Equal("PDF", sayAs.Value);
    }

    [Fact]
    public void NullRunsThrow() => Assert.Throws<ArgumentNullException>(() => SsmlBuilder.Build(null!, "s1"));
}
