using Mubsir.Core.Sessions;
using Mubsir.App.Speech;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Cost;
using Mubsir.Core.Input;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Policy;
using Mubsir.Core.Speech;

namespace Mubsir.App.Orchestration;

internal sealed class AppServices
{
    public required IClock Clock { get; init; }

    public required IEventLog Log { get; init; }

    public required SettingsProvider Settings { get; init; }

    public required SecretStore Secrets { get; init; }

    public required MemoryStore Memory { get; init; }

    public required SpeechService Speech { get; init; }

    public required NvdaController Nvda { get; init; }

    public required NvdaQuiet Quiet { get; init; }

    public required WaveInRecorder Recorder { get; init; }

    public required InputStateMachine Input { get; init; }

    public required PolicyEngine Policy { get; init; }

    public required CostLedger Ledger { get; init; }

    public required CostStore CostStore { get; init; }

    public required AppStateStore State { get; init; }


    public required DiagnosticsReport Report { get; init; }

    public UiaScreenReader? Screen { get; set; }

    public BookAloud? Books { get; set; }

    public EarsRouter? Ears { get; set; }

    /// <summary>The notch. Null when they're turned off or couldn't start; nothing depends on them.</summary>
    public CompanionBridge? Companion { get; set; }

    /// <summary>Saved chats. Private mode and the setting keep it from writing.</summary>
    public SessionArchive? Sessions { get; set; }

    public IBrain? Brain { get; set; }

    public IToolExecutor? Executor { get; set; }

    public Action? OpenSetup { get; set; }

    public Lang UiLang => Settings.Profile.PreferredLanguage;

    public bool HasEars => Ears is { HasAnyKey: true };

    public bool HasBrain => Brain is not null && (HasSecret(SecretNames.Anthropic) || HasSecret(SecretNames.Gemini));

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

    public void SayBrainError(BrainErrorKind kind, Lang lang)
    {
        switch (kind)
        {
            case BrainErrorKind.Network:
                SayFailure(FailureState.Offline);
                break;
            case BrainErrorKind.NoCredit:
                SayNow(AppPhrase.ClaudeNoCredit);
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
