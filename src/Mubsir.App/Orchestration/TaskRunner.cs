using Mubsir.Core.Agent;
using Mubsir.Core.Brain;
using Mubsir.Core.Common;
using Mubsir.Core.Context;
using Mubsir.Core.Cost;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Policy;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal sealed class TaskRunner
{
    private static readonly TimeSpan ContextWait = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan ThinkingDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan QuietSlice = TimeSpan.FromSeconds(8);

    private readonly AppServices _app;
    private readonly Narrator _narrator;
    private readonly Confirmer _confirmer;
    private readonly CloudConsent _consent;
    private readonly AgentLoop? _loop;
    private readonly object _gate = new();
    private Running? _current;
    private Running? _agentOwner;

    public TaskRunner(AppServices app, Narrator narrator, Confirmer confirmer, CloudConsent consent, IUsageSink usage)
    {
        _app = app;
        _narrator = narrator;
        _confirmer = confirmer;
        _consent = consent;
        if (app.Brain is not null)
        {
            _loop = new AgentLoop(app.Brain, app.Executor ?? UnavailableToolExecutor.Instance, app.Policy, confirmer, narrator, usage, app.Clock);
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _current is not null;
            }
        }
    }

    public async Task ProcessAsync(string text, Task<(ScreenContext Screen, BuiltContext Built)?>? context, CancellationToken ct)
    {
        MubsirSettingsSnapshot s = Snapshot();
        Lang lang = ScriptSegmenter.DominantLanguage(text, s.UiLang);

        if (_loop is null || !_app.HasBrain)
        {
            _app.SayFailure(FailureState.NoApiKey);
            return;
        }

        if (s.PrivateMode)
        {
            Say(StringKey.PrivateModeBlocked, lang);
            return;
        }

        if (!await _consent.EnsureAsync(ct).ConfigureAwait(false))
        {
            return;
        }

        if (Cancel())
        {
            Say(AppPhrase.ReplacingTask, lang);
        }

        var running = new Running();
        lock (_gate)
        {
            _current?.Cts.Cancel();
            _current = running;
        }

        try
        {
            await RunAsync(text, lang, context, running, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("task", e);
            _app.SayBrainError(BrainErrorKind.Other, lang);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_current, running))
                {
                    _current = null;
                }
            }

            running.Cts.Dispose();
        }
    }

    public bool Cancel()
    {
        Running? running;
        lock (_gate)
        {
            running = _current;
            _current = null;
        }

        if (running is null)
        {
            return false;
        }

        try
        {
            running.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        running.Control.Resume();
        return true;
    }

    public void Pause()
    {
        Running? running = CurrentOrNull();
        running?.Control.Pause();
        _app.Input.SetTaskPaused(running is not null);
    }

    public void Resume()
    {
        CurrentOrNull()?.Control.Resume();
        _app.Input.SetTaskPaused(false);
    }

    private Running? CurrentOrNull()
    {
        lock (_gate)
        {
            return _current;
        }
    }

    private async Task RunAsync(string text, Lang lang, Task<(ScreenContext Screen, BuiltContext Built)?>? context, Running running, CancellationToken appCt)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(appCt, running.Cts.Token);
        CancellationToken ct = linked.Token;
        MubsirSettingsSnapshot s = Snapshot();

        (ScreenContext Screen, BuiltContext Built)? captured = await AwaitContextAsync(context, ct).ConfigureAwait(false);
        TaskContext task = TaskContexts.Create(text, lang, _app.Settings.Settings);
        ScreenContext? screen = captured?.Screen;
        if (screen is not null && SensitiveDetector.Evaluate(screen, task.BankDomains).Sensitive)
        {
            task.SensitiveMode = true;
        }

        if (!_app.Policy.AllowCloud(task, screen))
        {
            _app.Log.Event(LogKind.Policy, LogOutcome.Blocked, errorCode: "bank_mode");
            _app.Speech.PlayEarcon(Earcon.BankMode);
            _app.Speech.Speak(Strings.Get(StringKey.BankModeOn, lang) + " " + AppText.Get(AppPhrase.BankOfferLocal, lang), UtteranceKind.System, lang, interrupt: true);
            return;
        }

        if (!await BudgetAllowsAsync(lang, ct).ConfigureAwait(false))
        {
            return;
        }

        BrainMode mode = RequestModeClassifier.Classify(text);
        if (mode == BrainMode.Agent && _app.Executor is null)
        {
            mode = BrainMode.QuickAnswer;
        }

        if (screen is not null && PolicyEngine.MarksUntrusted(ToolNames.ReadScreen, screen))
        {
            task.UntrustedContentSeen = true;
        }

        var options = new BrainSessionOptions(
            mode,
            SystemPrompt.Build(lang, s.ReplyStyle, s.Verbosity),
            ProfileBlock.Build(ProfileFields.From(_app.Settings.Profile), SafeMemory(), _app.Settings.Profile.Apps ?? []),
            mode == BrainMode.Agent ? ToolCatalog.All : [],
            AllowWebSearch: true,
            lang);
        var request = new AgentRequest(
            text,
            captured?.Built.Text,
            InitialImage: null,
            task,
            options,
            MaxSteps: Math.Max(1, s.MaxSteps),
            MaxSeconds: Math.Max(10, s.MaxSeconds));

        long started = _app.Clock.NowMs;
        AgentOutcome outcome = await RunLoopAsync(request, running, lang, ct).ConfigureAwait(false);
        _app.Log.Write(new LogEvent(
            _app.Clock.UtcNow,
            LogKind.BrainRequest,
            OutcomeOf(outcome.End),
            Tool: mode == BrainMode.Agent ? "agent_task" : "quick_answer",
            LatencyMs: _app.Clock.NowMs - started,
            ErrorCode: outcome.Error?.ToString().ToLowerInvariant() ?? outcome.End.ToString().ToLowerInvariant(),
            Metric: "steps",
            Value: outcome.Steps));
        Report(outcome, lang);
    }

    private async Task<AgentOutcome> RunLoopAsync(AgentRequest request, Running running, Lang lang, CancellationToken ct)
    {
        lock (_gate)
        {
            _agentOwner = running;
        }

        _app.Input.SetAgentRunning(true);
        _narrator.BeginTask(lang);

        using Timer? quiet = request.Options.Mode == BrainMode.Agent
            ? new Timer(_ => RefreshQuiet(running), null, TimeSpan.Zero, QuietSlice / 2)
            : null;
        _ = ThinkingTickAsync(ct);
        try
        {
            AgentOutcome outcome = await _loop!.RunAsync(request, running.Control, ct).ConfigureAwait(false);
            if (outcome is { End: AgentEnd.BrainError, Error: BrainErrorKind.RateLimited or BrainErrorKind.Overloaded, Steps: 0 }
                && !_narrator.HasOutput)
            {
                _app.Speech.Speak(Strings.Get(StringKey.AiBusyRetrying, lang), UtteranceKind.System, lang);
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
                _narrator.BeginTask(lang);
                outcome = await _loop.RunAsync(request, running.Control, ct).ConfigureAwait(false);
            }

            return outcome;
        }
        finally
        {
            bool owner;
            lock (_gate)
            {
                owner = ReferenceEquals(_agentOwner, running);
                if (owner)
                {
                    _agentOwner = null;
                }
            }

            if (owner)
            {
                _app.Input.SetAgentRunning(false);
                _app.Quiet.Clear();
            }
        }
    }

    private void RefreshQuiet(Running running)
    {
        if (running.Control.IsPaused || _confirmer.IsPending)
        {
            _app.Quiet.Clear();
        }
        else
        {
            _app.Quiet.QuietFor(QuietSlice);
        }
    }

    private void Report(AgentOutcome outcome, Lang lang)
    {
        switch (outcome.End)
        {
            case AgentEnd.Completed when outcome.Steps > 0 && string.IsNullOrWhiteSpace(outcome.FinalText):
                _app.Speech.PlayEarcon(Earcon.Done);
                _app.Speech.Speak(Strings.Get(StringKey.Done, lang), UtteranceKind.System, lang);
                break;
            case AgentEnd.Completed when outcome.Steps > 0:
                _app.Speech.PlayEarcon(Earcon.Done);
                break;
            case AgentEnd.BrainError:
                _app.SayBrainError(outcome.Error ?? BrainErrorKind.Other, lang);
                break;
        }
    }

    private async Task<bool> BudgetAllowsAsync(Lang lang, CancellationToken ct)
    {
        CostLedger ledger = _app.Ledger;
        if (ledger.State == BudgetState.Reached)
        {
            _app.Speech.PlayEarcon(Earcon.Error);
            ConfirmOutcome answer = await _confirmer.ConfirmAsync(Strings.Get(StringKey.BudgetReached, lang), ct).ConfigureAwait(false);
            if (answer != ConfirmOutcome.Accepted)
            {
                _app.Log.Event(LogKind.Budget, LogOutcome.Declined);
                Say(StringKey.LocalStillWorks, lang);
                return false;
            }

            ledger.AllowExtra(1m);
            _app.SaveCost();
            _app.Log.Event(LogKind.Budget, LogOutcome.Ok, tool: "allow_extra");
            Say(StringKey.BudgetExtraAllowed, lang);
        }
        else if (ledger.TryClaimWarningAnnouncement())
        {
            _app.SaveCost();
            _app.Speech.Speak(Strings.Get(StringKey.BudgetWarning80, lang, ledger.MonthToDateUsd), UtteranceKind.System, lang);
        }

        return true;
    }

    private async Task<(ScreenContext Screen, BuiltContext Built)?> AwaitContextAsync(Task<(ScreenContext Screen, BuiltContext Built)?>? context, CancellationToken ct)
    {
        if (context is null)
        {
            return null;
        }

        try
        {
            return await context.WaitAsync(ContextWait, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _app.Log.Error("context_timeout", LogKind.BrainRequest, LogOutcome.TimedOut);
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
        {
            _app.Log.Failure("context", e);
            return null;
        }
    }

    private async Task ThinkingTickAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(ThinkingDelay, ct).ConfigureAwait(false);
            if (!_narrator.HasOutput && !_app.Speech.IsSpeaking && !_confirmer.IsPending)
            {
                _app.Speech.PlayEarcon(Earcon.Thinking);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private string SafeMemory()
    {
        try
        {
            return _app.Memory.ReadAll();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("memory_read", e);
            return string.Empty;
        }
    }

    private void Say(StringKey key, Lang lang) => _app.Speech.Speak(Strings.Get(key, lang), UtteranceKind.System, lang, interrupt: true);

    private void Say(AppPhrase phrase, Lang lang) => _app.Speech.Speak(AppText.Get(phrase, lang), UtteranceKind.System, lang, interrupt: true);

    private static LogOutcome OutcomeOf(AgentEnd end) => end switch
    {
        AgentEnd.Completed => LogOutcome.Ok,
        AgentEnd.Cancelled => LogOutcome.Cancelled,
        AgentEnd.PolicyStopped => LogOutcome.Blocked,
        AgentEnd.StepLimit or AgentEnd.TimeLimit => LogOutcome.TimedOut,
        _ => LogOutcome.Failed,
    };

    private MubsirSettingsSnapshot Snapshot()
    {
        var settings = _app.Settings.Settings;
        var profile = _app.Settings.Profile;
        return new MubsirSettingsSnapshot(
            profile.PreferredLanguage,
            settings.PrivateMode,
            profile.ReplyStyle,
            settings.Verbosity,
            settings.AgentMaxSteps,
            settings.AgentMaxSeconds);
    }

    private sealed record MubsirSettingsSnapshot(Lang UiLang, bool PrivateMode, ReplyStyle ReplyStyle, Verbosity Verbosity, int MaxSteps, int MaxSeconds);

    private sealed class Running
    {
        public CancellationTokenSource Cts { get; } = new();

        public AgentControl Control { get; } = new();
    }
}
