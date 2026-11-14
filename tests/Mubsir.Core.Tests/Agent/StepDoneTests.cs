using System.Runtime.CompilerServices;
using System.Text.Json;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Tests.Agent;

/// <summary>Someone who can't see the screen hears each step start and hears it finish.</summary>
public sealed class StepDoneTests
{
    private sealed class Session : IBrainSession
    {
        private int _turns;

        public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            if (_turns++ == 0)
            {
                yield return new ToolCallRequested(new ToolCall("call-1", "read_screen", JsonDocument.Parse("{}").RootElement));
            }
            else
            {
                yield return new TextDelta("All done.");
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Brain : IBrain
    {
        public IBrainSession StartSession(BrainSessionOptions options) => new Session();
    }

    private sealed class Executor(ToolResult result, string narration) : IToolExecutor
    {
        public Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct) =>
            Task.FromResult(new PreparedAction(call, new AgentAction { Tool = call.Name }, null));

        public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct) => Task.FromResult(result);

        public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct) => Task.FromResult(result);

        public string Narration(PreparedAction action, Lang lang) => narration;
    }

    private sealed class Confirmer : IConfirmer
    {
        public Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct) => Task.FromResult(ConfirmOutcome.Rejected);
    }

    private sealed class Narrator : INarrator
    {
        public List<string> Heard { get; } = [];

        public void Narrate(string text, NarrationKind kind) => Heard.Add("step: " + text);

        public void StepFinished(bool ok) => Heard.Add(ok ? "done" : "failed");

        public void AnswerDelta(string text)
        {
        }

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

    private static async Task<List<string>> Run(ToolResult result, string narration)
    {
        var narrator = new Narrator();
        var loop = new AgentLoop(new Brain(), new Executor(result, narration), new PolicyEngine(), new Confirmer(), narrator, new Usage(), new Clock());
        var options = new BrainSessionOptions(BrainMode.Agent, "prompt", "", [], false, Lang.English);
        await loop.RunAsync(new AgentRequest("read the screen", null, null, new TaskContext(), options), new AgentControl(), CancellationToken.None);
        return narrator.Heard;
    }

    [Fact]
    public async Task A_finished_step_is_announced_after_it_runs()
    {
        Assert.Equal(["step: Reading the screen", "done"], await Run(new ToolResult("call-1", "ok"), "Reading the screen"));
    }

    [Fact]
    public async Task A_failed_step_is_announced_as_failed()
    {
        Assert.Equal(["step: Reading the screen", "failed"], await Run(new ToolResult("call-1", "error: nothing there", IsError: true), "Reading the screen"));
    }

    [Fact]
    public async Task A_silent_step_stays_silent()
    {
        Assert.Empty(await Run(new ToolResult("call-1", "ok"), string.Empty));
    }

    [Fact]
    public void Done_is_short_in_both_languages()
    {
        Assert.Equal("Done.", AgentText.StepDone(Lang.English));
        Assert.Equal("تم.", AgentText.StepDone(Lang.Arabic));
    }
}
