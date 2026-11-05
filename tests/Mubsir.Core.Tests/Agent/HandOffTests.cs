using System.Runtime.CompilerServices;
using System.Text.Json;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Tests.Agent;

public class HandOffTests
{
    private sealed class Session : IBrainSession
    {
        public List<BrainInput> Inputs { get; } = [];

        public async IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, [EnumeratorCancellation] CancellationToken ct)
        {
            Inputs.Add(input);
            await Task.Yield();
            if (input is UserTurn)
            {
                yield return new ToolCallRequested(new ToolCall("call-1", "read_document", JsonDocument.Parse("{}").RootElement));
            }
            else
            {
                yield return new TextDelta("Here is the text of the book.");
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Brain(Session session) : IBrain
    {
        public IBrainSession StartSession(BrainSessionOptions options) => session;
    }

    private sealed class Executor(ToolResult result) : IToolExecutor
    {
        public Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct) =>
            Task.FromResult(new PreparedAction(call, new AgentAction { Tool = call.Name }, null));

        public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct) => Task.FromResult(result);

        public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct) => Task.FromResult(result);

        public string? Narration(PreparedAction action, Lang lang) => null;
    }

    private sealed class Confirmer : IConfirmer
    {
        public Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct) => Task.FromResult(ConfirmOutcome.Rejected);
    }

    private sealed class Narrator : INarrator
    {
        public List<string> Answers { get; } = [];

        public void Narrate(string text, NarrationKind kind)
        {
        }

        public void AnswerDelta(string text) => Answers.Add(text);

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

    private static async Task<(AgentOutcome Outcome, Session Session, Narrator Narrator)> Run(ToolResult result)
    {
        var session = new Session();
        var narrator = new Narrator();
        var loop = new AgentLoop(new Brain(session), new Executor(result), new PolicyEngine(), new Confirmer(), narrator, new Usage(), new Clock());
        var options = new BrainSessionOptions(BrainMode.Agent, "prompt", "", [], false, Lang.English);
        var request = new AgentRequest("read my book", null, null, new TaskContext(), options);
        return (await loop.RunAsync(request, new AgentControl(), CancellationToken.None), session, narrator);
    }

    [Fact]
    public async Task A_tool_that_ends_the_turn_means_the_ai_is_not_called_again()
    {
        var (outcome, session, narrator) = await Run(new ToolResult("call-1", "Reading aloud.", EndsTurn: true));
        Assert.Equal(AgentEnd.HandedOff, outcome.End);
        Assert.Single(session.Inputs);
        Assert.Empty(narrator.Answers);
    }

    [Fact]
    public async Task A_normal_tool_result_still_goes_back_to_the_ai()
    {
        var (outcome, session, _) = await Run(new ToolResult("call-1", "some text"));
        Assert.Equal(AgentEnd.Completed, outcome.End);
        Assert.Equal(2, session.Inputs.Count);
    }
}
