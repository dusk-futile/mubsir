using System.Diagnostics;
using System.Threading.Channels;
using Basira.App.Abstractions;
using Basira.App.Audio;
using Basira.App.Composition;
using Basira.App.Diagnostics;
using Basira.App.Input;
using Basira.App.Native;
using Basira.App.Orchestration;
using Basira.App.Speech;
using Basira.App.Storage;
using Basira.App.Ui;
using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Config;
using Basira.Core.Cost;
using Basira.Core.Input;
using Basira.Core.Logging;
using Basira.Core.Policy;

namespace Basira.App.Host;

/// <summary>
/// Composition root: creates the host services, registers them for the screen/brain/actions modules, resolves the
/// optional ones (missing means local-only), starts the keyboard hook, watchers, tray icon and the app, then runs the
/// main thread's message loop until WM_QUIT.
/// </summary>
internal sealed class BasiraHost
{
    private static readonly TimeSpan RamSampleInterval = TimeSpan.FromMinutes(10);

    private readonly FileEventLog _log;
    private readonly IClock _clock;
    private readonly CommandLine _cmd;
    private uint _mainThreadId;

    private BasiraHost(FileEventLog log, IClock clock, CommandLine cmd)
    {
        _log = log;
        _clock = clock;
        _cmd = cmd;
    }

    /// <summary>Runs Basira. Returns 0 for an intentional exit, non-zero on failure (the supervisor then restarts it).</summary>
    /// <param name="processStartMs">Monotonic time when the process started, for the cold-start metric.</param>
    public static int Run(CommandLine cmd, long processStartMs)
    {
        AppPaths.EnsureCreated();
        IClock clock = SystemClock.Instance;
        using var log = new FileEventLog(AppPaths.LogsDir, clock);
        log.Prune();
        NativeWindow.OnHandlerError = e => log.Failure("wndproc", e);
        WinEventHook.OnHandlerError = e => log.Failure("winevent", e);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Error("unhandled:" + (e.ExceptionObject?.GetType().Name ?? "unknown"));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Failure("unobserved_task", e.Exception.InnerException ?? e.Exception);
            e.SetObserved();
        };

        try
        {
            return new BasiraHost(log, clock, cmd).RunCore(processStartMs);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Failure("host_fatal", e);
            return 1;
        }
    }

    private int RunCore(long processStartMs)
    {
        _mainThreadId = Kernel32.GetCurrentThreadId();
        _log.Event(LogKind.Startup, tool: _cmd.Dev ? "start_dev" : "start");

        // Storage and settings.
        using var settings = new SettingsProvider(_log);
        settings.StartWatching();
        var secrets = new SecretStore(_log);
        var memory = new MemoryStore(_log, _clock, settings);
        var costStore = new CostStore(_log);
        CostLedger ledger = costStore.Load(_clock, settings.Settings.BudgetSoftCapUsd);
        var state = new AppStateStore(_log);

        // Voice, microphone, keyboard state.
        var nvda = new NvdaController();
        using var oneCore = new OneCoreSpeech(_log);
        using var speech = new SpeechService(nvda, oneCore, settings, _log, _clock);
        using var recorder = new WaveInRecorder(_log);
        var input = new InputStateMachine(InputSettingsFrom(settings.Settings), _clock);
        var actions = Channel.CreateUnbounded<InputAction>(new UnboundedChannelOptions { SingleReader = true });
        using var metrics = new Metrics(_log, _clock);

        AppServices? appRef = null;
        var report = new DiagnosticsReport(_log, settings, _clock, () => SystemFacts.Collect(
            () => nvda.IsRunning(),
            () => appRef?.HasSecret(SecretNames.Anthropic) ?? false,
            () => appRef?.HasSecret(SecretNames.Gemini) ?? false,
            () => settings.Settings.PrivateMode));
        var app = new AppServices
        {
            Clock = _clock,
            Log = _log,
            Settings = settings,
            Secrets = secrets,
            Memory = memory,
            Speech = speech,
            Nvda = nvda,
            Quiet = new NvdaQuiet(_clock, _log),
            Recorder = recorder,
            Input = input,
            Policy = new PolicyEngine(),
            Ledger = ledger,
            CostStore = costStore,
            State = state,
            Metrics = metrics,
            Report = report,
        };
        appRef = app;
        var narrator = new Narrator(app);
        var confirmer = new Confirmer(app);
        var prompter = new UserPrompter(app);
        var usage = new UsageSink(app);

        ServiceRegistry registry = Register(app, narrator, confirmer, prompter, usage);
        ResolveOptional(registry, app);

        // The app, the keyboard hook and the main-thread windows.
        using var hook = new KeyboardHookService(input, actions.Writer, _log) { FastStop = speech.RequestStopFast };
        using var basira = new BasiraApp(app, actions.Reader, narrator, confirmer, prompter, usage);
        using var hostWindow = new HostWindow(_log);
        if (hostWindow.Create())
        {
            hostWindow.RegisterStopHotkey();
            hostWindow.StopHotkey += () =>
            {
                actions.Writer.TryWrite(new StopSpeech());
                actions.Writer.TryWrite(new TaskStop());
            };
        }

        using var foreground = new ForegroundWatcher(app, basira.Tasks, hook);
        foreground.Start();
        using var session = new SessionWatcher(app, basira.Tasks, hook);
        session.Start(hostWindow.Handle);
        hostWindow.SessionChanged += session.OnSessionChange;
        using var nvdaWatcher = new NvdaWatcher(app, hook);
        nvdaWatcher.Start();

        using var tray = new TrayIcon(
            new TrayActions(
                () => basira.IsPaused,
                basira.SetPaused,
                () => OpenSettingsFile(),
                basira.SaveReport,
                () => User32.PostQuitMessage(0),
                () => User32.PostQuitMessage(0)),
            app.UiLang,
            _log);
        tray.Start();

        using DevPipe? devPipe = _cmd.Dev
            ? new DevPipe(new DevPipeHandlers(hook.InjectForDev, basira.SubmitTypedAsync, basira.RunLocalCommandAsync, input.ToString), _log)
            : null;
        devPipe?.Start();

        if (!hook.Start())
        {
            // The always-on hotkey and the voice still work; the hook is retried every 30 s.
            _log.Error("hook_start_failed", LogKind.Hotkey);
        }

        metrics.StartRamSampling(RamSampleInterval);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestQuit();
        };

        basira.Start(_clock.NowMs - processStartMs, _cmd.Recovered);
        int code = NativeWindow.RunMessageLoop();

        _log.Event(LogKind.Shutdown, code == 0 ? LogOutcome.Ok : LogOutcome.Failed);
        metrics.LogSummaries();
        speech.Stop();
        app.Quiet.Clear();
        return code == 0 ? 0 : 1;
    }

    /// <summary>The host's services, for the modules that implement the screen, brain and actions side.</summary>
    private ServiceRegistry Register(AppServices app, Narrator narrator, Confirmer confirmer, UserPrompter prompter, UsageSink usage)
    {
        var registry = new ServiceRegistry();
        registry.AddSingleton(app.Clock);
        registry.AddSingleton<IEventLog>(app.Log);
        registry.AddSingleton(app.Settings);
        registry.AddSingleton(app.Secrets);
        registry.AddSingleton(app.Memory);
        registry.AddSingleton(app.Ledger);
        registry.AddSingleton<ISpeechService>(app.Speech);
        registry.AddSingleton(app.Recorder);
        registry.AddSingleton<IUserPrompter>(prompter);
        registry.AddSingleton<IConfirmer>(confirmer);
        registry.AddSingleton<INarrator>(narrator);
        registry.AddSingleton<IUsageSink>(usage);
        registry.AddSingleton(app.Policy);
        registry.AddSingleton(app.Input);
        try
        {
            Modules.RegisterAll(registry);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("modules_register", e);
        }

        return registry;
    }

    /// <summary>Screen, ears, brain and executor are optional: a missing or failing one leaves local features working.</summary>
    private void ResolveOptional(ServiceRegistry registry, AppServices app)
    {
        app.Screen = TryResolve<IScreenReader>(registry, "screen");
        app.Ears = TryResolve<IEars>(registry, "ears");
        app.Brain = TryResolve<IBrain>(registry, "brain");
        app.Executor = TryResolve<IToolExecutor>(registry, "executor");
    }

    private T? TryResolve<T>(ServiceRegistry registry, string what)
        where T : class
    {
        try
        {
            T? service = registry.TryGet<T>();
            if (service is null)
            {
                _log.Event(LogKind.Startup, LogOutcome.Failed, tool: what + "_missing");
            }

            return service;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure(what + "_create", e);
            return null;
        }
    }

    private static InputSettings InputSettingsFrom(BasiraSettings s)
    {
        try
        {
            InputSettings result = InputSettings.Default with { TalkKey = s.TalkKey };
            result.Validate();
            return result;
        }
        catch (ArgumentException)
        {
            return InputSettings.Default;
        }
    }

    private void OpenSettingsFile()
    {
        try
        {
            var psi = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
            psi.ArgumentList.Add(AppPaths.SettingsFile);
            using Process? _ = Process.Start(psi);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _log.Failure("open_settings", e);
        }
    }

    private void RequestQuit()
    {
        if (_mainThreadId != 0 && !User32.PostThreadMessageW(_mainThreadId, Win32.WM_QUIT, 0, 0))
        {
            _log.Error("post_quit_" + NativeQueries.LastError());
        }
    }
}
