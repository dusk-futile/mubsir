using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Common;
using Basira.Core.Logging;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.App.Orchestration;

/// <summary>
/// The agent's ask_user: speaks the question, then hands the next spoken or typed request (within 60 s) back as the
/// answer instead of processing it as a new request. One question at a time.
/// </summary>
internal sealed class UserPrompter : IUserPrompter
{
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(60);

    private readonly AppServices _app;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly object _gate = new();
    private TaskCompletionSource<string?>? _waiter;

    public UserPrompter(AppServices app)
    {
        _app = app;
    }

    public bool IsPending
    {
        get
        {
            lock (_gate)
            {
                return _waiter is not null;
            }
        }
    }

    public async Task<string?> AskAsync(string question, CancellationToken ct)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        var waiter = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            lock (_gate)
            {
                _waiter = waiter;
            }

            if (!string.IsNullOrWhiteSpace(question))
            {
                Lang lang = ScriptSegmenter.DominantLanguage(question, _app.UiLang);
                _app.Speech.Speak(question, UtteranceKind.System, lang, interrupt: true);
            }

            Task finished = await Task.WhenAny(waiter.Task, Task.Delay(AnswerTimeout, ct)).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (finished != waiter.Task)
            {
                _app.Log.Event(LogKind.Confirmation, LogOutcome.TimedOut, tool: "ask_user");
                return null;
            }

            return await waiter.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_waiter, waiter))
                {
                    _waiter = null;
                }
            }

            _one.Release();
        }
    }

    /// <summary>Delivers the user's answer. Returns false when no question is pending.</summary>
    public bool TryTakeAnswer(string text)
    {
        TaskCompletionSource<string?>? waiter;
        lock (_gate)
        {
            waiter = _waiter;
            _waiter = null;
        }

        return waiter is not null && waiter.TrySetResult(text);
    }
}
