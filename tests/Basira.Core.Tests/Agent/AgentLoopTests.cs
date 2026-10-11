using System.Runtime.CompilerServices;
using System.Text.Json;
using Basira.Core.Agent;
using Basira.Core.Brain;
using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Policy;

namespace Basira.Core.Tests.Agent;

public class AgentLoopTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly ScreenContext Chrome = new()
    {
        AppName = "Google Chrome", ProcessName = "chrome.exe", WindowTitle = "News", Domain = "news.example",
        Url = "https://news.example/", IsBrowser = true,
    };

    private static readonly ScreenContext WhatsApp = new() { AppName = "WhatsApp", ProcessName = "WhatsApp.exe", WindowTitle = "WhatsApp" };

    private static readonly ScreenContext BankPage = new()
    {
        AppName = "Google Chrome", ProcessName = "chrome.exe", WindowTitle = "CIB Online", Domain = "online.cib.com.eg",
        Url = "https://online.cib.com.eg/pay", IsBrowser = true,
    };

    private static UiNode Button(string name, string role = "button") => new() { Id = 5, Role = role, Name = name };

    private static ToolCall Call(string id, string name, string json = "{}")
    {
        using var doc = JsonDocument.Parse(json);
        return new ToolCall(id, name, doc.RootElement.Clone());
    }

    private static ToolCallRequested Req(string id, string name, string json = "{}") => new(Call(id, name, json));

    private static TurnCompleted End(string reason = "end_turn") => new(reason);

    private static UsageReported Usage(int input, int output) =>
        new(new UsageRecord(DateTimeOffset.UnixEpoch, "test-model", input, 0, 0, output));

    /// <summary>Wires an agent loop to fakes.</summary>
    private sealed class Harness
    {
        public Harness(Lang lang = Lang.Arabic, string utterance = "اقرأ الشاشة", bool privateMode = false, bool sensitive = false)
        {
            Executor = new FakeExecutor(Log);
            Confirmer = new FakeConfirmer(Log);
            Narrator = new FakeNarrator(Log);
            Task = new TaskContext
            {
                UserUtterance = utterance,
                Lang = lang,
                PrivateMode = privateMode,
                SensitiveMode = sensitive,
                LibraryRoots = [@"C:\Users\Omar\Documents"],
                BankDomains = ["cib.com.eg"],
            };
        }

        public List<string> Log { get; } = [];

        public ScriptedBrain Brain { get; } = new();

        public FakeExecutor Executor { get; }

        public FakeConfirmer Confirmer { get; }

        public FakeNarrator Narrator { get; }

        public FakeUsage Usage { get; } = new();

        public ManualClock Clock { get; } = new();

        public AgentControl Control { get; } = new();

        public TaskContext Task { get; }

        public AgentLoop Loop => new(Brain, Executor, new PolicyEngine(), Confirmer, Narrator, Usage, Clock);

        public AgentRequest Request(BrainMode mode = BrainMode.Agent, int maxSteps = 15, int maxSeconds = 180, string? context = "App: Calculator", byte[]? image = null) =>
            new(Task.UserUtterance, context, image, Task,
                new BrainSessionOptions(mode, "system prompt", "profile block", ToolCatalog.All, AllowWebSearch: false, Task.Lang),
                maxSteps, maxSeconds);

        public Task<AgentOutcome> RunAsync(AgentRequest? request = null, CancellationToken ct = default) =>
            Loop.RunAsync(request ?? Request(), Control, ct).WaitAsync(Timeout);

        public ToolResults ResultsSent(int input) => Assert.IsType<ToolResults>(Brain.Inputs[input]);
    }

    // ------------------------------------------------------------------------------------- answers

    [Fact]
    public async Task PlainAnswerStreamsAndCompletes()
    {
        var h = new Harness();
        h.Brain.Turn(new TextDelta("الشاشة فيها "), new TextDelta("آلة حاسبة."), Usage(1200, 30), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(0, outcome.Steps);
        Assert.Null(outcome.Error);
        Assert.Equal("الشاشة فيها آلة حاسبة.", outcome.FinalText);
        Assert.Equal("الشاشة فيها آلة حاسبة.", h.Narrator.Answer.ToString());
        Assert.Equal(1, h.Narrator.AnswerEnds);
        Assert.Single(h.Usage.Records);
        Assert.Equal(1200, h.Usage.Records[0].InputTokens);
        Assert.Empty(h.Executor.Prepared);
        Assert.Equal(1, h.Brain.Disposed);
    }

    [Fact]
    public async Task TheFirstInputCarriesTheRequestContextAndImage()
    {
        var h = new Harness(utterance: "what is this?");
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF };
        h.Brain.Turn(new TextDelta("A calculator."), End());

        await h.RunAsync(h.Request(context: "App: Calculator", image: jpeg));

        var turn = Assert.IsType<UserTurn>(Assert.Single(h.Brain.Inputs));
        Assert.Equal("what is this?", turn.Text);
        Assert.Equal("App: Calculator", turn.ScreenContextText);
        Assert.Same(jpeg, turn.JpegImage);
        var options = Assert.Single(h.Brain.Started);
        Assert.Equal(BrainMode.Agent, options.Mode);
        Assert.Equal(ToolCatalog.All.Count, options.Tools.Count);
        Assert.Equal("system prompt", options.SystemPrompt);
    }

    [Fact]
    public async Task QuickAnswerSendsNoToolsAndTakesOneTurn()
    {
        var h = new Harness();
        h.Brain.Turn(new TextDelta("Hi"), Req("c1", ToolNames.ReadScreen), End("tool_use"));

        var outcome = await h.RunAsync(h.Request(BrainMode.QuickAnswer));

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal("Hi", outcome.FinalText);
        Assert.Empty(Assert.Single(h.Brain.Started).Tools);
        Assert.Single(h.Brain.Inputs);
        Assert.Empty(h.Executor.Prepared);
    }

    [Fact]
    public async Task UsageIsReportedFromEveryTurn()
    {
        var h = new Harness();
        h.Brain.Turn(Usage(1000, 10), Req("c1", ToolNames.SystemStatus, """{"what":"battery"}"""), End("tool_use"))
               .Turn(Usage(1100, 20), new TextDelta("Battery 80%"), End());

        await h.RunAsync();

        Assert.Equal([1000L, 1100L], h.Usage.Records.Select(r => r.InputTokens));
    }

    // ------------------------------------------------------------------------------------- tools

    [Fact]
    public async Task MultiStepToolUseReturnsEachResultToTheModel()
    {
        var h = new Harness();
        h.Executor.Resolve = (call, a) => call.Name == ToolNames.Invoke ? a with { Target = Button("Equals") } : a;
        h.Brain.Turn(new TextDelta("أقرأ الشاشة."), Req("c1", ToolNames.ReadScreen, """{"scope":"window"}"""), End("tool_use"))
               .Turn(Req("c2", ToolNames.Invoke, """{"id":5}"""), End("tool_use"))
               .Turn(new TextDelta("النتيجة ٤."), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(2, outcome.Steps);
        Assert.Equal("النتيجة ٤.", outcome.FinalText);
        Assert.Equal(3, h.Brain.Inputs.Count);
        Assert.Equal(new ToolResult("c1", "ok read_screen"), Assert.Single(h.ResultsSent(1).Results));
        Assert.Equal(new ToolResult("c2", "ok invoke"), Assert.Single(h.ResultsSent(2).Results));
        Assert.Equal(["step read_screen", "step invoke"], h.Narrator.Steps);
        Assert.Equal(2, h.Narrator.AnswerEnds);
    }

    [Fact]
    public async Task AllowedStepsAreNarratedBeforeTheyRun()
    {
        var h = new Harness();
        h.Brain.Turn(Req("c1", ToolNames.Scroll, """{"direction":"down","amount":"page","id":null}"""), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Equal(["prepare c1", "narrate Step: step scroll", "execute c1"], h.Log);
        Assert.Equal("down", h.Executor.Executed[0].Action.Args["direction"]);
    }

    [Fact]
    public async Task SeveralCallsInOneTurnRunInOrderAndReturnTogether()
    {
        var h = new Harness();
        h.Brain.Turn(Req("a", ToolNames.Scroll), Req("b", ToolNames.ReadScreen), Req("c", ToolNames.SystemStatus), End("tool_use"))
               .Turn(new TextDelta("done"), End());

        var outcome = await h.RunAsync();

        Assert.Equal(1, outcome.Steps);
        Assert.Equal(["a", "b", "c"], h.Executor.Executed.Select(e => e.Call.Id));
        var results = h.ResultsSent(1).Results;
        Assert.Equal(["a", "b", "c"], results.Select(r => r.ToolCallId));
        Assert.Equal(["ok scroll", "ok read_screen", "ok system_status"], results.Select(r => r.Content));
        Assert.Equal(2, h.Brain.Inputs.Count);
    }

    [Fact]
    public async Task MixedDecisionsInOneTurnStillReturnEveryResult()
    {
        var h = new Harness();
        h.Executor.Resolve = (call, a) => call.Name == ToolNames.Invoke ? a with { Screen = WhatsApp, Target = Button("Send") } : a;
        h.Confirmer.Outcomes.Enqueue(ConfirmOutcome.Rejected);
        h.Brain.Turn(Req("a", ToolNames.Scroll), Req("b", ToolNames.OpenUrl, """{"url":"javascript:alert(1)"}"""), Req("c", ToolNames.Invoke), End("tool_use"))
               .Turn(End());

        await h.RunAsync();

        var results = h.ResultsSent(1).Results;
        Assert.Equal(["a", "b", "c"], results.Select(r => r.ToolCallId));
        Assert.False(results[0].IsError);
        Assert.Equal("blocked: blocked.url_scheme", results[1].Content);
        Assert.Equal(AgentLoop.DeclinedResult, results[2].Content);
        Assert.Equal(["a"], h.Executor.Executed.Select(e => e.Call.Id));
    }

    // ------------------------------------------------------------------------------------- confirmations

    private static Harness SendHarness(params ConfirmOutcome[] outcomes)
    {
        var h = new Harness(utterance: "رد على أحمد وقول له أنا في الطريق");
        h.Executor.Resolve = (call, a) => call.Name == ToolNames.Invoke
            ? a with
            {
                Screen = WhatsApp,
                Target = Button("إرسال"),
                Args = new Dictionary<string, string?> { ["id"] = "5", ["recipient"] = "أحمد", ["body"] = "أنا في الطريق" },
            }
            : a;
        foreach (var o in outcomes)
        {
            h.Confirmer.Outcomes.Enqueue(o);
        }

        return h;
    }

    [Fact]
    public async Task AcceptedConfirmationRunsTheAction()
    {
        var h = SendHarness(ConfirmOutcome.Accepted);
        h.Brain.Turn(new TextDelta("Sending to Mom now."), Req("c1", ToolNames.Invoke, """{"id":5}"""), End("tool_use"))
               .Turn(new TextDelta("أُرسلت."), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        var prompt = Assert.Single(h.Confirmer.Prompts);
        var expected = ConfirmationBuilder.Build(new ConfirmationFacts
        {
            Kind = ConfirmKind.Send, ButtonName = "إرسال", Recipient = "أحمد", BodyPreview = "أنا في الطريق", ToolName = ToolNames.Invoke,
        }, Lang.Arabic);
        Assert.Equal(expected, prompt);
        Assert.DoesNotContain("Mom", prompt); // built from facts, never from the model's words
        Assert.Contains(Earcon.NeedsConfirmation, h.Narrator.Earcons);
        Assert.True(h.Log.IndexOf("earcon NeedsConfirmation") < h.Log.IndexOf("confirm"));
        Assert.Single(h.Executor.Executed);
        Assert.Equal(new ToolResult("c1", "ok invoke"), Assert.Single(h.ResultsSent(1).Results));
        Assert.Empty(h.Narrator.Steps); // the confirmation replaces the step narration
    }

    [Theory]
    [InlineData(ConfirmOutcome.Rejected)]
    [InlineData(ConfirmOutcome.TimedOut)]
    public async Task DeclinedConfirmationDoesNotRunTheAction(ConfirmOutcome answer)
    {
        var h = SendHarness(answer);
        h.Brain.Turn(Req("c1", ToolNames.Invoke, """{"id":5}"""), End("tool_use")).Turn(new TextDelta("لم أرسل."), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Empty(h.Executor.Executed);
        var result = Assert.Single(h.ResultsSent(1).Results);
        Assert.Equal(AgentLoop.DeclinedResult, result.Content);
        Assert.True(result.IsError);
        Assert.Contains((AgentText.Declined(Lang.Arabic), NarrationKind.Status), h.Narrator.Narrations);
    }

    [Fact]
    public async Task ADeclinedActionIsNotAskedAgainInTheSameTask()
    {
        var h = SendHarness(ConfirmOutcome.Rejected, ConfirmOutcome.Accepted);
        h.Brain.Turn(Req("c1", ToolNames.Invoke, """{"id":5}"""), End("tool_use"))
               .Turn(Req("c2", ToolNames.Invoke, """{"id":5}"""), End("tool_use"))
               .Turn(End());

        await h.RunAsync();

        Assert.Single(h.Confirmer.Prompts);
        Assert.Empty(h.Executor.Executed);
        Assert.Equal(AgentLoop.DeclinedResult, Assert.Single(h.ResultsSent(2).Results).Content);
    }

    [Fact]
    public async Task ADifferentActionIsStillAskedAfterADecline()
    {
        var h = new Harness();
        h.Executor.Resolve = (call, a) => a with { Screen = WhatsApp, Target = Button(call.Id == "c1" ? "Send" : "Delete") };
        h.Confirmer.Outcomes.Enqueue(ConfirmOutcome.Rejected);
        h.Confirmer.Outcomes.Enqueue(ConfirmOutcome.Accepted);
        h.Brain.Turn(Req("c1", ToolNames.Invoke), End("tool_use")).Turn(Req("c2", ToolNames.Invoke), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Equal(2, h.Confirmer.Prompts.Count);
        Assert.Equal(["c2"], h.Executor.Executed.Select(e => e.Call.Id));
    }

    [Fact]
    public async Task AFailingConfirmerCountsAsNo()
    {
        var h = SendHarness();
        h.Confirmer.Custom = (_, _) => throw new InvalidOperationException("audio device lost");
        h.Brain.Turn(Req("c1", ToolNames.Invoke), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Empty(h.Executor.Executed);
        Assert.Equal(AgentLoop.DeclinedResult, Assert.Single(h.ResultsSent(1).Results).Content);
    }

    [Fact]
    public async Task EnglishTasksGetEnglishSentences()
    {
        var h = new Harness(Lang.English, "read my email");
        h.Executor.Resolve = (_, a) => a with { Screen = WhatsApp, Target = Button("Send") };
        h.Brain.Turn(Req("c1", ToolNames.Invoke), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.EndsWith(ConfirmationBuilder.EnglishKeysHint, Assert.Single(h.Confirmer.Prompts));
        Assert.Contains((AgentText.Declined(Lang.English), NarrationKind.Status), h.Narrator.Narrations);
    }

    // ------------------------------------------------------------------------------------- blocks

    [Fact]
    public async Task BlockedActionsAreRefusedAndExplained()
    {
        var h = new Harness();
        h.Brain.Turn(Req("c1", ToolNames.OpenUrl, """{"url":"javascript:alert(1)"}"""), End("tool_use"))
               .Turn(new TextDelta("لا أستطيع."), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        var result = Assert.Single(h.ResultsSent(1).Results);
        Assert.Equal("blocked: " + ReasonCodes.UrlScheme, result.Content);
        Assert.True(result.IsError);
        Assert.Equal([AgentText.Blocked(ReasonCodes.UrlScheme, Lang.Arabic)], h.Narrator.Warnings);
        Assert.Empty(h.Executor.Executed);
        Assert.Empty(h.Confirmer.Prompts);
    }

    [Fact]
    public async Task RepeatedBlocksStopTheTask()
    {
        var h = new Harness();
        h.Brain.Forever = n => [Req("c" + n, ToolNames.TypeText, """{"text":"x"}"""), End("tool_use")];
        h.Executor.Resolve = (_, a) => a with { Screen = new ScreenContext { ProcessName = "cmd.exe" } };

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Equal(AgentLoop.MaxBlocksPerTask, outcome.Steps);
        Assert.Equal(AgentText.PolicyStopped(Lang.Arabic), h.Narrator.Warnings.Last());
        Assert.Empty(h.Executor.Executed);
    }

    [Fact]
    public async Task AnAdministratorWindowStopsTheTaskAtOnce()
    {
        var h = new Harness();
        h.Executor.Resolve = (_, a) => a with { Screen = new ScreenContext { ProcessName = "mmc.exe", IsElevated = true }, Target = Button("OK") };
        h.Brain.Turn(Req("c1", ToolNames.Invoke), Req("c2", ToolNames.Invoke), End("tool_use"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Equal(["c1"], h.Executor.Prepared.Select(c => c.Id));
        Assert.Contains(AgentText.Blocked(ReasonCodes.Elevated, Lang.Arabic), h.Narrator.Warnings);
        Assert.Single(h.Brain.Inputs);
    }

    [Fact]
    public async Task APolicyFailureFailsClosed()
    {
        var h = new Harness();
        h.Executor.Resolve = (_, a) => a with { Args = null! }; // the policy cannot read the arguments
        h.Brain.Turn(Req("c1", ToolNames.OpenUrl), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Empty(h.Executor.Executed);
        Assert.Equal(AgentLoop.BlockedPrefix + AgentLoop.PolicyErrorReason, Assert.Single(h.ResultsSent(1).Results).Content);
        Assert.Equal([AgentText.Blocked(AgentLoop.PolicyErrorReason, Lang.Arabic)], h.Narrator.Warnings);
    }

    [Fact]
    public async Task AMissingPreparedActionIsAnError()
    {
        var h = new Harness();
        h.Executor.Resolve = (_, _) => null!;
        h.Brain.Turn(Req("c1", ToolNames.Invoke), End("tool_use")).Turn(End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Empty(h.Executor.Executed);
        Assert.Equal(new ToolResult("c1", AgentLoop.PrepareFailedResult, IsError: true), Assert.Single(h.ResultsSent(1).Results));
    }

    // ------------------------------------------------------------------------------------- bank and private mode

    [Fact]
    public async Task BankPayButtonIsHandedToTheUser()
    {
        var h = new Harness(utterance: "ادفع فاتورة فودافون");
        h.Executor.Resolve = (_, a) => a with { Screen = BankPage, Target = Button("Pay") };
        h.Brain.Turn(Req("c1", ToolNames.Invoke, """{"id":5}"""), End("tool_use"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Single(h.Executor.HandedOff);
        Assert.Empty(h.Executor.Executed);
        Assert.Empty(h.Confirmer.Prompts);
        Assert.Contains(Earcon.BankMode, h.Narrator.Earcons);
        Assert.Single(h.Brain.Inputs); // nothing from the bank page goes back to the cloud
        Assert.Equal(1, h.Brain.Disposed);
    }

    [Fact]
    public async Task ReachingABankPageMidTaskStopsBeforeAnythingIsSent()
    {
        var h = new Harness(utterance: "افتح موقع البنك");
        h.Executor.Resolve = (call, a) => call.Name == ToolNames.ReadScreen ? a with { Screen = BankPage } : a;
        h.Brain.Turn(Req("c1", ToolNames.OpenUrl, """{"url":"https://online.cib.com.eg"}"""), End("tool_use"))
               .Turn(Req("c2", ToolNames.ReadScreen), End("tool_use"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Equal(2, h.Brain.Inputs.Count); // the bank page's text was read locally but never sent
        Assert.Equal(AgentText.BankStop(Lang.Arabic), h.Narrator.Warnings.Last());
    }

    [Fact]
    public async Task ExecutorSwitchingOnSensitiveModeStopsTheTask()
    {
        var h = new Harness();
        h.Executor.Handlers[ToolNames.ReadScreen] = a =>
        {
            h.Task.SensitiveMode = true;
            return new ToolResult(a.Call.Id, "card form");
        };
        h.Brain.Turn(Req("c1", ToolNames.ReadScreen), End("tool_use"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Single(h.Brain.Inputs);
    }

    [Fact]
    public async Task PrivateModeNeverStartsASession()
    {
        var h = new Harness(privateMode: true);
        h.Brain.Turn(new TextDelta("x"), End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Empty(h.Brain.Started);
        Assert.Equal([AgentText.PrivateStop(Lang.Arabic)], h.Narrator.Warnings);
    }

    [Fact]
    public async Task BankModeNeverStartsASession()
    {
        var h = new Harness(Lang.English, sensitive: true);

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.Empty(h.Brain.Started);
        Assert.Equal([AgentText.BankStop(Lang.English)], h.Narrator.Warnings);
    }

    // ------------------------------------------------------------------------------------- untrusted content

    [Fact]
    public async Task ReadingAWebPageMakesUnnamedSitesNeedConfirmation()
    {
        var h = new Harness(Lang.English, "read me the news");
        h.Executor.Resolve = (call, a) => call.Name == ToolNames.ReadScreen ? a with { Screen = Chrome } : a;
        h.Brain.Turn(Req("c1", ToolNames.OpenUrl, """{"url":"https://first.example"}"""), End("tool_use"))
               .Turn(Req("c2", ToolNames.ReadScreen, """{"scope":"document"}"""), End("tool_use"))
               .Turn(Req("c3", ToolNames.OpenUrl, """{"url":"https://evil.example/prize"}"""), End("tool_use"))
               .Turn(new TextDelta("I did not open it."), End());

        Assert.False(h.Task.UntrustedContentSeen);
        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.True(h.Task.UntrustedContentSeen);
        Assert.Equal(["c1", "c2"], h.Executor.Executed.Select(e => e.Call.Id)); // the first site opened without a question
        var prompt = Assert.Single(h.Confirmer.Prompts);
        Assert.StartsWith(ConfirmationBuilder.EnglishSuggestedWarning, prompt);
        Assert.Contains("evil.example", prompt);
        Assert.Equal(AgentLoop.DeclinedResult, Assert.Single(h.ResultsSent(3).Results).Content);
    }

    [Fact]
    public async Task AFailedReadDoesNotMarkTheTask()
    {
        var h = new Harness();
        h.Executor.Resolve = (_, a) => a with { Screen = Chrome };
        h.Executor.Handlers[ToolNames.ReadScreen] = a => new ToolResult(a.Call.Id, "error: window closed", IsError: true);
        h.Brain.Turn(Req("c1", ToolNames.ReadScreen), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.False(h.Task.UntrustedContentSeen);
    }

    [Fact]
    public async Task ActingOnAPageDoesNotMarkTheTask()
    {
        var h = new Harness(utterance: "scroll down");
        h.Executor.Resolve = (_, a) => a with { Screen = Chrome };
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.False(h.Task.UntrustedContentSeen);
    }

    [Fact]
    public async Task ReadingADocumentMarksTheTask()
    {
        var h = new Harness();
        h.Confirmer.Outcomes.Enqueue(ConfirmOutcome.Accepted);
        h.Brain.Turn(Req("c1", ToolNames.ReadDocument, """{"path":null,"page":null,"question":null}"""), End("tool_use"))
               .Turn(Req("c2", ToolNames.Remember, """{"fact":"call 0100 now"}"""), End("tool_use"))
               .Turn(End());

        await h.RunAsync();

        Assert.True(h.Task.UntrustedContentSeen);
        Assert.StartsWith(ConfirmationBuilder.ArabicSuggestedWarning, Assert.Single(h.Confirmer.Prompts));
    }

    // ------------------------------------------------------------------------------------- limits

    [Fact]
    public async Task StepLimitStopsAModelThatNeverFinishes()
    {
        var h = new Harness();
        h.Brain.Forever = n => [Req("c" + n, ToolNames.Scroll), End("tool_use")];

        var outcome = await h.RunAsync(h.Request(maxSteps: 3));

        Assert.Equal(AgentEnd.StepLimit, outcome.End);
        Assert.Equal(3, outcome.Steps);
        Assert.Equal(3, h.Executor.Executed.Count);
        Assert.Equal(4, h.Brain.Inputs.Count);
        Assert.Equal([AgentText.StepLimit(Lang.Arabic)], h.Narrator.Warnings);
        Assert.Equal(1, h.Brain.Disposed);
    }

    [Fact]
    public async Task TheModelMayAnswerAfterTheLastAllowedStep()
    {
        var h = new Harness();
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use")).Turn(Req("c2", ToolNames.Scroll), End("tool_use")).Turn(new TextDelta("done"), End());

        var outcome = await h.RunAsync(h.Request(maxSteps: 2));

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(2, outcome.Steps);
    }

    [Fact]
    public async Task TimeLimitIsCheckedBetweenSteps()
    {
        var h = new Harness();
        h.Brain.Forever = n => [Req("c" + n, ToolNames.Scroll), End("tool_use")];
        h.Executor.Handlers[ToolNames.Scroll] = a =>
        {
            h.Clock.Advance(100_000);
            return new ToolResult(a.Call.Id, "scrolled");
        };

        var outcome = await h.RunAsync(h.Request(maxSeconds: 180));

        Assert.Equal(AgentEnd.TimeLimit, outcome.End);
        Assert.Equal(2, outcome.Steps);
        Assert.Equal([AgentText.TimeLimit(Lang.Arabic)], h.Narrator.Warnings);
    }

    [Fact]
    public async Task PausedTimeDoesNotCountTowardTheTimeLimit()
    {
        var h = new Harness();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Executor.Handlers[ToolNames.Scroll] = a =>
        {
            if (a.Call.Id == "c1")
            {
                h.Control.Pause();
                paused.TrySetResult();
            }

            return new ToolResult(a.Call.Id, "scrolled");
        };
        h.Brain.Turn(Req("c1", ToolNames.Scroll), Req("c2", ToolNames.Scroll), End("tool_use"))
               .Turn(Req("c3", ToolNames.Scroll), End("tool_use"))
               .Turn(new TextDelta("done"), End());

        var run = h.RunAsync(h.Request(maxSeconds: 60));
        await paused.Task.WaitAsync(Timeout);
        await Task.Delay(50);
        Assert.Equal(["c1"], h.Executor.Prepared.Select(c => c.Id)); // waiting before the next call
        h.Clock.Advance(10 * 60 * 1000); // the user paused for ten minutes
        h.Control.Resume();

        var outcome = await run;
        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(2, outcome.Steps);
    }

    // ------------------------------------------------------------------------------------- pause and cancel

    [Fact]
    public async Task APausedTaskWaitsForResume()
    {
        var h = new Harness();
        h.Brain.Turn(new TextDelta("ok"), End());
        h.Control.Pause();

        var run = h.RunAsync();
        await Task.Delay(50);

        Assert.False(run.IsCompleted);
        Assert.Empty(h.Brain.Started);
        h.Control.Resume();
        Assert.Equal(AgentEnd.Completed, (await run).End);
    }

    [Fact]
    public async Task CancellationMidStreamEndsPromptly()
    {
        var h = new Harness();
        using var cts = new CancellationTokenSource();
        h.Narrator.OnDelta = _ => cts.Cancel();
        h.Brain.Turn((_, ct) => Hang(new TextDelta("جارٍ"), ct));

        var outcome = await h.RunAsync(ct: cts.Token);

        Assert.Equal(AgentEnd.Cancelled, outcome.End);
        Assert.Empty(h.Executor.Prepared);
        Assert.Equal(1, h.Brain.Disposed);
    }

    [Fact]
    public async Task ASessionThatEndsQuietlyOnCancelIsStillCancelled()
    {
        var h = new Harness();
        using var cts = new CancellationTokenSource();
        h.Narrator.OnDelta = _ => cts.Cancel();
        h.Brain.Turn(new TextDelta("x"), Req("c1", ToolNames.Scroll)); // no exception, the stream just ends

        var outcome = await h.RunAsync(ct: cts.Token);

        Assert.Equal(AgentEnd.Cancelled, outcome.End);
        Assert.Empty(h.Executor.Prepared);
    }

    [Fact]
    public async Task CancellationDuringAConfirmationRunsNothing()
    {
        var h = SendHarness();
        using var cts = new CancellationTokenSource();
        h.Confirmer.Custom = async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return ConfirmOutcome.Accepted;
        };
        h.Brain.Turn(Req("c1", ToolNames.Invoke), End("tool_use"));

        var outcome = await h.RunAsync(ct: cts.Token);

        Assert.Equal(AgentEnd.Cancelled, outcome.End);
        Assert.Empty(h.Executor.Executed);
    }

    [Fact]
    public async Task CancellationWhilePausedEndsTheTask()
    {
        var h = new Harness();
        h.Brain.Turn(new TextDelta("x"), End());
        h.Control.Pause();
        using var cts = new CancellationTokenSource();

        var run = h.RunAsync(ct: cts.Token);
        await Task.Delay(20);
        await cts.CancelAsync();

        Assert.Equal(AgentEnd.Cancelled, (await run).End);
        Assert.Empty(h.Brain.Started);
    }

    [Fact]
    public async Task AlreadyCancelledTokenRunsNothing()
    {
        var h = new Harness();
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use"));

        var outcome = await h.RunAsync(ct: new CancellationToken(canceled: true));

        Assert.Equal(AgentEnd.Cancelled, outcome.End);
        Assert.Empty(h.Executor.Prepared);
    }

    private static async IAsyncEnumerable<BrainEvent> Hang(BrainEvent first, [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return first;
        await Task.Delay(System.Threading.Timeout.Infinite, ct);
        yield return End();
    }

    // ------------------------------------------------------------------------------------- brain errors

    [Theory]
    [InlineData(BrainErrorKind.Network)]
    [InlineData(BrainErrorKind.RateLimited)]
    [InlineData(BrainErrorKind.Overloaded)]
    [InlineData(BrainErrorKind.Auth)]
    [InlineData(BrainErrorKind.NoApiKey)]
    [InlineData(BrainErrorKind.Refusal)]
    [InlineData(BrainErrorKind.BadRequest)]
    [InlineData(BrainErrorKind.Other)]
    public async Task BrainFailuresMapToTheirKind(BrainErrorKind kind)
    {
        var h = new Harness();
        h.Brain.Turn(new BrainFailed(kind, "code-1"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.BrainError, outcome.End);
        Assert.Equal(kind, outcome.Error);
        Assert.Equal(1, h.Brain.Disposed);
    }

    [Fact]
    public async Task AFailureAfterSomeStepsKeepsTheStepCountAndPartialText()
    {
        var h = new Harness();
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use"))
               .Turn(new TextDelta("نصف"), new BrainFailed(BrainErrorKind.Overloaded, "529"), new TextDelta("never"));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.BrainError, outcome.End);
        Assert.Equal(BrainErrorKind.Overloaded, outcome.Error);
        Assert.Equal(1, outcome.Steps);
        Assert.Equal("نصف", outcome.FinalText);
        Assert.Equal("نصف", h.Narrator.Answer.ToString());
        Assert.Equal(1, h.Narrator.AnswerEnds);
    }

    public static TheoryData<Exception, BrainErrorKind> StreamExceptions() => new()
    {
        { new HttpRequestException("no route"), BrainErrorKind.Network },
        { new IOException("reset"), BrainErrorKind.Network },
        { new TimeoutException(), BrainErrorKind.Network },
        { new OperationCanceledException("HTTP timeout"), BrainErrorKind.Network },
        { new InvalidOperationException("bug"), BrainErrorKind.Other },
    };

    [Theory]
    [MemberData(nameof(StreamExceptions))]
    public async Task StreamExceptionsAreMapped(Exception error, BrainErrorKind kind)
    {
        var h = new Harness();
        h.Brain.Turn((_, ct) => Throwing(error, ct));

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.BrainError, outcome.End);
        Assert.Equal(kind, outcome.Error);
    }

    [Fact]
    public async Task AFailedSessionStartIsABrainError()
    {
        var h = new Harness();
        h.Brain.StartError = new InvalidOperationException("no key");

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.BrainError, outcome.End);
        Assert.Equal(BrainErrorKind.Other, outcome.Error);
    }

    private static async IAsyncEnumerable<BrainEvent> Throwing(Exception error, [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new TextDelta("");
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        throw error;
    }

    // ------------------------------------------------------------------------------------- executor problems

    [Fact]
    public async Task ExecutorFailuresBecomeErrorResultsWithoutContent()
    {
        var h = new Harness();
        h.Executor.PrepareError = call => call.Id == "c1" ? new InvalidOperationException("element 99 not found") : null;
        h.Executor.Handlers[ToolNames.ListFolder] = _ => throw new UnauthorizedAccessException(@"C:\Users\Omar\secret");
        h.Executor.Resolve = (_, a) => a with { FilePaths = [@"C:\Users\Omar\Documents"] };
        h.Brain.Turn(Req("c1", ToolNames.Invoke), Req("c2", ToolNames.ListFolder), End("tool_use")).Turn(End());

        var outcome = await h.RunAsync();

        Assert.Equal(AgentEnd.Completed, outcome.End);
        var results = h.ResultsSent(1).Results;
        Assert.True(results[0].IsError);
        Assert.StartsWith("error:", results[0].Content);
        Assert.Equal(new ToolResult("c2", "error: UnauthorizedAccessException", IsError: true), results[1]);
        Assert.DoesNotContain("secret", string.Join(' ', results.Select(r => r.Content)));
    }

    [Fact]
    public async Task ResultsAreAlwaysTiedToTheirCall()
    {
        var h = new Harness();
        h.Executor.Handlers[ToolNames.Scroll] = _ => new ToolResult("wrong-id", "scrolled");
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Equal("c1", Assert.Single(h.ResultsSent(1).Results).ToolCallId);
    }

    [Fact]
    public async Task AFailingNarrationDoesNotStopTheStep()
    {
        var h = new Harness();
        h.Executor.NarrationText = _ => throw new InvalidOperationException();
        h.Brain.Turn(Req("c1", ToolNames.Scroll), End("tool_use")).Turn(End());

        await h.RunAsync();

        Assert.Single(h.Executor.Executed);
    }

    [Fact]
    public async Task RunRejectsNulls()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ArgumentNullException>(() => h.Loop.RunAsync(null!, h.Control, default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => h.Loop.RunAsync(h.Request(), null!, default));
    }

    // ------------------------------------------------------------------------------------- helpers

    [Fact]
    public void DeclineKeyIdentifiesToolAndTarget()
    {
        var call = Call("c1", ToolNames.Invoke);
        var send = new AgentAction { Tool = ToolNames.Invoke, Target = Button("Send") with { RuntimeId = "42.1" }, Args = new Dictionary<string, string?> { ["id"] = "5" } };
        var sameButtonNewId = send with { Args = new Dictionary<string, string?> { ["id"] = "9" } };
        var other = send with { Target = Button("Send") with { RuntimeId = "42.2" } };

        Assert.Equal(AgentLoop.DeclineKey(send, call), AgentLoop.DeclineKey(sameButtonNewId, call));
        Assert.NotEqual(AgentLoop.DeclineKey(send, call), AgentLoop.DeclineKey(other, call));

        var remember = new AgentAction { Tool = ToolNames.Remember, Args = new Dictionary<string, string?> { ["fact"] = "a" } };
        var rememberOther = remember with { Args = new Dictionary<string, string?> { ["fact"] = "b" } };
        Assert.NotEqual(AgentLoop.DeclineKey(remember, call), AgentLoop.DeclineKey(rememberOther, call));

        var enter = new AgentAction { Tool = ToolNames.PressKeys, Target = Button("Message", "edit"), KeyChord = "enter" };
        Assert.NotEqual(AgentLoop.DeclineKey(enter, call), AgentLoop.DeclineKey(enter with { KeyChord = "ctrl+enter" }, call));
    }

    [Fact]
    public async Task ControlPausesAndResumes()
    {
        var control = new AgentControl();
        Assert.False(control.IsPaused);
        Assert.True(control.WaitIfPausedAsync(default).IsCompletedSuccessfully);

        control.Pause();
        control.Pause();
        Assert.True(control.IsPaused);
        var wait = control.WaitIfPausedAsync(default);
        Assert.False(wait.IsCompleted);

        control.Resume();
        await wait.WaitAsync(Timeout);
        Assert.False(control.IsPaused);
        control.Resume(); // resuming twice is harmless
    }

    [Fact]
    public async Task ControlWaitHonoursCancellation()
    {
        var control = new AgentControl();
        Assert.True(control.WaitIfPausedAsync(new CancellationToken(canceled: true)).IsCanceled);

        control.Pause();
        using var cts = new CancellationTokenSource();
        var wait = control.WaitIfPausedAsync(cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Timeout));
        Assert.True(control.IsPaused);
    }
}
