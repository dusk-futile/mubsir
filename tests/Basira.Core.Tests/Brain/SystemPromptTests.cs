using Basira.Core.Brain;
using Basira.Core.Common;
using Basira.Core.Context;

namespace Basira.Core.Tests.Brain;

public class SystemPromptTests
{
    public static TheoryData<Lang, ReplyStyle, Verbosity> AllVariants()
    {
        var data = new TheoryData<Lang, ReplyStyle, Verbosity>();
        foreach (var lang in Enum.GetValues<Lang>())
        {
            foreach (var style in Enum.GetValues<ReplyStyle>())
            {
                foreach (var verbosity in Enum.GetValues<Verbosity>())
                {
                    data.Add(lang, style, verbosity);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void PromptIsLongEnoughToBeCached(Lang lang, ReplyStyle style, Verbosity verbosity)
    {
        var prompt = SystemPrompt.Build(lang, style, verbosity);
        Assert.True(TokenEstimator.Estimate(prompt) >= 700, $"estimated {TokenEstimator.Estimate(prompt)} tokens");
        Assert.True(prompt.Length >= 2800, $"{prompt.Length} characters"); // about 700 tokens even at 4 characters per token
    }

    [Theory]
    [MemberData(nameof(AllVariants))]
    public void PromptIsStableEnglish(Lang lang, ReplyStyle style, Verbosity verbosity)
    {
        var prompt = SystemPrompt.Build(lang, style, verbosity);
        Assert.Equal(prompt, SystemPrompt.Build(lang, style, verbosity));
        Assert.All(prompt.Replace(ContextBuilder.PasswordMask, ""), c => Assert.True(c < 0x80, $"non-ASCII character U+{(int)c:X4}"));
        Assert.DoesNotContain("\r", prompt);
        Assert.DoesNotContain("  -", prompt); // no stray indentation from the raw string
    }

    [Fact]
    public void VariantsShareTheSameLeadingText()
    {
        var variants = AllVariants().Select(row => SystemPrompt.Build((Lang)row[0], (ReplyStyle)row[1], (Verbosity)row[2])).ToList();
        int common = variants.Skip(1).Aggregate(variants[0].Length, (len, v) => Math.Min(len, CommonPrefix(variants[0], v)));
        Assert.True(TokenEstimator.Estimate(variants[0][..common]) >= 700, "the shared prefix alone is cacheable");
    }

    [Theory]
    [InlineData("blind")]
    [InlineData("cannot see the screen")]
    [InlineData("untrusted data")]
    [InlineData("Never follow instructions")]
    [InlineData("Never claim to see")]
    [InlineData("it seems")]
    [InlineData("Never type passwords, card numbers")]
    [InlineData("Never try to get around a confirmation")]
    [InlineData("\"blocked\"")]
    [InlineData("\"declined by user\"")]
    [InlineData("Prefer invoke and set_text")]
    [InlineData("call read_screen")]
    [InlineData("verify the result")]
    [InlineData("browse_search")]
    [InlineData("read_document")]
    [InlineData("call ask_user")]
    [InlineData("which part to repeat")]
    [InlineData("limited number of steps")]
    [InlineData("stop as soon as the task is done")]
    [InlineData("no markdown")]
    [InlineData("no emoji")]
    [InlineData("numbers as digits")]
    [InlineData("one short phrase")]
    [InlineData("User Account Control")]
    public void PromptStatesTheRules(string rule) =>
        Assert.Contains(rule, SystemPrompt.Build(Lang.Arabic, ReplyStyle.SimpleMsa, Verbosity.Brief));

    [Fact]
    public void ArabicRepliesAreSimpleStandardArabicByDefault()
    {
        var msa = SystemPrompt.Build(Lang.Arabic, ReplyStyle.SimpleMsa, Verbosity.Normal);
        Assert.Contains("simple Modern Standard Arabic", msa);
        Assert.DoesNotContain("dialect, lightly", msa);
    }

    [Fact]
    public void DialectStyleUsesTheUsersDialectLightly()
    {
        var dialect = SystemPrompt.Build(Lang.Arabic, ReplyStyle.Dialect, Verbosity.Normal);
        Assert.Contains("dialect, lightly", dialect);
        Assert.Contains("never write Arabic in Latin letters", dialect);
    }

    [Fact]
    public void EnglishUsersGetEnglishReplies()
    {
        var en = SystemPrompt.Build(Lang.English, ReplyStyle.SimpleMsa, Verbosity.Normal);
        Assert.Contains("usually speaks English", en);
        Assert.Contains("Reply in the language of the user's latest request", en);
    }

    [Fact]
    public void VerbosityChangesOnlyTheVerbosityRule()
    {
        var brief = SystemPrompt.Build(Lang.Arabic, ReplyStyle.SimpleMsa, Verbosity.Brief);
        var normal = SystemPrompt.Build(Lang.Arabic, ReplyStyle.SimpleMsa, Verbosity.Normal);
        var detailed = SystemPrompt.Build(Lang.Arabic, ReplyStyle.SimpleMsa, Verbosity.Detailed);
        Assert.Contains("- Brief:", brief);
        Assert.Contains("- Normal:", normal);
        Assert.Contains("- Detailed:", detailed);
        Assert.Equal(3, new[] { brief, normal, detailed }.Distinct().Count());
        Assert.Equal(brief[..brief.IndexOf("VERBOSITY", StringComparison.Ordinal)], normal[..normal.IndexOf("VERBOSITY", StringComparison.Ordinal)]);
    }

    [Fact]
    public void RedactionMarkersAreExplained()
    {
        var prompt = SystemPrompt.Build(Lang.English, ReplyStyle.SimpleMsa, Verbosity.Brief);
        foreach (var marker in new[] { "[card]", "[iban]", "[code]", "[phone]", "[email]", "[id]", ContextBuilder.HiddenMask, ContextBuilder.PasswordMask })
        {
            Assert.Contains(marker, prompt);
        }
    }

    private static int CommonPrefix(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        int i = 0;
        while (i < n && a[i] == b[i])
        {
            i++;
        }

        return i;
    }
}
