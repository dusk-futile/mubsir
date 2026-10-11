using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Policy;

namespace Basira.Core.Tests.Agent;

/// <summary>A brain whose turns are scripted in order; records every session, input and disposal.</summary>
internal sealed class ScriptedBrain : IBrain
{
    private readonly Queue<Func<BrainInput, CancellationToken, IAsyncEnumerable<BrainEvent>>> _turns = new();

    public List<BrainSessionOptions> Started { get; } = [];

    public List<BrainInput> Inputs { get; } = [];

    public int Disposed { get; private set; }

    public Exception? StartError { get; set; }

    /// <summary>Used once the scripted turns run out (e.g. a model that never stops calling tools).</summary>
    public Func<int, BrainEvent[]>? Forever { get; set; }

    public ScriptedBrain Turn(params BrainEvent[] events)
    {
        _turns.Enqueue((_, ct) => Stream(events, ct));
        return this;
    }

    public ScriptedBrain Turn(Func<BrainInput, CancellationToken, IAsyncEnumerable<BrainEvent>> turn)
    {
        _turns.Enqueue(turn);
        return this;
    }

    public IBrainSession StartSession(BrainSessionOptions options)
    {
        if (StartError is not null)
        {
            throw StartError;
        }

        Started.Add(options);
        return new Session(this);
    }

    public static async IAsyncEnumerable<BrainEvent> Stream(IEnumerable<BrainEvent> events, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var e in events)
        {
            ct.ThrowIfCancellationRequested();
            yield return e;
        }

        await Task.CompletedTask;
    }

    private IAsyncEnumerable<BrainEvent> Next(BrainInput input, CancellationToken ct)
    {
        Inputs.Add(input);
        if (_turns.TryDequeue(out var turn))
        {
            return turn(input, ct);
        }

        if (Forever is not null)
        {
            return Stream(Forever(Inputs.Count), ct);
        }

        throw new InvalidOperationException("The test scripted no more turns.");
    }

    private sealed class Session(ScriptedBrain brain) : IBrainSession
    {
        public IAsyncEnumerable<BrainEvent> SendAsync(BrainInput input, CancellationToken ct) => brain.Next(input, ct);

        public ValueTask DisposeAsync()
        {
            brain.Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Resolves calls to actions (by default on a neutral calculator window) and records what runs.</summary>
internal sealed class FakeExecutor(List<string> log) : IToolExecutor
{
    public static readonly ScreenContext Calculator = new() { AppName = "Calculator", ProcessName = "CalculatorApp.exe", WindowTitle = "Calculator" };

    /// <summary>Adjusts the default action (tool name, string args, calculator screen) for a call.</summary>
    public Func<ToolCall, AgentAction, AgentAction> Resolve { get; set; } = (_, a) => a;

    public Dictionary<string, Func<PreparedAction, ToolResult>> Handlers { get; } = new(StringComparer.Ordinal);

    public Func<ToolCall, Exception?> PrepareError { get; set; } = _ => null;

    public Func<PreparedAction, string> NarrationText { get; set; } = a => "step " + a.Call.Name;

    public List<ToolCall> Prepared { get; } = [];

    public List<PreparedAction> Executed { get; } = [];

    public List<PreparedAction> HandedOff { get; } = [];

    public Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct)
    {
        log.Add("prepare " + call.Id);
        Prepared.Add(call);
        if (PrepareError(call) is { } error)
        {
            throw error;
        }

        var action = new AgentAction { Tool = call.Name, Args = ArgsOf(call.Arguments), Screen = Calculator };
        return Task.FromResult(new PreparedAction(call, Resolve(call, action), null));
    }

    public Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct)
    {
        log.Add("execute " + action.Call.Id);
        Executed.Add(action);
        var result = Handlers.TryGetValue(action.Call.Name, out var handler)
            ? handler(action)
            : new ToolResult(action.Call.Id, "ok " + action.Call.Name);
        return Task.FromResult(result);
    }

    public Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct)
    {
        log.Add("handoff " + action.Call.Id);
        HandedOff.Add(action);
        return Task.FromResult(new ToolResult(action.Call.Id, "handed off"));
    }

    public string Narration(PreparedAction action, Lang lang) => NarrationText(action);

    public static IReadOnlyDictionary<string, string?> ArgsOf(JsonElement args)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (args.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var p in args.EnumerateObject())
        {
            map[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Array => string.Join('\n', p.Value.EnumerateArray().Select(e => e.ToString())),
                _ => p.Value.GetRawText(),
            };
        }

        return map;
    }
}

internal sealed class FakeConfirmer(List<string> log) : IConfirmer
{
    public Queue<ConfirmOutcome> Outcomes { get; } = new();

    public List<string> Prompts { get; } = [];

    /// <summary>Replaces the queue, e.g. to wait for cancellation or to throw.</summary>
    public Func<string, CancellationToken, Task<ConfirmOutcome>>? Custom { get; set; }

    public Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct)
    {
        log.Add("confirm");
        Prompts.Add(prompt);
        if (Custom is not null)
        {
            return Custom(prompt, ct);
        }

        return Task.FromResult(Outcomes.TryDequeue(out var outcome) ? outcome : ConfirmOutcome.Rejected);
    }
}

internal sealed class FakeNarrator(List<string> log) : INarrator
{
    public List<(string Text, NarrationKind Kind)> Narrations { get; } = [];

    public StringBuilder Answer { get; } = new();

    public int AnswerEnds { get; private set; }

    public List<Earcon> Earcons { get; } = [];

    public Action<string>? OnDelta { get; set; }

    public IEnumerable<string> Warnings => Narrations.Where(n => n.Kind == NarrationKind.Warning).Select(n => n.Text);

    public IEnumerable<string> Steps => Narrations.Where(n => n.Kind == NarrationKind.Step).Select(n => n.Text);

    public void Narrate(string text, NarrationKind kind)
    {
        log.Add($"narrate {kind}: {text}");
        Narrations.Add((text, kind));
    }

    public void AnswerDelta(string text)
    {
        Answer.Append(text);
        OnDelta?.Invoke(text);
    }

    public void AnswerEnd() => AnswerEnds++;

    public void Play(Earcon earcon)
    {
        log.Add("earcon " + earcon);
        Earcons.Add(earcon);
    }
}

internal sealed class FakeUsage : IUsageSink
{
    public List<UsageRecord> Records { get; } = [];

    public void Report(UsageRecord usage) => Records.Add(usage);
}
