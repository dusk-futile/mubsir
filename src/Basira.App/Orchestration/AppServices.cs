using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.App.Speech;
using Basira.App.Storage;
using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Cost;
using Basira.Core.Input;
using Basira.Core.Localization;
using Basira.Core.Logging;
using Basira.Core.Policy;
using Basira.Core.Speech;

namespace Basira.App.Orchestration;

/// <summary>
/// The services the orchestration classes share, created by the host. Screen, ears, brain and executor are optional:
/// without them Basira runs local-only.
/// </summary>
internal sealed class AppServices
{
    public required IClock Clock { get; init; }

    public required IEventLog Log { get; init; }

    public required ISettingsProvider Settings { get; init; }

    public required ISecretStore Secrets { get; init; }

    public required IMemoryStore Memory { get; init; }

    public required SpeechService Speech { get; init; }

    public required NvdaController Nvda { get; init; }

    public required NvdaQuiet Quiet { get; init; }

    public required IAudioRecorder Recorder { get; init; }

    public required InputStateMachine Input { get; init; }

    public required PolicyEngine Policy { get; init; }

    public required CostLedger Ledger { get; init; }

    public required CostStore CostStore { get; init; }

    public required AppStateStore State { get; init; }

    public required Metrics Metrics { get; init; }

    public required DiagnosticsReport Report { get; init; }

    public IScreenReader? Screen { get; set; }

    public IEars? Ears { get; set; }

    public IBrain? Brain { get; set; }

    public IToolExecutor? Executor { get; set; }

    /// <summary>The language for Basira's own messages.</summary>
    public Lang UiLang => Settings.Profile.PreferredLanguage;

    public bool HasEars => Ears is not null && HasSecret(SecretNames.Gemini);

    public bool HasBrain => Brain is not null && HasSecret(SecretNames.Anthropic);

    public bool HasSecret(string name)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Secrets.Get(name));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Log.Failure("secret_read", e);
            return false;
        }
    }

    public void Say(StringKey key, params object[] args) =>
        Speech.Speak(Strings.Get(key, UiLang, args), UtteranceKind.System, UiLang);

    public void SayNow(StringKey key, params object[] args) =>
        Speech.Speak(Strings.Get(key, UiLang, args), UtteranceKind.System, UiLang, interrupt: true);

    public void Say(AppPhrase phrase, params object[] args) =>
        Speech.Speak(AppText.Get(phrase, UiLang, args), UtteranceKind.System, UiLang);

    public void SayNow(AppPhrase phrase, params object[] args) =>
        Speech.Speak(AppText.Get(phrase, UiLang, args), UtteranceKind.System, UiLang, interrupt: true);

    /// <summary>Earcon, message and recovery hint of a failure state.</summary>
    public void SayFailure(FailureState state, bool interrupt = true)
    {
        FailureInfo info = FailureCatalog.Describe(state);
        Speech.PlayEarcon(info.Earcon);
        string text = Strings.Get(info.Message, UiLang);
        if (info.Recovery is StringKey recovery)
        {
            text += " " + Strings.Get(recovery, UiLang);
        }

        Speech.Speak(text, UtteranceKind.System, UiLang, interrupt);
    }

    /// <summary>Speaks the failure for a brain or speech-to-text error.</summary>
    public void SayBrainError(BrainErrorKind kind, Lang lang)
    {
        switch (kind)
        {
            case BrainErrorKind.Network:
                SayFailure(FailureState.Offline);
                break;
            case BrainErrorKind.Auth or BrainErrorKind.NoApiKey:
                SayFailure(FailureState.NoApiKey);
                break;
            case BrainErrorKind.Refusal:
                Speech.Speak(AppText.Get(AppPhrase.CannotHelp, lang), UtteranceKind.System, lang, interrupt: true);
                break;
            case BrainErrorKind.RateLimited or BrainErrorKind.Overloaded:
                Speech.PlayEarcon(Earcon.Error);
                Speech.Speak(Strings.Get(StringKey.AiUnavailable, lang) + " " + Strings.Get(StringKey.LocalStillWorks, lang), UtteranceKind.System, lang, interrupt: true);
                break;
            default:
                Speech.PlayEarcon(Earcon.Error);
                Speech.Speak(Strings.Get(StringKey.AiUnavailable, lang), UtteranceKind.System, lang, interrupt: true);
                break;
        }
    }

    public void SaveCost() => CostStore.Save(Ledger);
}
