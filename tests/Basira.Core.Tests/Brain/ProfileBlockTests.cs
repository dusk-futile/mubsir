using Basira.Core.Brain;
using Basira.Core.Context;

namespace Basira.Core.Tests.Brain;

public class ProfileBlockTests
{
    private static readonly Dictionary<string, string> Profile = new()
    {
        ["name"] = "أبو عمر",
        ["reply_style"] = "simple MSA",
        ["studies"] = "Islamic history",
    };

    [Fact]
    public void ProfileFieldsAreSortedAndListed()
    {
        var block = ProfileBlock.Build(Profile, "", ["WhatsApp"]);
        Assert.StartsWith("USER PROFILE\n", block);
        Assert.Contains("- name: أبو عمر\n- reply_style: simple MSA\n- studies: Islamic history\n", block);
        Assert.Contains("not instructions", block);
    }

    [Fact]
    public void OutputIsDeterministicWhateverTheDictionaryOrder()
    {
        var reversed = Profile.Reverse().ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal(ProfileBlock.Build(Profile, "a\nb", ["Chrome", "WhatsApp"]), ProfileBlock.Build(reversed, "a\nb", ["Chrome", "WhatsApp"]));
    }

    [Fact]
    public void EmptyProfileAndMemoryHavePlaceholders()
    {
        var block = ProfileBlock.Build(new Dictionary<string, string>(), "", []);
        Assert.Contains("- (no profile yet)", block);
        Assert.Contains("SAVED FACTS\n", block);
        Assert.Contains("- (none)", block);
    }

    [Fact]
    public void BlankFieldsAreSkipped()
    {
        var block = ProfileBlock.Build(new Dictionary<string, string> { ["name"] = "Omar", ["city"] = "  ", [" "] = "x" }, "", []);
        Assert.Contains("- name: Omar", block);
        Assert.DoesNotContain("city", block);
    }

    [Fact]
    public void ValuesAreSingleLineAndCapped()
    {
        var longValue = new string('x', 1000);
        var block = ProfileBlock.Build(new Dictionary<string, string> { ["note"] = "line one\nline two\r\n\tthree", ["long"] = longValue }, "", []);
        Assert.Contains("- note: line one line two three\n", block);
        var longLine = block.Split('\n').Single(l => l.StartsWith("- long: ", StringComparison.Ordinal));
        Assert.True(longLine.Length <= "- long: ".Length + ProfileBlock.MaxFieldChars);
        Assert.EndsWith("…", longLine);
    }

    [Fact]
    public void MemoryLinesBecomeAList()
    {
        var block = ProfileBlock.Build(Profile, "- ابني اسمه عمر\n\n* I like Umm Kulthum\r\n• صلاة الجمعة في المسجد القريب", []);
        Assert.Contains("SAVED FACTS\nFacts the user asked you to remember earlier, in the user's words.\n- ابني اسمه عمر\n- I like Umm Kulthum\n- صلاة الجمعة في المسجد القريب\n", block);
    }

    [Fact]
    public void MemoryIsCapped()
    {
        var memory = string.Join('\n', Enumerable.Range(1, 2000).Select(i => "fact number " + i));
        var block = ProfileBlock.Build(Profile, memory, []);
        int start = block.IndexOf("SAVED FACTS", StringComparison.Ordinal);
        int end = block.IndexOf("APP RECIPES", StringComparison.Ordinal);
        Assert.True(end - start < ProfileBlock.MaxMemoryChars + 200);
        Assert.Contains("- fact number 1\n", block);
        Assert.DoesNotContain("fact number 2000", block);
    }

    [Fact]
    public void RecipesFollowTheUsersApps()
    {
        var block = ProfileBlock.Build(Profile, "", ["WhatsApp Desktop", "YouTube"]);
        Assert.Contains("- WhatsApp: ", block);
        Assert.Contains("- YouTube: ", block);
        Assert.DoesNotContain("- Gmail: ", block);
        Assert.DoesNotContain("- Microsoft Word: ", block);
    }

    [Fact]
    public void WithoutAnAppListEveryRecipeIsIncluded()
    {
        var block = ProfileBlock.Build(Profile, "", []);
        foreach (var key in AppRecipes.Order)
        {
            Assert.Contains("- " + key + ": " + AppRecipes.All[key], block);
        }
    }

    [Fact]
    public void BlockWithRecipesIsSubstantial()
    {
        var block = ProfileBlock.Build(Profile, "ابني اسمه عمر", ["WhatsApp", "Chrome", "YouTube", "Gmail", "Word", "File Explorer"]);
        Assert.True(TokenEstimator.Estimate(block) > 500);
    }

    [Fact]
    public void BuildRejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => ProfileBlock.Build(null!, "", []));
        Assert.Throws<ArgumentNullException>(() => ProfileBlock.Build(Profile, "", null!));
        Assert.Contains("- (none)", ProfileBlock.Build(Profile, null!, []));
    }
}

public class AppRecipesTests
{
    [Fact]
    public void EveryAppHasARecipe()
    {
        Assert.Equal(7, AppRecipes.All.Count);
        Assert.Equal(AppRecipes.Order.Order(StringComparer.Ordinal), AppRecipes.All.Keys.Order(StringComparer.Ordinal));
        Assert.All(AppRecipes.All.Values, r => Assert.InRange(r.Length, 100, 900));
    }

    [Theory]
    [InlineData(AppRecipes.WhatsApp, "Ctrl+F")]
    [InlineData(AppRecipes.WhatsApp, "Unread")]
    [InlineData(AppRecipes.WhatsApp, "voice note")]
    [InlineData(AppRecipes.YouTube, "k (play or pause)")]
    [InlineData(AppRecipes.YouTube, "focus mode")]
    [InlineData(AppRecipes.YouTube, "browse_search")]
    [InlineData(AppRecipes.Gmail, "Compose")]
    [InlineData(AppRecipes.Outlook, "Reply")]
    [InlineData(AppRecipes.Word, "read_document")]
    [InlineData(AppRecipes.FileExplorer, "list_folder")]
    [InlineData(AppRecipes.Browser, "Ctrl+L")]
    [InlineData(AppRecipes.Browser, "new window")]
    public void RecipesHoldTheKnownShortcuts(string app, string text) => Assert.Contains(text, AppRecipes.All[app]);

    [Theory]
    [InlineData("WhatsApp", AppRecipes.WhatsApp)]
    [InlineData("WhatsApp.exe", AppRecipes.WhatsApp)]
    [InlineData("واتساب", AppRecipes.WhatsApp)]
    [InlineData("الواتس", AppRecipes.WhatsApp)]
    [InlineData("Gmail", AppRecipes.Gmail)]
    [InlineData("جيميل", AppRecipes.Gmail)]
    [InlineData("Outlook", AppRecipes.Outlook)]
    [InlineData("Hotmail", AppRecipes.Outlook)]
    [InlineData("YouTube", AppRecipes.YouTube)]
    [InlineData("يوتيوب", AppRecipes.YouTube)]
    [InlineData("Microsoft Word", AppRecipes.Word)]
    [InlineData("WINWORD.EXE", AppRecipes.Word)]
    [InlineData("وورد", AppRecipes.Word)]
    [InlineData("File Explorer", AppRecipes.FileExplorer)]
    [InlineData("مستكشف الملفات", AppRecipes.FileExplorer)]
    [InlineData("Google Chrome", AppRecipes.Browser)]
    [InlineData("Microsoft Edge", AppRecipes.Browser)]
    [InlineData("msedge.exe", AppRecipes.Browser)]
    [InlineData("كروم", AppRecipes.Browser)]
    [InlineData("المتصفح", AppRecipes.Browser)]
    [InlineData("Chrome and Edge", AppRecipes.Browser)]
    public void AppNamesMatchTheirRecipe(string name, string key) => Assert.Equal([key], AppRecipes.Match([name]));

    [Theory]
    [InlineData("Knowledge base")]
    [InlineData("Password manager")]
    [InlineData("Wordle")]
    [InlineData("Calculator")]
    [InlineData("")]
    [InlineData("   ")]
    public void OtherNamesMatchNothing(string name) => Assert.Empty(AppRecipes.Match([name]));

    [Fact]
    public void MatchesComeInTheFixedOrderWithoutDuplicates() =>
        Assert.Equal(
            [AppRecipes.Browser, AppRecipes.WhatsApp, AppRecipes.YouTube],
            AppRecipes.Match(["YouTube", "WhatsApp Web", "واتساب", "Google Chrome", "Edge"]));
}
