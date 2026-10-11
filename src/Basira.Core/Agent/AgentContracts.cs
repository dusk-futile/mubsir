using Basira.Core.Common;
using Basira.Core.Policy;

namespace Basira.Core.Agent;

/// <summary>A tool call resolved against the screen, ready for the policy and the executor.</summary>
/// <param name="Call">The model's call.</param>
/// <param name="Action">Ground-truth facts about what the call would do.</param>
/// <param name="ExecutorState">Anything the executor needs to run it later (e.g. the resolved UIA element).</param>
public sealed record PreparedAction(ToolCall Call, AgentAction Action, object? ExecutorState);

/// <summary>Runs tools on the PC. Implemented by the Windows layer.</summary>
public interface IToolExecutor
{
    /// <summary>
    /// Resolves the call: element ids to nodes, library names to absolute paths, link targets, and the UIA facts
    /// for confirmations ("recipient", "body", "amount" in <see cref="AgentAction.Args"/>). Must not change anything.
    /// When the screen it reads is a bank or payment page it should set <see cref="TaskContext.SensitiveMode"/>.
    /// </summary>
    Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct);

    Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct);

    /// <summary>Bank mode: focus the real button and read the amount and payee locally so the user presses Enter.</summary>
    Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct);

    /// <summary>One short phrase spoken before the step, e.g. "فتح يوتيوب".</summary>
    string Narration(PreparedAction action, Lang lang);
}

public enum ConfirmOutcome
{
    Accepted,
    Rejected,
    TimedOut,
}

/// <summary>Speaks the confirmation sentence and waits for Enter / Escape / a spoken yes (Windows layer).</summary>
public interface IConfirmer
{
    Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct);
}

public enum NarrationKind
{
    Step,
    Status,
    Warning,
}

/// <summary>Speech output for the loop: step narration, streamed answer text and earcons.</summary>
public interface INarrator
{
    void Narrate(string text, NarrationKind kind);

    void AnswerDelta(string text);

    /// <summary>The streamed answer text of a turn is complete; flush it.</summary>
    void AnswerEnd();

    void Play(Earcon earcon);
}

public interface IUsageSink
{
    void Report(UsageRecord usage);
}

/// <summary>Pause and resume for a running task. Thread-safe: the input thread pauses, the loop waits.</summary>
public sealed class AgentControl
{
    private readonly object _gate = new();
    private TaskCompletionSource? _resume;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _resume is not null;
            }
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _resume ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource? resume;
        lock (_gate)
        {
            resume = _resume;
            _resume = null;
        }

        resume?.TrySetResult();
    }

    /// <summary>Completes at once when running; otherwise when resumed or when <paramref name="ct"/> is cancelled.</summary>
    public Task WaitIfPausedAsync(CancellationToken ct)
    {
        Task wait;
        lock (_gate)
        {
            if (_resume is null)
            {
                return ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;
            }

            wait = _resume.Task;
        }

        return wait.WaitAsync(ct);
    }
}

/// <summary>One spoken task.</summary>
/// <param name="Utterance">The user's request.</param>
/// <param name="InitialContextText">Built and redacted screen context. If it came from an untrusted window,
/// the caller sets <see cref="TaskContext.UntrustedContentSeen"/> (see <see cref="PolicyEngine.MarksUntrusted"/>).</param>
/// <param name="InitialImage">Announced screenshot, if any.</param>
/// <param name="Task">Shared task state for the policy.</param>
/// <param name="Options">Session options; tools are ignored in quick-answer mode.</param>
/// <param name="MaxSteps">Most tool-call rounds.</param>
/// <param name="MaxSeconds">Wall-clock limit, checked between steps.</param>
public sealed record AgentRequest(
    string Utterance,
    string? InitialContextText,
    byte[]? InitialImage,
    TaskContext Task,
    BrainSessionOptions Options,
    int MaxSteps = 15,
    int MaxSeconds = 180);

public enum AgentEnd
{
    Completed,
    StepLimit,
    TimeLimit,
    Cancelled,
    BrainError,
    PolicyStopped,
}

/// <param name="End">How the task ended.</param>
/// <param name="Steps">Tool-call rounds executed.</param>
/// <param name="Error">The brain error when <paramref name="End"/> is <see cref="AgentEnd.BrainError"/>.</param>
/// <param name="FinalText">The text of the model's last turn.</param>
public sealed record AgentOutcome(AgentEnd End, int Steps, BrainErrorKind? Error = null, string? FinalText = null);
