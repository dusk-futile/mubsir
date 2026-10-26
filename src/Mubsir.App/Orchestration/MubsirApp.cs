using System.Globalization;
using System.Threading.Channels;
using Mubsir.App.Native;
using Mubsir.App.Ui;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Input;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Speech;

namespace Mubsir.App.Orchestration;

internal sealed class MubsirApp : IDisposable
{
    private const string NoKeyNoticeState = "no_key_notice_day";

    private readonly AppServices _app;
    private readonly ChannelReader<InputAction> _actions;
    private readonly Confirmer _confirmer;
    private readonly CancellationTokenSource _stopping = new();
    private readonly CloudConsent _consent;
    private readonly TaskRunner _tasks;
    private readonly ReadingController _reading;
    private readonly LocalCommandRunner _local;
    private readonly UtteranceFlow _flow;
    private bool _lastWasLayerKey;
    private int _requestBoxOpen;
    private Task? _loop;

    public MubsirApp(
        AppServices app,
        ChannelReader<InputAction> actions,
        Narrator narrator,
        Confirmer confirmer,
        UserPrompter prompter,
        UsageSink usage)
    {
        _app = app;
        _actions = actions;
        _confirmer = confirmer;
        _consent = new CloudConsent(app, confirmer);
        _tasks = new TaskRunner(app, narrator, confirmer, _consent, usage);
        _reading = new ReadingController(app, narrator);
        _local = new LocalCommandRunner(app, _reading, confirmer, _tasks, () => StopTask(speak: true), SaveReport);
        _flow = new UtteranceFlow(app, confirmer, prompter, _consent, _local, _tasks, usage, _stopping.Token);

        app.Settings.Changed += OnSettingsChanged;
    }

    public TaskRunner Tasks => _tasks;

    public bool IsPaused => _app.Settings.Settings.MubsirPaused;

    public void Start(long coldStartMs, bool recoveredFromCrash)
    {
        _loop = Task.Run(LoopAsync);
        _ = Task.Run(() => StartupAsync(coldStartMs, recoveredFromCrash));
    }



    public void SetPaused(bool paused)
    {
        if (paused)
        {
            _tasks.Cancel();
        }

        _app.Settings.Update(s => s with { MubsirPaused = paused });
        _app.Input.SetMubsirPaused(paused);
        _app.SayNow(paused ? StringKey.Paused : StringKey.Resumed);
    }

    public void SaveReport()
    {
        _ = Task.Run(() =>
        {
            string? path = _app.Report.Create();
            if (path is null)
            {
                _app.Speech.PlayEarcon(Earcon.Error);
                _app.SayNow(AppPhrase.ReportFailed);
                return;
            }

            _app.Speech.PlayEarcon(Earcon.Done);
            _app.SayNow(StringKey.ReportSaved);
            _app.Say(AppPhrase.ReportIncludes);
        });
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _tasks.Cancel();
        _app.Settings.Changed -= OnSettingsChanged;
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _stopping.Dispose();
    }

    private async Task LoopAsync()
    {
        try
        {
            await foreach (InputAction action in _actions.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    Handle(action);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    _app.Log.Failure("action_" + action.GetType().Name, e);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Handle(InputAction action)
    {
        bool afterLayerKey = _lastWasLayerKey;
        _lastWasLayerKey = action is LayerKey;
        switch (action)
        {
            case StopSpeech:
                _app.Speech.Stop();
                break;
            case TalkPreroll:
                StartRecording();
                break;
            case TalkStarted:
                _app.Speech.Stop();
                _app.Speech.PlayEarcon(Earcon.ListeningStart);
                break;
            case TalkEnded:
                _flow.OnTalkEnded();
                break;
            case TalkCancelled:
                _app.Recorder.Cancel();
                break;
            case LayerOpened:
                _app.Speech.PlayEarcon(Earcon.LayerOpen);
                break;
            case LayerClosed when !afterLayerKey:
                _app.Speech.PlayEarcon(Earcon.LayerClose);
                break;
            case LayerKey key:
                HandleLayer(key.Command);
                break;
            case TaskStop:
                StopTask(speak: true);
                break;
            case TaskPause:
                _tasks.Pause();
                _app.Speech.PlayEarcon(Earcon.Paused);
                _app.SayNow(StringKey.TaskPaused);
                break;
            case TaskResume:
                _tasks.Resume();
                break;
            case ConfirmAccepted:
                _confirmer.Signal(ConfirmSignal.Accept);
                break;
            case ConfirmRejected:
                _confirmer.Signal(ConfirmSignal.Reject);
                break;
            case ConfirmRepeat:
                _confirmer.Signal(ConfirmSignal.Repeat);
                break;
            case PausedNotice:
                _app.SayFailure(FailureState.Paused);
                break;
            case ResumeFromPaused:
                _app.Settings.Update(s => s with { MubsirPaused = false });
                _app.SayNow(StringKey.Resumed);
                break;
            case DescribeKey describe:
                DescribeKeyAloud(describe.Key);
                break;
            case KeyExplorerExited:
                _app.SayNow(StringKey.KeyExplorerExit);
                break;
        }
    }

    private void HandleLayer(LayerCommand command)
    {
        switch (command)
        {
            case LayerCommand.ReadFromHere:
                _ = Guard(_reading.ReadFromHereAsync(_stopping.Token), "layer_read");
                break;
            case LayerCommand.PreviousSentence:
                _reading.PreviousSentence();
                break;
            case LayerCommand.NextSentence:
                _reading.NextSentence();
                break;
            case LayerCommand.PreviousParagraph:
                _reading.PreviousParagraph();
                break;
            case LayerCommand.NextParagraph:
                _reading.NextParagraph();
                break;
            case LayerCommand.Repeat:
                _reading.Repeat();
                break;
            case LayerCommand.TypeRequest:
                _ = Guard(TypeRequestAsync(), "type_request");
                break;
            case LayerCommand.StopTask:
                StopTask(speak: true);
                break;
            case LayerCommand.Help:
                _app.SayNow(StringKey.LayerHelp);
                break;
        }
    }

    private void StopTask(bool speak)
    {
        _tasks.Cancel();
        _app.Speech.Stop();
        _app.Input.SetReading(false);
        if (speak)
        {
            _app.SayNow(StringKey.TaskStopped);
        }
    }

    private void StartRecording()
    {
        try
        {
            _app.Recorder.Start();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("recorder_start", e);
        }
    }

    private async Task TypeRequestAsync()
    {
        if (Interlocked.Exchange(ref _requestBoxOpen, 1) != 0)
        {
            return;
        }

        try
        {
            _app.Speech.Stop();
            RequestBoxResult result = await RequestBox.ShowAsync(_app.UiLang, _app.Log, _stopping.Token).ConfigureAwait(false);
            switch (result.Status)
            {
                case RequestBoxStatus.Submitted when !string.IsNullOrWhiteSpace(result.Text):
                    await _flow.HandleTypedAsync(result.Text).ConfigureAwait(false);
                    break;
                case RequestBoxStatus.Unavailable:
                    _app.Speech.PlayEarcon(Earcon.Error);
                    _app.SayNow(AppPhrase.TypingUnavailable);
                    break;
            }
        }
        finally
        {
            Volatile.Write(ref _requestBoxOpen, 0);
        }
    }

    private void DescribeKeyAloud(KeyEvent key)
    {
        StringKey role = KeyExplorerGuide.Describe(key, _app.Input.Settings);
        string? name = NativeQueries.KeyName(key.ScanCode, key.Extended);
        string text = Strings.Get(role, _app.UiLang);
        if (!string.IsNullOrWhiteSpace(name) && role == StringKey.KeyExplorerNoAction)
        {
            text = name + ". " + text;
        }

        _app.Speech.Speak(text, UtteranceKind.System, _app.UiLang, interrupt: true);
    }

    private async Task StartupAsync(long coldStartMs, bool recoveredFromCrash)
    {
        try
        {
            var settings = _app.Settings.Settings;
            _app.Input.SetMubsirPaused(settings.MubsirPaused);
            if (recoveredFromCrash)
            {
                _app.SayFailure(FailureState.Crashed, interrupt: false);
            }
            else
            {
                _app.Speech.PlayEarcon(Earcon.Ready);
                _app.Say(StringKey.Ready);
            }

            _app.Log.Event(LogKind.Startup, latencyMs: coldStartMs);

            if (settings.MubsirPaused)
            {
                _app.Say(StringKey.Paused);
                return;
            }

            bool anyKey = _app.HasBrain || _app.HasEars;
            if (!_app.HasBrain || !_app.HasEars)
            {
                AnnounceMissingKeyOncePerDay();
            }

            if (anyKey && !_consent.Given)
            {
                await _consent.EnsureAsync(_stopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("startup", e);
        }
    }

    private void AnnounceMissingKeyOncePerDay()
    {
        string today = _app.Clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_app.State.GetString(NoKeyNoticeState) == today)
        {
            return;
        }

        _app.State.SetString(NoKeyNoticeState, today);
        _app.SayFailure(FailureState.NoApiKey, interrupt: false);
    }

    private void OnSettingsChanged()
    {
        try
        {
            MubsirSettings s = _app.Settings.Settings;
            if (_app.Speech.Rate != s.SpeechRate)
            {
                _app.Speech.Rate = s.SpeechRate;
            }

            if (_app.Input.MubsirPaused != s.MubsirPaused)
            {
                _app.Input.SetMubsirPaused(s.MubsirPaused);
            }

            if (_app.Input.Settings.TalkKey != s.TalkKey)
            {
                _app.Input.UpdateSettings(_app.Input.Settings with { TalkKey = s.TalkKey });
            }

            _app.Ledger.SoftCapUsd = Math.Max(0m, s.BudgetSoftCapUsd);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("settings_apply", e);
        }
    }

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
        }
    }
}
