using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Context;
using Basira.Core.Localization;
using Basira.Core.Logging;
using Basira.Core.Policy;
using Basira.Core.Router;
using Basira.Core.Text;

namespace Basira.App.Orchestration;

/// <summary>
/// From the talk key's release (or a typed request) to an action: level checks on the clip, privacy gates,
/// screen context captured in parallel with transcription, then the answer to a pending question, a local command,
/// or an AI request.
/// </summary>
internal sealed class UtteranceFlow
{
    private const double SilentPeak = 0.01;
    private const int MinSpeechMs = 400;
    private static readonly TimeSpan TranscribeTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan HintWait = TimeSpan.FromMilliseconds(300);

    private readonly AppServices _app;
    private readonly Confirmer _confirmer;
    private readonly UserPrompter _prompter;
    private readonly CloudConsent _consent;
    private readonly LocalCommandRunner _local;
    private readonly TaskRunner _tasks;
    private readonly UsageSink _usage;
    private readonly CancellationToken _appStopping;

    public UtteranceFlow(
        AppServices app,
        Confirmer confirmer,
        UserPrompter prompter,
        CloudConsent consent,
        LocalCommandRunner local,
        TaskRunner tasks,
        UsageSink usage,
        CancellationToken appStopping)
    {
        _app = app;
        _confirmer = confirmer;
        _prompter = prompter;
        _consent = consent;
        _local = local;
        _tasks = tasks;
        _usage = usage;
        _appStopping = appStopping;
    }

    /// <summary>The talk key was released after a real hold: check the clip and send it on.</summary>
    public void OnTalkEnded()
    {
        RecordedAudio audio;
        try
        {
            audio = _app.Recorder.Stop();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("recorder_stop", e);
            _app.SayFailure(FailureState.MicSilent);
            return;
        }

        _app.Log.Write(new LogEvent(_app.Clock.UtcNow, LogKind.Talk, LatencyMs: audio.DurationMs, Metric: "speech_ms", Value: audio.SpeechMs));
        if (audio.Peak < SilentPeak)
        {
            _app.SayFailure(FailureState.MicSilent);
            return;
        }

        if (audio.SpeechMs < MinSpeechMs)
        {
            _app.SayNow(StringKey.HeardNothing);
            return;
        }

        _app.Speech.PlayEarcon(Earcon.ListeningEnd);
        _app.Metrics.Arm(Metrics.ReleaseToFirstAudio);
        _ = Guard(HandleAudioAsync(audio), "utterance_audio");
    }

    /// <summary>A typed request (request box or dev pipe).</summary>
    public Task HandleTypedAsync(string text) => Guard(RouteAsync(text, null, null, StartContextCapture()), "utterance_typed");

    /// <summary>A local command by name (dev pipe).</summary>
    public Task RunLocalAsync(LocalCommand command, string? argument) =>
        Guard(_local.RunAsync(command, argument, argument ?? command.ToString(), _app.UiLang, _appStopping), "local_command");

    private async Task HandleAudioAsync(RecordedAudio audio)
    {
        if (_confirmer.IsPending && !_confirmer.AcceptsVoice)
        {
            // The consent prompt itself: a spoken answer would already need the cloud.
            _app.SayNow(AppPhrase.ConsentPressEnter);
            return;
        }

        if (!_app.HasEars)
        {
            _app.SayFailure(FailureState.NoApiKey);
            return;
        }

        if (_app.Settings.Settings.PrivateMode)
        {
            _app.SayNow(AppPhrase.VoiceNeedsInternet);
            return;
        }

        if (!_consent.Given)
        {
            if (_consent.IsAsking)
            {
                _app.SayNow(AppPhrase.ConsentPressEnter);
            }
            else if (await _consent.EnsureAsync(_appStopping).ConfigureAwait(false))
            {
                _app.Say(AppPhrase.SayRequestAgain);
            }

            return;
        }

        bool answering = _confirmer.IsPending || _prompter.IsPending;
        Task<(ScreenContext Screen, BuiltContext Built)?>? context = answering ? null : StartContextCapture();
        EarsHints hints = await BuildHintsAsync(context).ConfigureAwait(false);

        EarsResult result;
        long started = _app.Clock.NowMs;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping))
        {
            cts.CancelAfter(TranscribeTimeout);
            try
            {
                result = await _app.Ears!.TranscribeAsync(audio.Wav, hints, cts.Token).ConfigureAwait(false);
            }
            catch (EarsException e)
            {
                _app.Log.Error("ears_" + e.Kind.ToString().ToLowerInvariant(), LogKind.Transcribe);
                _app.Metrics.Disarm(Metrics.ReleaseToFirstAudio);
                _app.SayBrainError(e.Kind, _app.UiLang);
                return;
            }
            catch (OperationCanceledException) when (!_appStopping.IsCancellationRequested)
            {
                _app.Log.Error("ears_timeout", LogKind.Transcribe, LogOutcome.TimedOut);
                _app.Metrics.Disarm(Metrics.ReleaseToFirstAudio);
                _app.SayBrainError(BrainErrorKind.Network, _app.UiLang);
                return;
            }
            catch (Exception e) when (e is HttpRequestException or IOException)
            {
                _app.Log.Failure("ears", e);
                _app.Metrics.Disarm(Metrics.ReleaseToFirstAudio);
                _app.SayBrainError(BrainErrorKind.Network, _app.UiLang);
                return;
            }
        }

        _app.Metrics.Record(Metrics.TranscribeMs, _app.Clock.NowMs - started);
        _app.Log.Event(LogKind.Transcribe, LogOutcome.Ok, latencyMs: _app.Clock.NowMs - started);
        if (result.Usage is { } usage)
        {
            _usage.Report(usage);
        }

        await RouteAsync(result.Transcript ?? string.Empty, result.LocalCommand, result.LocalArgument, context).ConfigureAwait(false);
    }

    private async Task RouteAsync(string text, string? localCommand, string? localArgument, Task<(ScreenContext Screen, BuiltContext Built)?>? context)
    {
        text = text.Trim();
        if (text.Length > 0 && (_confirmer.TryTakeAnswer(text) || _prompter.TryTakeAnswer(text)))
        {
            return; // an answer to a pending question, not a new request
        }

        if (text.Length == 0)
        {
            _app.Metrics.Disarm(Metrics.ReleaseToFirstAudio);
            _app.SayNow(StringKey.HeardNothing);
            return;
        }

        Lang lang = ScriptSegmenter.DominantLanguage(text, _app.UiLang);
        if (CommandGrammar.TryParseName(localCommand, out LocalCommand fromEars))
        {
            await _local.RunAsync(fromEars, localArgument, text, lang, _appStopping).ConfigureAwait(false);
            return;
        }

        if (CommandGrammar.Match(text) is { } match)
        {
            await _local.RunAsync(match.Command, match.ArgumentText ?? match.Argument, text, lang, _appStopping).ConfigureAwait(false);
            return;
        }

        await _tasks.ProcessAsync(text, context, _appStopping).ConfigureAwait(false);
    }

    /// <summary>Starts the UI Automation snapshot in parallel with transcription; null when there is no screen reader.</summary>
    private Task<(ScreenContext Screen, BuiltContext Built)?>? StartContextCapture()
    {
        if (_app.Screen is not { } screen)
        {
            return null;
        }

        return Task.Run(async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping);
            cts.CancelAfter(CaptureTimeout);
            try
            {
                var captured = await screen.CaptureContextAsync(ReadScope.Window, cts.Token).WaitAsync(cts.Token).ConfigureAwait(false);
                return ((ScreenContext Screen, BuiltContext Built)?)captured;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _app.Log.Failure("context_capture", e);
                return null;
            }
        });
    }

    /// <summary>Only the preferred language, dialect, app name and focused element name leave the PC as hints.</summary>
    private async Task<EarsHints> BuildHintsAsync(Task<(ScreenContext Screen, BuiltContext Built)?>? context)
    {
        var profile = _app.Settings.Profile;
        string? appName = null;
        string? focusName = null;
        if (context is not null)
        {
            try
            {
                if (await context.WaitAsync(HintWait).ConfigureAwait(false) is { } captured)
                {
                    ScreenContext screen = captured.Screen;
                    bool sensitive = SensitiveDetector.Evaluate(screen, _app.Settings.Settings.BankDomains ?? []).Sensitive;
                    if (!sensitive)
                    {
                        appName = screen.AppName ?? screen.ProcessName;
                        focusName = screen.Focused is { } f && !SensitiveDetector.IsSensitiveField(f) ? f.Name : null;
                    }
                }
            }
            catch (TimeoutException)
            {
                // Not ready yet: the transcription does not wait for hints.
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _app.Log.Failure("hints", e);
            }
        }

        return new EarsHints(
            profile.PreferredLanguage,
            profile.Dialect,
            Trim(appName),
            Trim(focusName),
            CommandGrammar.KnownLocalCommandNames);
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Length <= 80 ? s.Trim() : s[..80].Trim();

    private async Task Guard(Task work, string where)
    {
        try
        {
            await work.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure(where, e);
            _app.Speech.PlayEarcon(Earcon.Error);
        }
    }
}
