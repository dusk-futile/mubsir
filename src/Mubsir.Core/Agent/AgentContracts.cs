using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Agent;

public sealed record PreparedAction(ToolCall Call, AgentAction Action, object? ExecutorState);

public interface IToolExecutor
{
    Task<PreparedAction> PrepareAsync(ToolCall call, TaskContext task, CancellationToken ct);

    Task<ToolResult> ExecuteAsync(PreparedAction action, CancellationToken ct);

    Task<ToolResult> HandOffAsync(PreparedAction action, CancellationToken ct);

    string Narration(PreparedAction action, Lang lang);
}

public enum ConfirmOutcome
{
    Accepted,
    Rejected,
    TimedOut,
}

public interface IConfirmer
{
    Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct);

    /// <summary>For what can't be taken back: only a real key press counts, so sound from the speakers can't agree.</summary>
    Task<ConfirmOutcome> ConfirmByKeyAsync(string prompt, CancellationToken ct) => ConfirmAsync(prompt, ct);
}

public enum NarrationKind
{
    Step,
    Status,
    Warning,
}

public interface INarrator
{
    void Narrate(string text, NarrationKind kind);

    /// <summary>The step just narrated has finished: said out loud, so someone who can't see knows it's done.</summary>
    void StepFinished(bool ok);

    void AnswerDelta(string text);

    void AnswerEnd();

    void Play(Earcon earcon);
}

public interface IUsageSink
{
    void Report(UsageRecord usage);
}

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

    /// <summary>A tool took over, like the book reader. It speaks for itself, so nothing is reported.</summary>
    HandedOff,
}

public sealed record AgentOutcome(AgentEnd End, int Steps, BrainErrorKind? Error = null, string? FinalText = null);
