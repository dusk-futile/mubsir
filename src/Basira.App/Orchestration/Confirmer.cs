using Basira.App.Diagnostics;
using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Localization;
using Basira.Core.Logging;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.App.Orchestration;

/// <summary>What the user did while a confirmation was pending.</summary>
internal enum ConfirmSignal
{
    Accept,
    Reject,
    Repeat,
}

/// <summary>
/// One-key confirmation: speaks the prompt (interrupting), then waits for Enter / Escape / "." (repeat) from the
/// keyboard state machine, or a spoken yes or no, or 30 s of silence. Keys pressed before the prompt has finished
/// (plus the state machine's grace period) are ignored. One confirmation at a time; the pending state is always cleared.
/// </summary>
internal sealed class Confirmer : IConfirmer
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly AppServices _app;
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly object _gate = new();
    private Pending? _pending;

    public Confirmer(AppServices app)
    {
        _app = app;
    }

    public bool IsPending
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }

    public Task<ConfirmOutcome> ConfirmAsync(string prompt, CancellationToken ct) =>
        ConfirmAsync(prompt, DefaultTimeout, allowVoice: true, sayTimedOut: true, ct);

    /// <param name="allowVoice">False when a spoken answer must not be sent to the cloud (the privacy consent itself).</param>
    public async Task<ConfirmOutcome> ConfirmAsync(string prompt, TimeSpan timeout, bool allowVoice, bool sayTimedOut, CancellationToken ct)
    {
        await _one.WaitAsync(ct).ConfigureAwait(false);
        var pending = new Pending(allowVoice);
        Lang lang = ScriptSegmenter.DominantLanguage(prompt, _app.UiLang);
        try
        {
            lock (_gate)
            {
                _pending = pending;
            }

            // Pending at once so Escape can cancel early; Enter only counts after the prompt was spoken.
            _app.Input.SetConfirmationPending(true, NotYet());
            await SpeakPromptAsync(prompt, lang, ct).ConfigureAwait(false);

            while (true)
            {
                Task<ConfirmSignal> signal = pending.NextAsync();
                Task finished = await Task.WhenAny(signal, Task.Delay(timeout, ct)).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (finished != signal)
                {
                    _app.Log.Event(LogKind.Confirmation, LogOutcome.TimedOut);
                    if (sayTimedOut)
                    {
                        _app.Speech.Speak(Strings.Get(StringKey.ConfirmTimedOut, lang), UtteranceKind.System, lang, interrupt: true);
                    }

                    return ConfirmOutcome.TimedOut;
                }

                switch (await signal.ConfigureAwait(false))
                {
                    case ConfirmSignal.Accept:
                        _app.Log.Event(LogKind.Confirmation, LogOutcome.Ok);
                        return ConfirmOutcome.Accepted;
                    case ConfirmSignal.Reject:
                        _app.Log.Event(LogKind.Confirmation, LogOutcome.Declined);
                        return ConfirmOutcome.Rejected;
                    default:
                        _app.Input.SetConfirmationPending(true, NotYet());
                        await SpeakPromptAsync(prompt, lang, ct).ConfigureAwait(false);
                        break;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _pending = null;
            }

            _app.Input.SetConfirmationPending(false, 0);
            _one.Release();
        }
    }

    /// <summary>Enter, Escape or "." from the state machine.</summary>
    public void Signal(ConfirmSignal signal)
    {
        Pending? pending;
        lock (_gate)
        {
            pending = _pending;
        }

        pending?.Push(signal);
    }

    /// <summary>A spoken or typed answer while a confirmation is pending. Returns false when none is pending.</summary>
    public bool TryTakeAnswer(string text)
    {
        Pending? pending;
        lock (_gate)
        {
            pending = _pending;
        }

        if (pending is null)
        {
            return false;
        }

        switch (YesNoMatcher.Match(text))
        {
            case SpokenAnswer.Yes:
                pending.Push(ConfirmSignal.Accept);
                break;
            case SpokenAnswer.No:
                pending.Push(ConfirmSignal.Reject);
                break;
            default:
                _app.SayNow(AppPhrase.AnswerYesOrNo);
                break;
        }

        return true;
    }

    /// <summary>Whether a pending confirmation accepts spoken answers (the consent prompt does not).</summary>
    public bool AcceptsVoice
    {
        get
        {
            lock (_gate)
            {
                return _pending?.AllowVoice ?? true;
            }
        }
    }

    /// <summary>A time far enough ahead that Enter is ignored until the prompt has been spoken.</summary>
    private long NotYet() => _app.Clock.NowMs + (long)TimeSpan.FromHours(1).TotalMilliseconds;

    private async Task SpeakPromptAsync(string prompt, Lang lang, CancellationToken ct)
    {
        // A hung voice must not hold the confirmation forever: Enter becomes valid after a generous budget.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(20) + TimeSpan.FromMilliseconds(prompt.Length * 120L));
        try
        {
            await _app.Speech.SpeakAsync(prompt, UtteranceKind.System, lang, budget.Token, interrupt: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }

        ct.ThrowIfCancellationRequested();
        _app.Input.SetConfirmationPending(true, _app.Clock.NowMs);
    }

    /// <summary>Signals for one pending confirmation, buffered so none is lost between waits.</summary>
    private sealed class Pending(bool allowVoice)
    {
        private readonly object _gate = new();
        private readonly Queue<ConfirmSignal> _signals = new();
        private TaskCompletionSource<ConfirmSignal>? _waiter;

        public bool AllowVoice { get; } = allowVoice;

        public void Push(ConfirmSignal signal)
        {
            TaskCompletionSource<ConfirmSignal>? waiter;
            lock (_gate)
            {
                waiter = _waiter;
                _waiter = null;
                if (waiter is null)
                {
                    _signals.Enqueue(signal);
                }
            }

            waiter?.TrySetResult(signal);
        }

        public Task<ConfirmSignal> NextAsync()
        {
            lock (_gate)
            {
                if (_signals.TryDequeue(out ConfirmSignal s))
                {
                    return Task.FromResult(s);
                }

                _waiter ??= new TaskCompletionSource<ConfirmSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _waiter.Task;
            }
        }
    }
}
