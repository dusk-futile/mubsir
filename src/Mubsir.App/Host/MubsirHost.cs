using System.Diagnostics;
using System.Threading.Channels;
using Mubsir.App.Actions;
using Mubsir.App.Screen.Documents;
using Mubsir.App.Input;
using Mubsir.App.Native;
using Mubsir.App.Ui;
using Mubsir.Core.Agent;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Cost;
using Mubsir.Core.Input;
using Mubsir.Core.Logging;
using Mubsir.Core.Policy;

namespace Mubsir.App.Host;

internal sealed class MubsirHost
{
    private readonly FileEventLog _log;
    private readonly IClock _clock;
    private readonly CommandLine _cmd;
    private uint _mainThreadId;

    private MubsirHost(FileEventLog log, IClock clock, CommandLine cmd)
    {
        _log = log;
        _clock = clock;
        _cmd = cmd;
    }

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
            return new MubsirHost(log, clock, cmd).RunCore(processStartMs);
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
        _log.Event(LogKind.Startup, tool: "start");

        using var settings = new SettingsProvider(_log);
        settings.StartWatching();
        var secrets = new SecretStore(_log);
        var memory = new MemoryStore(_log, _clock, settings);
        var costStore = new CostStore(_log);
        CostLedger ledger = costStore.Load(_clock, settings.Settings.BudgetSoftCapUsd);
        var state = new AppStateStore(_log);

        var nvda = new NvdaController();
        using var oneCore = new OneCoreSpeech(_log);
        using var speech = new SpeechService(nvda, oneCore, settings, _log, _clock);
        using var recorder = new WaveInRecorder(_log);
        var input = new InputStateMachine(InputSettingsFrom(settings.Settings), _clock);
        var actions = Channel.CreateUnbounded<InputAction>(new UnboundedChannelOptions { SingleReader = true });

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
            Report = report,
        };
        appRef = app;
        var narrator = new Narrator(app);
        var confirmer = new Confirmer(app);
        var prompter = new UserPrompter(app);
        var usage = new UsageSink(app);

        UiaScreenReader? screen = Create("screen", () => new UiaScreenReader(_log));
        app.Screen = screen;
        app.Ears = Create("ears", () => new GeminiEars(secrets, settings, _log));
        app.Brain = Create<IBrain>("brain", () => new ClaudeBrain(secrets, settings, _log));
        if (screen is not null)
        {
            app.Executor = Create<IToolExecutor>("executor", () => new WindowsToolExecutor(
                screen, new DocumentReader(screen, _log), new ReadingPositions(), app.Brain, speech, settings, memory, prompter, usage, _log));
        }

        using var hook = new KeyboardHookService(input, actions.Writer, _log) { FastStop = speech.RequestStopFast };
        using var mubsir = new MubsirApp(app, actions.Reader, narrator, confirmer, prompter, usage);
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

        using var foreground = new ForegroundWatcher(app, mubsir.Tasks, hook);
        foreground.Start();
        using var session = new SessionWatcher(app, mubsir.Tasks, hook);
        session.Start(hostWindow.Handle);
        hostWindow.SessionChanged += session.OnSessionChange;
        using var nvdaWatcher = new NvdaWatcher(app, hook);
        nvdaWatcher.Start();

        using var tray = new TrayIcon(
            new TrayActions(
                () => mubsir.IsPaused,
                mubsir.SetPaused,
                () => OpenSettingsFile(),
                mubsir.SaveReport,
                () => User32.PostQuitMessage(0),
                () => User32.PostQuitMessage(0)),
            app.UiLang,
            _log);
        tray.Start();


        if (!hook.Start())
        {
            _log.Error("hook_start_failed", LogKind.Hotkey);
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestQuit();
        };

        mubsir.Start(_clock.NowMs - processStartMs, _cmd.Recovered);
        int code = NativeWindow.RunMessageLoop();

        _log.Event(LogKind.Shutdown, code == 0 ? LogOutcome.Ok : LogOutcome.Failed);
        speech.Stop();
        app.Quiet.Clear();
        return code == 0 ? 0 : 1;
    }

    private T? Create<T>(string what, Func<T> factory)
        where T : class
    {
        try
        {
            return factory();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure(what + "_create", e);
            return null;
        }
    }

    private static InputSettings InputSettingsFrom(MubsirSettings s)
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
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
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
