using Mubsir.Core.Agent;
using Mubsir.Core.Brain;
using Mubsir.Core.Common;
using Mubsir.Core.Context;
using Mubsir.Core.Library;
using Mubsir.Core.Onboarding;
using Mubsir.Core.Policy;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;
using Mubsir.Core.Web;
using Xunit;

namespace Mubsir.Core.Tests.Web;

public class FullControlTests
{
    private static UiNode Link(string name, string? url) => new() { Role = "link", Name = name, Value = url };

    private static readonly PolicyEngine Policy = new();

    private static PolicyDecision Check(string tool, Dictionary<string, string?>? args = null, UiNode? target = null, TaskContext? task = null, ScreenContext? screen = null, string? path = null) =>
        Policy.Evaluate(new AgentAction
        {
            Tool = tool,
            Args = args ?? [],
            Target = target,
            Screen = screen,
            FilePaths = path is null ? [] : [path],
        }, task ?? new TaskContext { LibraryRoots = [@"C:\Users\me\Documents"] });

    [Fact]
    public void Results_keep_real_sites_and_drop_ads_and_the_search_page()
    {
        UiNode[] nodes =
        [
            Link("Sign in to your account now", "https://accounts.google.com/x"),
            Link("Images and more from Google", "https://www.google.com/imghp"),
            Link("Sponsored: buy prayer apps today", "https://ads.example.com/"),
            Link("Prayer times in Cairo today", "https://www.islamicfinder.org/cairo"),
            Link("مواعيد الصلاة في القاهرة اليوم", "https://example.eg/prayer"),
            Link("Prayer times in Cairo today", "https://www.islamicfinder.org/cairo"),
            Link("Short", "https://a.com/"),
            Link("A link with no address at all", null),
        ];
        IReadOnlyList<SearchResult> results = SearchResults.Pick(nodes);
        Assert.Equal(2, results.Count);
        Assert.Equal("islamicfinder.org", results[0].Site);
        Assert.StartsWith("1. Prayer times", SearchResults.Text(results), StringComparison.Ordinal);
        Assert.Contains("2. مواعيد", SearchResults.Text(results), StringComparison.Ordinal);
    }

    [Fact]
    public void No_more_than_five_results_are_read()
    {
        var nodes = Enumerable.Range(1, 12).Select(i => Link("A good result number " + i, "https://site" + i + ".com/"));
        Assert.Equal(SearchResults.MaxResults, SearchResults.Pick(nodes).Count);
    }

    [Theory]
    [InlineData("the word /ʃɪp/ here", "the word the sh sound, i as in sit, p here")]
    [InlineData("ordinary words stay the same", "ordinary words stay the same")]
    [InlineData("angle θ and beta β", "angle θ and beta β")]
    public void Phonetics_are_said_sound_by_sound_and_other_text_is_left(string text, string heard) =>
        Assert.Equal(heard, IpaReader.Read(text, Lang.English));

    [Fact]
    public void Phonetics_have_arabic_names_too() =>
        Assert.Contains("صوت الشين", IpaReader.Read("/ʃ/", Lang.Arabic), StringComparison.Ordinal);

    private static LibraryCard Card(string title = "Introduction to phonology", int pages = 200, string? opening = null, string path = "/b/x.pdf") =>
        new(path, null, title, null, pages, opening ?? new string('a', 300), 1000, DateTime.UtcNow);

    [Fact]
    public void Only_real_books_are_described()
    {
        Assert.True(BookDescriber.Worth(Card()));
        Assert.False(BookDescriber.Worth(Card(pages: 3)));
        Assert.False(BookDescriber.Worth(Card("Bank statement March")));
        Assert.False(BookDescriber.Worth(Card(path: @"C:\b\فاتورة كهرباء.pdf")));
        Assert.False(BookDescriber.Worth(Card(opening: "Card 4111 1111 1111 1111 " + new string('a', 300))));
        Assert.False(BookDescriber.Worth(Card() with { Locked = true }));
        Assert.False(BookDescriber.Worth(Card() with { About = "done" }));
    }

    [Fact]
    public void A_description_makes_a_book_findable_and_never_replaces_a_real_title()
    {
        BookDescription? d = BookDescriber.Parse("Here: {\"title\":\"Sounds of Language\",\"about\":\"About speech sounds. It covers IPA.\",\"keywords\":\"صوتيات phonetics\"}");
        Assert.NotNull(d);
        Assert.Equal("Introduction to phonology", BookDescriber.Apply(Card(), d).Title);
        Assert.Equal("Sounds of Language", BookDescriber.Apply(Card("x"), d).Title);

        string file = Path.GetTempFileName();
        try
        {
            var library = new BookLibrary(file);
            library.Put(BookDescriber.Apply(Card("Book one"), d));
            Assert.Single(library.Search("صوتيات"));
            Assert.Contains("About speech sounds.", BookListText.List(library.Search("صوتيات"), Lang.English), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"title\":\"x\"}")]
    [InlineData("{\"about\":5}")]
    public void A_bad_description_is_ignored(string answer) => Assert.Null(BookDescriber.Parse(answer));

    [Theory]
    [InlineData("{\"found\":false}")]
    [InlineData("{\"found\":true,\"x\":5000,\"y\":10}")]
    [InlineData("nothing here")]
    public void A_click_is_only_made_on_a_sure_spot(string answer) => Assert.Null(ClickSpot.Parse(answer));

    [Fact]
    public void A_spot_is_read_with_its_label()
    {
        ClickSpot? spot = ClickSpot.Parse("{\"found\":true,\"x\":500,\"y\":250,\"label\":\"Play\"}");
        Assert.Equal(new ClickSpot(500, 250, "Play"), spot);
    }

    [Fact]
    public void Clicking_by_sight_still_asks_before_paying()
    {
        var pay = new UiNode { Role = "button", Name = "Pay now", AutomationId = "the green button" };
        Assert.Equal(ActionTier.Confirm, Check(ToolNames.ClickOn, target: pay).Tier);
        var play = new UiNode { Role = "button", Name = "Play", AutomationId = "the play button" };
        Assert.Equal(ActionTier.Allow, Check(ToolNames.ClickOn, target: play).Tier);
    }

    [Fact]
    public void A_click_by_sight_a_page_could_have_asked_for_needs_the_user()
    {
        var task = new TaskContext { UntrustedContentSeen = true };
        var play = new UiNode { Role = "button", Name = "Play", AutomationId = "the play button" };
        Assert.Equal(ActionTier.Confirm, Check(ToolNames.ClickOn, target: play, task: task).Tier);
    }

    [Theory]
    [InlineData(ToolNames.ClickOn)]
    [InlineData(ToolNames.OpenFile)]
    [InlineData(ToolNames.CreateFolder)]
    [InlineData(ToolNames.ReadClipboard)]
    public void Teaching_can_never_use_the_hands(string tool) =>
        Assert.Equal(ActionTier.Block, Check(tool, task: new TaskContext { TeachMode = true }).Tier);

    [Fact]
    public void Private_mode_takes_no_picture_to_click() =>
        Assert.Equal(ActionTier.Block, Check(ToolNames.ClickOn, task: new TaskContext { PrivateMode = true }).Tier);

    [Theory]
    [InlineData(@"C:\Users\me\Documents\a.pdf", ActionTier.Allow)]
    [InlineData(@"C:\Users\me\Documents\setup.exe", ActionTier.Confirm)]
    [InlineData(@"C:\Users\me\Documents\setup.msi", ActionTier.Confirm)]
    [InlineData(@"C:\Users\me\Documents\run.ps1", ActionTier.Block)]
    [InlineData(@"C:\Users\me\Documents\run.bat", ActionTier.Block)]
    [InlineData(@"C:\Users\me\Documents\macro.docm", ActionTier.Block)]
    [InlineData(@"C:\Users\me\Documents\page.html", ActionTier.Block)]
    [InlineData(@"C:\Users\me\Documents\noextension", ActionTier.Block)]
    public void Opening_a_file_depends_on_what_it_can_do(string path, ActionTier tier)
    {
        var task = new TaskContext { LibraryRoots = [@"C:\Users\me\Documents"] };
        Assert.Equal(tier, Check(ToolNames.OpenFile, new Dictionary<string, string?> { ["path"] = path }, task: task, path: path).Tier);
    }

    [Fact]
    public void An_installer_a_page_suggested_still_needs_the_key_press()
    {
        string path = @"C:\Users\me\Documents\setup.exe";
        var task = new TaskContext { LibraryRoots = [@"C:\Users\me\Documents"], UntrustedContentSeen = true };
        PolicyDecision decision = Check(ToolNames.OpenFile, new Dictionary<string, string?> { ["path"] = path }, task: task, path: path);
        Assert.Equal(ActionTier.Confirm, decision.Tier);
        Assert.Equal(ConfirmKind.Install, decision.Facts?.Kind);
    }

    [Fact]
    public void Reading_the_clipboard_after_a_page_was_read_needs_the_user() =>
        Assert.Equal(ActionTier.Confirm, Check(ToolNames.ReadClipboard, task: new TaskContext { UntrustedContentSeen = true }).Tier);

    [Fact]
    public void Every_voice_has_a_number_an_id_and_no_signed_link()
    {
        foreach (Lang lang in new[] { Lang.Arabic, Lang.English })
        {
            IReadOnlyList<FishVoiceChoice> voices = VoiceCatalog.For(lang);
            Assert.Equal(Enumerable.Range(1, voices.Count), voices.Select(v => v.Number));
            Assert.All(voices, v =>
            {
                Assert.Equal(32, v.Id.Length);
                Assert.StartsWith("https://platform.r2.fish.audio/", v.Sample, StringComparison.Ordinal);
                Assert.DoesNotContain("?", v.Sample, StringComparison.Ordinal);
            });
        }
    }

    [Theory]
    [InlineData("أيوه", true)]
    [InlineData("لأ", false)]
    [InlineData("مش فاهم xyz", false)]
    public void A_reader_is_asked_before_books_are_described(string answer, bool describes)
    {
        var flow = new OnboardingFlow(new OnboardingFacts(ScreenReaderRunning: true), Lang.Arabic);
        int guard = 0;
        while (flow.Current is { } step && guard++ < 40)
        {
            flow.Answer(step.Id switch
            {
                "blind_goal" => "كتب",
                "describe_books" => answer,
                _ => "خلاص",
            });
        }

        Assert.Equal(describes, flow.Plan().DescribeBooks);
    }
}
