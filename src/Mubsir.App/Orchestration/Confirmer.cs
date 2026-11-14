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

    public Task<ConfirmOutcome> ConfirmByKeyAsync(string prompt, CancellationToken ct) =>
        ConfirmAsync(prompt + " " + AppText.Get(AppPhrase.ConsentPressEnter, ScriptSegmenter.DominantLanguage(prompt, _app.UiLang)), DefaultTimeout, allowVoice: false, sayTimedOut: true, ct);

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

        // Something that can't be undone takes a real key press. A spoken yes doesn't count, wherever it came from.
        if (!pending.AllowVoice)
        {
            _app.SayNow(AppPhrase.ConsentPressEnter);
            return true;
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

internal enum SpokenAnswer
{
    Unclear,
    Yes,
    No,
}

internal static class YesNoMatcher
{
    private static readonly string[] YesPhrases = Fold(
    [
        "نعم", "أيوه", "ايوه", "أيوة", "ايوا", "أيوا", "اه", "آه", "تمام", "موافق", "موافقه", "اكيد", "أكيد",
        "طبعا", "طبعًا", "يلا", "ماشي", "اوكي", "أوكي", "حاضر", "صح", "بلى", "اي", "إي", "yes", "yeah", "yep", "ok", "okay", "sure",
        "confirm", "go ahead", "do it",
    ]);

    private static readonly string[] NoPhrases = Fold(
    [
        "لا", "لأ", "لاء", "لا لا", "مش عايز", "مش عاوز", "ما بدي", "مابدي", "ما ابغى", "لا شكرا", "إلغاء", "الغاء", "الغي", "ألغي",
        "بلاش", "خلاص لا", "توقف", "وقف", "no", "nope", "cancel", "stop", "don't", "dont", "do not",
    ]);

    private static readonly HashSet<string> Fillers = new(Fold(
    [
        "يا", "مبصر", "مُبصِر", "mubsir", "please", "من", "فضلك", "لو", "سمحت", "شكرا", "thanks", "thank", "you", "يعني",
    ]), StringComparer.Ordinal);

    public static SpokenAnswer Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SpokenAnswer.Unclear;
        }

        string folded = ArabicFolding.Fold(text);
        string core = string.Join(' ', folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Fillers.Contains(w)));
        if (core.Length == 0)
        {
            return SpokenAnswer.Unclear;
        }

        bool no = NoPhrases.Any(p => core == p || StartsWithWords(core, p));
        bool yes = YesPhrases.Any(p => core == p || StartsWithWords(core, p));
        return no ? SpokenAnswer.No : yes ? SpokenAnswer.Yes : SpokenAnswer.Unclear;
    }

    private static bool StartsWithWords(string text, string phrase) =>
        text.StartsWith(phrase, StringComparison.Ordinal) && (text.Length == phrase.Length || text[phrase.Length] == ' ')
        && text.Split(' ').Length <= phrase.Split(' ').Length + 2;

    private static string[] Fold(string[] words) => words.Select(ArabicFolding.Fold).Where(w => w.Length > 0).Distinct().ToArray();
}
