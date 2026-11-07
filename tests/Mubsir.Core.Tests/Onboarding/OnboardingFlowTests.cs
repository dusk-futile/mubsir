using Mubsir.Core.Brain;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Onboarding;
using Mubsir.Core.Policy;
using Mubsir.Core.Sessions;
using Mubsir.Core.Setup;

namespace Mubsir.Core.Tests.Onboarding;

public class OnboardingFlowTests
{
    private static OnboardingFlow Run(bool screenReader, params string[] answers)
    {
        var flow = new OnboardingFlow(new OnboardingFacts(screenReader));
        foreach (string answer in answers)
        {
            flow.Answer(answer);
        }

        return flow;
    }

    [Fact]
    public void A_running_screen_reader_means_the_sight_question_is_never_asked()
    {
        var flow = Run(true, "عربي");
        Assert.Equal("blind_goal", flow.Current!.Id);
    }

    [Fact]
    public void A_blind_reader_gets_a_subject_question_then_speed_and_the_plan_follows_the_answers()
    {
        OnboardingFlow flow = Run(false, "عربي", "مش بشوف الشاشة", "عايز اقرا كتب", "الفونولوجيا", "لأ", "سريعة", "ولا حاجة", "خلاص");
        Assert.Null(flow.Current);
        PersonalPlan plan = flow.Plan();
        Assert.Equal(UserKind.Blind, plan.Kind);
        Assert.True(plan.UseNvda);
        Assert.Equal(Verbosity.Brief, plan.Verbosity);
        Assert.Equal(70, plan.SpeechRate);
        Assert.Contains("الفونولوجيا", plan.FirstSuggestion, StringComparison.Ordinal);
    }

    [Fact]
    public void The_only_choices_are_blind_and_low_vision()
    {
        var flow = Run(false, "arabic");
        Assert.Equal("sight", flow.Current!.Id);
        Assert.Equal(["blind", "lowvision"], flow.Current.Options.Select(o => o.Id));
    }

    [Fact]
    public void A_mixed_language_answer_is_understood()
    {
        OnboardingFlow flow = Run(false, "arabic", "low vision", "عايز اقرا books");
        Assert.Equal("books", flow.Plan().Goal);
    }

    [Fact]
    public void Nobody_can_get_stuck_on_a_question_it_cant_understand()
    {
        var flow = new OnboardingFlow(new OnboardingFacts(false));
        for (int i = 0; i < 30 && flow.Current is not null; i++)
        {
            flow.Answer("مش فاهم حاجة xyz");
        }

        Assert.Null(flow.Current);
        Assert.Equal(UserKind.Blind, flow.Plan().Kind);
    }

    [Fact]
    public void The_plan_changes_the_settings_and_marks_setup_done()
    {
        var settings = new MubsirSettings();
        Run(false, "arabic", "low vision", "books", "skip", "لأ", "none", "skip").Plan().ApplyTo(settings);
        Assert.True(settings.OnboardingDone);
        Assert.Equal(UserKind.LowVision, settings.UserKind);
    }

    [Theory]
    [InlineData("sk-ant-api03-abcdefghijklmnopqrstuvwx", KeyKind.Anthropic)]
    [InlineData("  sk-ant-api03-abcdefghijklmnopqrstuvwx\n", KeyKind.Anthropic)]
    [InlineData("AIzaSyA1234567890abcdefghijklmnop", KeyKind.Gemini)]
    [InlineData("0123456789abcdef0123456789abcdef", KeyKind.FishAudio)]
    public void A_copied_key_is_recognised(string copied, KeyKind kind) => Assert.Equal(kind, KeySetup.Recognize(copied));

    [Theory]
    [InlineData("my password is sk-ant-123456789012345678901234")]
    [InlineData("short")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("just a sentence someone copied from a page")]
    public void Other_copied_text_is_left_alone(string? copied) => Assert.Null(KeySetup.Recognize(copied));

    [Fact]
    public void A_free_gemini_key_alone_is_enough_to_start()
    {
        Assert.True(KeySetup.CanStart(false, false, true));
        Assert.True(KeySetup.CanStart(true, true, false));
        Assert.False(KeySetup.CanStart(true, false, false));
        Assert.False(KeySetup.CanStart(false, true, false));
        Assert.Equal(KeyKind.Gemini, KeySetup.NextNeeded(false, false, false));
    }

    [Fact]
    public void The_cost_estimate_grows_with_use_and_the_free_voice_is_cheaper()
    {
        MonthlyCost light = CostGuide.Estimate(20);
        MonthlyCost heavy = CostGuide.Estimate(150);
        Assert.True(heavy.Total > light.Total * 5);
        Assert.True(light.WithFreeVoice < light.Total);
        Assert.InRange(light.Total, 2m, 8m);
    }

    [Fact]
    public void A_person_can_choose_their_ai_chat_at_the_start()
    {
        OnboardingFlow flow = Run(false, "arabic", "low vision", "books", "skip", "لأ", "gemini", "skip");
        Assert.Null(flow.Current);
        Assert.Equal(Mubsir.Core.Chat.ChatSite.Gemini, flow.Plan().ChatSite);
    }

    [Theory]
    [InlineData(ToolNames.MoveFile)]
    [InlineData(ToolNames.RenameFile)]
    [InlineData(ToolNames.CopyFile)]
    public void After_reading_a_folder_moving_files_needs_a_yes(string tool)
    {
        Assert.True(PolicyEngine.MarksUntrusted(ToolNames.ListFolder, null));
        Assert.True(PolicyEngine.MarksUntrusted(ToolNames.FindFiles, null));
        var task = new TaskContext { UserUtterance = "tidy up", UntrustedContentSeen = true };
        PolicyDecision decision = new PolicyEngine().Evaluate(new AgentAction { Tool = tool, Args = new Dictionary<string, string?>() }, task);
        Assert.NotEqual(ActionTier.Allow, decision.Tier);
    }

    [Theory]
    [InlineData("Password", true)]
    [InlineData("Show password field", true)]
    [InlineData("كلمة السر", true)]
    [InlineData("كلمة المرور", true)]
    [InlineData("Search", false)]
    [InlineData(null, false)]
    public void A_field_that_looks_like_a_password_is_known(string? name, bool expected) =>
        Assert.Equal(expected, SensitiveDetector.NameLooksSecret(name));

    [Fact]
    public void Chats_are_saved_by_day_with_secrets_hidden()
    {
        string folder = Path.Combine(Path.GetTempPath(), "mubsir-chats-" + Guid.NewGuid().ToString("N"));
        try
        {
            var when = new DateTimeOffset(2026, 11, 5, 14, 30, 0, TimeSpan.Zero);
            var archive = new SessionArchive(folder, () => when);
            Assert.True(archive.Add("You", "my card is 4111 1111 1111 1111"));
            Assert.True(archive.Add("Mubsir", "شكرا\nتم"));
            string text = File.ReadAllText(archive.FileFor(when));
            Assert.DoesNotContain("4111", text, StringComparison.Ordinal);
            Assert.Contains("[card]", text, StringComparison.Ordinal);
            Assert.Contains("شكرا تم", text, StringComparison.Ordinal);
            Assert.Single(archive.RecentFiles(5));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void A_folder_that_cant_be_written_never_breaks_anything() =>
        Assert.False(new SessionArchive("/dev/null/nope", () => DateTimeOffset.UnixEpoch).Add("You", "hello"));
}
