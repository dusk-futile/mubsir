using System.Runtime.CompilerServices;
using System.Text.Json;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Tests.Agent;

/// <summary>
/// Whole tasks through the real agent loop and policy, with a scripted brain, judged only by what a person who
/// can't see the screen would hear. A task that ends without a word is a failure, whatever went wrong inside.
/// </summary>
public sealed class BlindListenerTests
{
    private static BrainEvent Call(string tool, string id = "c") => new ToolCallRequested(new ToolCall(id, tool, JsonDocument.Parse("{}").RootElement));

    /// <summary>Plays one scripted turn per request, then repeats the last one for ever.</summary>
    private sealed class Brain(params BrainEvent[][] turns) : IBrain, IBrainSession
    {
        private int _turn;

        public IBrainSession StartSession(BrainSessionOptions options) => this;

        public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            foreach (BrainEvent e in turns[Math.Min(_turn++, turns.Length - 1)])
            {
                yield return e;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Executor : IToolExecutor
    {
        public Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct) =>
            Task.FromResult(new PreparedAction(call, new AgentAction { Tool = call.Name }, null));

        public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct) =>
            Task.FromResult(new ToolResult(action.Call.Id, action.Call.Name == "wait" ? "error: timed out" : "ok", IsError: action.Call.Name == "wait"));

        public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct) => ExecuteAsync(action, ct);

        public string Narration(PreparedAction action, Lang lang) => "Doing " + action.Call.Name;
    }

    private sealed class Confirmer : IConfirmer
    {
        public Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct) => Task.FromResult(ConfirmOutcome.Rejected);
    }

    private sealed class Ears : INarrator
    {
        public List<string> Heard { get; } = [];

        public void Narrate(string text, NarrationKind kind) => Heard.Add(kind + ": " + text);

        public void StepFinished(bool ok) => Heard.Add(ok ? "done" : "failed");

        public void AnswerDelta(string text) => Heard.Add("answer: " + text);

        public void AnswerEnd()
        {
        }

        public void Play(Earcon earcon)
        {
        }
    }

    private sealed class Usage : IUsageSink
    {
        public void Report(UsageRecord usage)
        {
        }
    }

    private sealed class Clock : IClock
    {
        public long NowMs => 0;

        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private static async Task<(AgentOutcome Outcome, List<string> Heard)> Listen(params BrainEvent[][] turns)
    {
        var ears = new Ears();
        var loop = new AgentLoop(new Brain(turns), new Executor(), new PolicyEngine(), new Confirmer(), ears, new Usage(), new Clock());
        var options = new BrainSessionOptions(BrainMode.Agent, "prompt", "", [], false, Lang.English);
        AgentOutcome outcome = await loop.RunAsync(new AgentRequest("do it", null, null, new TaskContext(), options, MaxSteps: 4), new AgentControl(), CancellationToken.None);
        return (outcome, ears.Heard);
    }

    [Fact]
    public async Task A_task_that_works_is_heard_step_by_step_and_ends_with_an_answer()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("read_screen")], [new TextDelta("Your inbox has 2 new messages.")]);

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(["Step: Doing read_screen", "done", "answer: Your inbox has 2 new messages."], heard);
    }

    [Fact]
    public async Task A_step_that_fails_is_heard_as_failed_and_the_task_still_ends_with_words()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("wait")], [new TextDelta("The page didn't load.")]);

        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(["Step: Doing wait", "failed", "answer: The page didn't load."], heard);
    }

    [Fact]
    public async Task A_task_that_never_finishes_says_it_stopped()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("read_screen")]);

        Assert.Equal(AgentEnd.StepLimit, outcome.End);
        Assert.StartsWith("Warning: ", heard[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tool_that_does_not_exist_is_refused_out_loud_and_never_run()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("format_disk")], [new TextDelta("I can't do that.")]);

        Assert.DoesNotContain(heard, h => h.Contains("format_disk", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.StartsWith("Warning: ", StringComparison.Ordinal));
        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal("answer: I can't do that.", heard[^1]);
    }

    [Fact]
    public async Task A_brain_that_keeps_asking_for_what_is_blocked_is_stopped_out_loud()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("format_disk")]);

        Assert.Equal(AgentEnd.PolicyStopped, outcome.End);
        Assert.StartsWith("Warning: ", heard[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_brain_that_fails_reports_why_so_the_app_can_say_it()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("read_screen")], [new BrainFailed(BrainErrorKind.Network, "offline")]);

        Assert.Equal(AgentEnd.BrainError, outcome.End);
        Assert.Equal(BrainErrorKind.Network, outcome.Error);
        Assert.Equal(["Step: Doing read_screen", "done"], heard);
    }

    [Fact]
    public async Task A_brain_that_goes_quiet_after_acting_leaves_no_answer_so_the_app_says_done()
    {
        (AgentOutcome outcome, List<string> heard) = await Listen([Call("read_screen")], []);

        // TaskRunner.Report says "Done" for exactly this: finished, steps taken, no final words.
        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.True(outcome.Steps > 0);
        Assert.True(string.IsNullOrWhiteSpace(outcome.FinalText));
        Assert.Equal("done", heard[^1]);
    }
}
