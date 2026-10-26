using Mubsir.App.Native;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Policy;
using Mubsir.Core.Router;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal sealed class LocalCommandRunner
{
    private const int RateStep = 10;
    private static readonly TimeSpan PrepareTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ExecuteTimeout = TimeSpan.FromSeconds(15);

    private readonly AppServices _app;
    private readonly ReadingController _reading;
    private readonly Confirmer _confirmer;
    private readonly TaskRunner _tasks;
    private readonly Action _stopAll;
    private readonly Action _saveReport;

    public LocalCommandRunner(AppServices app, ReadingController reading, Confirmer confirmer, TaskRunner tasks, Action stopAll, Action saveReport)
    {
        _app = app;
        _reading = reading;
        _confirmer = confirmer;
        _tasks = tasks;
        _stopAll = stopAll;
        _saveReport = saveReport;
    }

    public async Task RunAsync(LocalCommand command, string? argument, string utterance, Lang lang, CancellationToken ct)
    {
        _app.Log.Event(LogKind.Hotkey, tool: "local_" + command.ToString().ToLowerInvariant());
        switch (command)
        {
            case LocalCommand.ReadScreen:
                await _reading.ReadWindowAsync(ct).ConfigureAwait(false);
                break;
            case LocalCommand.ReadFromHere:
                await _reading.ReadFromHereAsync(ct).ConfigureAwait(false);
                break;
            case LocalCommand.WhereAmI:
                await _reading.WhereAmIAsync(ct).ConfigureAwait(false);
                break;
            case LocalCommand.OcrWindow:
                await _reading.OcrWindowAsync(ct).ConfigureAwait(false);
                break;
            case LocalCommand.Stop:
                _stopAll();
                break;
            case LocalCommand.Repeat:
                _reading.Repeat();
                break;
            case LocalCommand.PreviousSentence:
                _reading.PreviousSentence();
                break;
            case LocalCommand.NextSentence:
                _reading.NextSentence();
                break;
            case LocalCommand.SpeakFaster:
                ChangeRate(+RateStep, lang);
                break;
            case LocalCommand.SpeakSlower:
                ChangeRate(-RateStep, lang);
                break;
            case LocalCommand.Pause:
                _tasks.Cancel();
                _app.Settings.Update(s => s with { MubsirPaused = true });
                _app.Input.SetMubsirPaused(true);
                SayKey(StringKey.Paused, lang);
                break;
            case LocalCommand.Resume:
                _app.Settings.Update(s => s with { MubsirPaused = false });
                _app.Input.SetMubsirPaused(false);
                SayKey(StringKey.Resumed, lang);
                break;
            case LocalCommand.Help:
                SayKey(StringKey.LayerHelp, lang);
                break;
            case LocalCommand.WhatTime:
                SayKey(StringKey.TimeNow, lang, SpokenDateTime.Time(_app.Clock.UtcNow.LocalDateTime, lang));
                break;
            case LocalCommand.WhatDate:
                SayKey(StringKey.DateToday, lang, SpokenDateTime.Date(_app.Clock.UtcNow.LocalDateTime, lang));
                break;
            case LocalCommand.PrivateModeOn:
                _tasks.Cancel();
                _app.Settings.Update(s => s with { PrivateMode = true });
                SayKey(StringKey.PrivateModeOn, lang);
                break;
            case LocalCommand.PrivateModeOff:
                _app.Settings.Update(s => s with { PrivateMode = false });
                SayKey(StringKey.PrivateModeOff, lang);
                break;
            case LocalCommand.Settings:
                SpeakSettings(lang);
                break;
            case LocalCommand.SaveReport:
                _saveReport();
                break;
            case LocalCommand.KeyExplorer:
                _app.Input.SetKeyExplorer(true);
                SayKey(StringKey.KeyExplorerIntro, lang);
                break;
            case LocalCommand.BatteryStatus when SpeakBatteryLocally(lang):
                break;
            default:
                await RunToolCommandAsync(command, argument, utterance, lang, ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task RunToolCommandAsync(LocalCommand command, string? argument, string utterance, Lang lang, CancellationToken ct)
    {
        ToolCall? call = LocalCommandMap.ToToolCall(command, argument, "local-" + Guid.NewGuid().ToString("N"));
        if (call is null)
        {
            SayKey(StringKey.NotUnderstood, lang);
            return;
        }

        if (_app.Executor is not { } executor)
        {
            Say(AppText.Get(AppPhrase.FeatureUnavailable, lang), lang);
            return;
        }

        TaskContext task = TaskContexts.Create(utterance, lang, _app.Settings.Settings);
        PreparedAction prepared;
        try
        {
            prepared = await executor.PrepareAsync(call, task, ct).WaitAsync(PrepareTimeout, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _app.Log.Failure("local_prepare", e);
            Say(AppText.Get(AppPhrase.ActionFailed, lang), lang);
            return;
        }

        PolicyDecision decision = _app.Policy.Evaluate(prepared.Action, task);
        _app.Log.Write(new LogEvent(_app.Clock.UtcNow, LogKind.Policy, OutcomeOf(decision.Tier), Tool: call.Name, ErrorCode: decision.ReasonCode));
        ToolResult? result;
        switch (decision.Tier)
        {
            case ActionTier.Block when decision.BankHandoff:
                _app.Speech.PlayEarcon(Earcon.BankMode);
                result = await RunToolAsync(() => executor.HandOffAsync(prepared, ct), call, ct).ConfigureAwait(false);
                break;
            case ActionTier.Block:
                _app.Speech.PlayEarcon(Earcon.Error);
                Say(AgentText.Blocked(decision.ReasonCode, lang), lang);
                return;
            case ActionTier.Confirm:
                _app.Speech.PlayEarcon(Earcon.NeedsConfirmation);
                ConfirmationFacts facts = decision.Facts ?? new ConfirmationFacts { ToolName = call.Name };
                if (await _confirmer.ConfirmAsync(ConfirmationBuilder.Build(facts, lang), ct).ConfigureAwait(false) != ConfirmOutcome.Accepted)
                {
                    SayKey(StringKey.ConfirmCancelled, lang);
                    return;
                }

                result = await RunToolAsync(() => executor.ExecuteAsync(prepared, ct), call, ct).ConfigureAwait(false);
                break;
            default:
                if (LocalCommandMap.NarratesFirst(command))
                {
                    NarrateStep(executor, prepared, command, argument, lang);
                }

                result = await RunToolAsync(() => executor.ExecuteAsync(prepared, ct), call, ct).ConfigureAwait(false);
                break;
        }

        Report(command, argument, result, lang);
    }

    private void NarrateStep(IToolExecutor executor, PreparedAction prepared, LocalCommand command, string? argument, Lang lang)
    {
        string? phrase = null;
        try
        {
            phrase = executor.Narration(prepared, lang);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("local_narration", e);
        }

        if (string.IsNullOrWhiteSpace(phrase) && command == LocalCommand.OpenApp)
        {
            phrase = Strings.Get(StringKey.OpeningApp, lang, argument ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(phrase))
        {
            _app.Speech.Speak(phrase, UtteranceKind.Narration, lang, interrupt: true);
        }
    }

    private void Report(LocalCommand command, string? argument, ToolResult? result, Lang lang)
    {
        if (result is null || result.IsError)
        {
            _app.Speech.PlayEarcon(Earcon.Error);
            Say(command == LocalCommand.OpenApp
                ? Strings.Get(StringKey.AppNotFound, lang, argument ?? string.Empty)
                : AppText.Get(command == LocalCommand.SwitchWindow ? AppPhrase.NoWindows : AppPhrase.ActionFailed, lang), lang);
            return;
        }

        if (command == LocalCommand.Mute)
        {
            SayKey(StringKey.Muted, lang);
        }
        else if (LocalCommandMap.SpeaksResult(command) && !string.IsNullOrWhiteSpace(result.Content))
        {
            _app.Speech.Speak(result.Content, UtteranceKind.System, ScriptSegmenter.DominantLanguage(result.Content, lang), interrupt: true);
        }
    }

    private async Task<ToolResult?> RunToolAsync(Func<Task<ToolResult>> run, ToolCall call, CancellationToken ct)
    {
        long started = _app.Clock.NowMs;
        try
        {
            ToolResult result = await run().WaitAsync(ExecuteTimeout, ct).ConfigureAwait(false);
            _app.Log.Event(LogKind.ToolCall, result.IsError ? LogOutcome.Failed : LogOutcome.Ok, call.Name, _app.Clock.NowMs - started);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            _app.Log.Event(LogKind.ToolCall, LogOutcome.TimedOut, call.Name, _app.Clock.NowMs - started);
            return null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("local_tool", e);
            return null;
        }
    }

    private void ChangeRate(int delta, Lang lang)
    {
        int before = _app.Speech.Rate;
        int after = Math.Clamp(before + delta, 0, 100);
        if (after == before)
        {
            Say(AppText.Get(AppPhrase.SpeedAtLimit, lang), lang);
            return;
        }

        _app.Speech.Rate = after;
        _app.Settings.Update(s => s with { SpeechRate = after });
        string text = Strings.Get(delta > 0 ? StringKey.SpeakingFaster : StringKey.SpeakingSlower, lang);
        if (_app.Speech.NvdaActive)
        {
            text += " " + AppText.Get(AppPhrase.NvdaSpeedHint, lang);
        }

        Say(text, lang);
    }

    private bool SpeakBatteryLocally(Lang lang)
    {
        var status = NativeQueries.PowerStatus();
        if (status is not { } s)
        {
            return false;
        }

        if (!s.HasBattery)
        {
            SayKey(StringKey.NoBattery, lang);
            return true;
        }

        string text = s.Percent is int p ? Strings.Get(StringKey.BatteryLevel, lang, p) : string.Empty;
        if (s.Charging)
        {
            text = (text + " " + Strings.Get(StringKey.BatteryCharging, lang)).Trim();
        }

        if (text.Length == 0)
        {
            return false;
        }

        Say(text, lang);
        return true;
    }

    private void SpeakSettings(Lang lang)
    {
        var s = _app.Settings.Settings;
        string talkKey = NativeQueries.KeyName(s.TalkKey.ScanCode, s.TalkKey.Extended) ?? "Right Ctrl";
        string text = AppText.Get(
            AppPhrase.SettingsSummary,
            lang,
            s.SpeechRate,
            AppText.Get(AppText.VerbosityName(s.Verbosity), lang),
            talkKey,
            Math.Round(_app.Ledger.MonthToDateUsd, 2),
            Math.Round(_app.Ledger.LimitUsd, 2),
            AppText.Get(s.PrivateMode ? AppPhrase.On : AppPhrase.Off, lang));
        Say(text, lang);
    }

    private void SayKey(StringKey key, Lang lang, params object[] args) => Say(Strings.Get(key, lang, args), lang);

    private void Say(string text, Lang lang) => _app.Speech.Speak(text, UtteranceKind.System, lang, interrupt: true);

    private static LogOutcome OutcomeOf(ActionTier tier) => tier switch
    {
        ActionTier.Block => LogOutcome.Blocked,
        _ => LogOutcome.Ok,
    };
}
