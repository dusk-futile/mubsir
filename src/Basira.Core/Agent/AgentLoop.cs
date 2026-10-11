using System.Text;
using Basira.Core.Common;
using Basira.Core.Policy;

namespace Basira.Core.Agent;

/// <summary>
/// Provider-neutral agent loop: stream a turn, run the requested tools one by one through the policy
/// (allow / confirm / block), return all results in one message, repeat until the model answers without tools.
/// Enforces the step and time limits, pause, cancellation, and stops for safety (bank mode, private mode,
/// administrator windows, repeated blocks).
/// </summary>
public sealed class AgentLoop(
    IBrain brain,
    IToolExecutor executor,
    PolicyEngine policy,
    IConfirmer confirmer,
    INarrator narrator,
    IUsageSink usage,
    IClock clock)
{
    /// <summary>After this many blocked actions in one task the loop stops.</summary>
    public const int MaxBlocksPerTask = 3;

    public const string DeclinedResult = "declined by user";
    public const string BlockedPrefix = "blocked: ";

    public async Task<AgentOutcome> RunAsync(AgentRequest request, AgentControl control, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(control);

        var task = request.Task;
        var lang = task.Lang;
        int steps = 0;

        if (!policy.AllowCloud(task, screen: null))
        {
            narrator.Narrate(task.PrivateMode ? AgentText.PrivateStop(lang) : AgentText.BankStop(lang), NarrationKind.Warning);
            return new AgentOutcome(AgentEnd.PolicyStopped, 0);
        }

        bool quick = request.Options.Mode == BrainMode.QuickAnswer;
        var options = quick ? request.Options with { Tools = [] } : request.Options;
        long deadline = clock.NowMs + Math.Max(1, request.MaxSeconds) * 1000L;
        var state = new RunState();
        IBrainSession? session = null;
        try
        {
            await control.WaitIfPausedAsync(ct).ConfigureAwait(false);
            session = brain.StartSession(options);
            BrainInput input = new UserTurn(request.Utterance, request.InitialContextText, request.InitialImage);

            while (true)
            {
                var turn = await RunTurnAsync(session, input, ct).ConfigureAwait(false);
                if (turn.Error is { } error)
                {
                    if (turn.Text.Length > 0)
                    {
                        narrator.AnswerEnd();
                    }

                    return new AgentOutcome(AgentEnd.BrainError, steps, error, turn.Text.Length > 0 ? turn.Text : null);
                }

                if (quick || turn.Calls.Count == 0)
                {
                    narrator.AnswerEnd();
                    return new AgentOutcome(AgentEnd.Completed, steps, FinalText: turn.Text);
                }

                if (turn.Text.Length > 0)
                {
                    narrator.AnswerEnd(); // flush the short phrase the model said before its tool calls
                }

                if (steps >= request.MaxSteps)
                {
                    narrator.Narrate(AgentText.StepLimit(lang), NarrationKind.Warning);
                    return new AgentOutcome(AgentEnd.StepLimit, steps);
                }

                if (clock.NowMs >= deadline)
                {
                    narrator.Narrate(AgentText.TimeLimit(lang), NarrationKind.Warning);
                    return new AgentOutcome(AgentEnd.TimeLimit, steps);
                }

                steps++;
                var results = new List<ToolResult>(turn.Calls.Count);
                foreach (var call in turn.Calls)
                {
                    var (result, stop) = await HandleCallAsync(call, task, control, state, ct).ConfigureAwait(false);
                    results.Add(result);
                    if (stop)
                    {
                        return new AgentOutcome(AgentEnd.PolicyStopped, steps);
                    }
                }

                await control.WaitIfPausedAsync(ct).ConfigureAwait(false);
                input = new ToolResults(results);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new AgentOutcome(AgentEnd.Cancelled, steps);
        }
        finally
        {
            if (session is not null)
            {
                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    // Disposal problems must not hide the task's outcome.
                }
            }
        }
    }

    private async Task<TurnResult> RunTurnAsync(IBrainSession session, BrainInput input, CancellationToken ct)
    {
        var calls = new List<ToolCall>();
        var text = new StringBuilder();
        BrainErrorKind? error = null;
        try
        {
            await foreach (var ev in session.SendAsync(input, ct).WithCancellation(ct).ConfigureAwait(false))
            {
                switch (ev)
                {
                    case TextDelta { Text.Length: > 0 } delta:
                        text.Append(delta.Text);
                        narrator.AnswerDelta(delta.Text);
                        break;
                    case UsageReported reported:
                        usage.Report(reported.Usage);
                        break;
                    case ToolCallRequested requested:
                        calls.Add(requested.Call);
                        break;
                    case BrainFailed failed:
                        error = failed.Kind;
                        break;
                }

                if (error is not null)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is OperationCanceledException or HttpRequestException or IOException or TimeoutException)
        {
            error = BrainErrorKind.Network; // e.g. an HTTP timeout, not the user's stop
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            error = BrainErrorKind.Other;
        }

        return new TurnResult(calls, text.ToString(), error);
    }

    private async Task<(ToolResult Result, bool Stop)> HandleCallAsync(
        ToolCall call, TaskContext task, AgentControl control, RunState state, CancellationToken ct)
    {
        await control.WaitIfPausedAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var lang = task.Lang;

        PreparedAction prepared;
        try
        {
            prepared = await executor.PrepareAsync(call, task, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return (new ToolResult(call.Id, "error: this tool call could not be prepared", IsError: true), false);
        }

        var decision = policy.Evaluate(prepared.Action, task);
        ToolResult result;
        bool stop = false;
        switch (decision.Tier)
        {
            case ActionTier.Block when decision.BankHandoff:
                // The user presses the real button himself; nothing more from this page goes to the cloud.
                narrator.Play(Earcon.BankMode);
                result = await RunToolAsync(() => executor.HandOffAsync(prepared, ct), call, ct).ConfigureAwait(false);
                stop = true;
                break;

            case ActionTier.Block:
                state.Blocks++;
                narrator.Narrate(AgentText.Blocked(decision.ReasonCode, lang), NarrationKind.Warning);
                result = new ToolResult(call.Id, BlockedPrefix + decision.ReasonCode, IsError: true);
                if (decision.ReasonCode == ReasonCodes.Elevated || decision.ReasonCode.StartsWith("bank.", StringComparison.Ordinal))
                {
                    stop = true;
                }
                else if (state.Blocks >= MaxBlocksPerTask)
                {
                    narrator.Narrate(AgentText.PolicyStopped(lang), NarrationKind.Warning);
                    stop = true;
                }

                break;

            case ActionTier.Confirm:
            {
                var key = DeclineKey(prepared.Action, call);
                if (state.Declined.Contains(key))
                {
                    result = new ToolResult(call.Id, DeclinedResult, IsError: true);
                    break;
                }

                narrator.Play(Earcon.NeedsConfirmation);
                var facts = decision.Facts ?? new ConfirmationFacts { ToolName = prepared.Action.Tool };
                var answer = await confirmer.ConfirmAsync(ConfirmationBuilder.Build(facts, lang), ct).ConfigureAwait(false);
                if (answer == ConfirmOutcome.Accepted)
                {
                    result = await ExecuteAsync(prepared, call, task, ct).ConfigureAwait(false);
                }
                else
                {
                    state.Declined.Add(key);
                    narrator.Narrate(AgentText.Declined(lang), NarrationKind.Status);
                    result = new ToolResult(call.Id, DeclinedResult, IsError: true);
                }

                break;
            }

            default:
            {
                string? phrase = null;
                try
                {
                    phrase = executor.Narration(prepared, lang);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                }

                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    narrator.Narrate(phrase, NarrationKind.Step);
                }

                result = await ExecuteAsync(prepared, call, task, ct).ConfigureAwait(false);
                break;
            }
        }

        if (!stop && !policy.AllowCloud(task, prepared.Action.Screen))
        {
            // Bank or private mode began during the task: stop before anything else is sent.
            narrator.Narrate(task.PrivateMode ? AgentText.PrivateStop(lang) : AgentText.BankStop(lang), NarrationKind.Warning);
            stop = true;
        }

        return (result, stop);
    }

    private async Task<ToolResult> ExecuteAsync(PreparedAction prepared, ToolCall call, TaskContext task, CancellationToken ct)
    {
        var result = await RunToolAsync(() => executor.ExecuteAsync(prepared, ct), call, ct).ConfigureAwait(false);
        var tool = string.IsNullOrWhiteSpace(prepared.Action.Tool) ? call.Name : prepared.Action.Tool;
        if (!result.IsError && PolicyEngine.MarksUntrusted(tool, prepared.Action.Screen))
        {
            task.UntrustedContentSeen = true;
        }

        return result;
    }

    private static async Task<ToolResult> RunToolAsync(Func<Task<ToolResult>> run, ToolCall call, CancellationToken ct)
    {
        ToolResult result;
        try
        {
            result = await run().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The type name only: messages can hold paths or screen text.
            return new ToolResult(call.Id, "error: " + e.GetType().Name, IsError: true);
        }

        return result.ToolCallId == call.Id ? result : result with { ToolCallId = call.Id };
    }

    /// <summary>Identity of an action for "never ask twice": the tool plus its target (or its arguments when it has none).</summary>
    internal static string DeclineKey(AgentAction a, ToolCall call)
    {
        var sb = new StringBuilder();
        sb.Append(string.IsNullOrWhiteSpace(a.Tool) ? call.Name : a.Tool.Trim().ToLowerInvariant()).Append('|');
        if (a.Target is { } t)
        {
            sb.Append(t.RuntimeId is { Length: > 0 } rid ? rid : $"{t.Role}/{t.AutomationId}/{t.Name}");
        }

        sb.Append('|').Append(a.KeyChord).Append('|').Append(a.Url).Append('|').Append(a.DestinationPath).Append('|');
        foreach (var p in a.FilePaths.Order(StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(p).Append(';');
        }

        foreach (var (k, v) in a.Args.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (a.Target is null || k == "keys")
            {
                sb.Append('|').Append(k).Append('=').Append(v);
            }
        }

        return sb.ToString();
    }

    private sealed class RunState
    {
        public int Blocks { get; set; }

        public HashSet<string> Declined { get; } = new(StringComparer.Ordinal);
    }

    private sealed record TurnResult(List<ToolCall> Calls, string Text, BrainErrorKind? Error);
}
