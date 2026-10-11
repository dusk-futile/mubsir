using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal enum ConfirmSignal
{
    Accept,
    Reject,
    Repeat,
}

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

    public void Signal(ConfirmSignal signal)
    {
        Pending? pending;
        lock (_gate)
        {
            pending = _pending;
        }

        pending?.Push(signal);
    }

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

    private long NotYet() => _app.Clock.NowMs + (long)TimeSpan.FromHours(1).TotalMilliseconds;

    private async Task SpeakPromptAsync(string prompt, Lang lang, CancellationToken ct)
    {
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
