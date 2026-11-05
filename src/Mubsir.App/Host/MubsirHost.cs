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
using Mubsir.Core.Library;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;
using Mubsir.Core.Policy;
using Mubsir.Core.Reading;
using Mubsir.Core.Sessions;
using Mubsir.Core.Setup;
using Mubsir.Core.Speech;

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
        var quiet = new NvdaQuiet(_clock, _log);
        using var oneCore = new OneCoreSpeech(_log);
        AppServices? appRef = null;
        void OnCostChanged()
        {
            costStore.Save(ledger);
            if (ledger.TryClaimWarningAnnouncement())
            {
                costStore.Save(ledger);
                appRef?.Say(StringKey.BudgetWarning80, ledger.MonthToDateUsd);
            }
        }

        using var fishVoice = new FishVoice(secrets, settings, _log, ledger, OnCostChanged);
        using var localVoice = new LocalVoice(_log);
        using var speech = new SpeechService(nvda, oneCore, settings, _log, _clock, fishVoice, quiet, localVoice);
        using var recorder = new WaveInRecorder(_log);
        var input = new InputStateMachine(InputSettingsFrom(settings.Settings), _clock);
        var actions = Channel.CreateUnbounded<InputAction>(new UnboundedChannelOptions { SingleReader = true });

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
            Quiet = quiet,
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

        LibraryScanner? scanner = null;
        UiaScreenReader? screen = Create("screen", () => new UiaScreenReader(_log));
        app.Screen = screen;
        app.Ears = Create("ears", () => new EarsRouter(new GeminiEars(secrets, settings, _log), new FishEars(secrets, _log, ledger, OnCostChanged), secrets, settings, _log));
        app.Sessions = new SessionArchive(settings.Settings.SessionsFolder ?? AppPaths.DefaultChatsDir, () => _clock.UtcNow.ToLocalTime());
        localVoice.Installed += () => app.SayNow(AppPhrase.LocalVoiceReady);
        localVoice.TooSlow += () => app.SayNow(AppPhrase.LocalVoiceTooSlow);
        fishVoice.KeyRejected += status => app.SayNow(status == 402 ? AppPhrase.FishNoCredit : AppPhrase.FishKeyRejected);
        using var companion = settings.Settings.ShowNotch ? new CompanionBridge(speech, recorder, settings, _log) : null;
        app.Companion = companion;
        app.Brain = Create<IBrain>("brain", () => new BrainRouter(new ClaudeBrain(secrets, settings, _log), new GeminiBrain(secrets, settings, _log), secrets));
        if (screen is not null)
        {
            var positions = new ReadingPositions(AppPaths.PositionsFile);
            var notes = new BookNotes(AppPaths.BookNotesFile);
            positions.SaveFailed += () =>
            {
                speech.PlayEarcon(Earcon.Error);
                speech.Speak(
                    Strings.Get(StringKey.BookmarkSaveFailed, settings.Profile.PreferredLanguage),
                    UtteranceKind.System,
                    settings.Profile.PreferredLanguage,
                    interrupt: false);
            };
            notes.SaveFailed += () =>
            {
                speech.PlayEarcon(Earcon.Error);
                speech.Speak(
                    Strings.Get(StringKey.NoteSaveFailed, settings.Profile.PreferredLanguage),
                    UtteranceKind.System,
                    settings.Profile.PreferredLanguage,
                    interrupt: false);
            };
            var documents = new DocumentReader(screen, _log);
            var library = new BookLibrary(AppPaths.LibraryFile);
            var books = new BookAloud(
                speech, positions, notes, library, documents, new PageImageBudget(AppPaths.PageRepairsFile),
                () => !settings.Settings.PrivateMode && settings.Profile.CloudConsentGiven && !settings.Settings.MubsirPaused,
                input.SetReading, _log);
            app.Books = books;
            app.Executor = Create<IToolExecutor>("executor", () => new WindowsToolExecutor(
                screen, documents, positions, books, app.Brain, speech, settings, memory, prompter, usage, _log) { Companion = () => app.Companion, Voices = fishVoice });
            scanner = new LibraryScanner(library, documents, _log) { Describe = app.Executor is WindowsToolExecutor hands ? hands.DescribeBookAsync : null };
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

        using var scanStopping = new CancellationTokenSource();
        scanner?.Start(scanStopping.Token);
        _ = Task.Run(() => localVoice.InstallAsync(scanStopping.Token));
        using var foreground = new ForegroundWatcher(app, mubsir.Tasks, hook);
        foreground.Start();
        using var session = new SessionWatcher(app, mubsir.Tasks, hook);
        session.Start(hostWindow.Handle);
        hostWindow.SessionChanged += session.OnSessionChange;
        using var nvdaWatcher = new NvdaWatcher(app, hook);
        nvdaWatcher.Start();

        using var setup = new SetupCoordinator(app, secrets, prompter);
        app.OpenSetup = () => setup.Open();
        using var tray = new TrayIcon(
            new TrayActions(
                () => mubsir.IsPaused,
                mubsir.SetPaused,
                () => setup.Open(),
                mubsir.SaveReport,
                () => User32.PostQuitMessage(0),
                () => User32.PostQuitMessage(0)),
            app.UiLang,
            _log);
        tray.Start();


        if (!hook.Start())
        {
            _log.Error("hook_start_failed", LogKind.Hotkey);
            app.SayNow(AppPhrase.KeysNotWorking);
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestQuit();
        };

        mubsir.Start(_clock.NowMs - processStartMs, _cmd.Recovered);
        companion?.Start();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPower;
        void OnPower(object? _, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            // Windows can drop the keyboard hook during sleep. Waking up shouldn't need the 30-second check.
            if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                hook.RequestReinstall(HookReinstallReason.Periodic);
            }
        }

        if (settings.Profile.PreferredLanguage == Lang.Arabic && !OcrService.HasArabic)
        {
            app.SayNow(AppPhrase.NoArabicOcr);
        }

        if (settings.Profile.PreferredLanguage == Lang.Arabic && !nvda.IsRunning() && speech.ArabicVoiceMissing && !secrets.Has(SecretNames.Fish))
        {
            _ = Task.Run(async () =>
            {
                string prompt = "Arabic speech isn't installed on this PC, so I can't speak Arabic yet. Shall I open Windows speech settings so you can add it?";
                ConfirmOutcome answer = await confirmer.ConfirmAsync(prompt, CancellationToken.None).ConfigureAwait(false);
                if (answer == ConfirmOutcome.Accepted)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:speech") { UseShellExecute = true });
                    }
                    catch (Exception e) when (e is not OutOfMemoryException)
                    {
                        _log.Failure("open_speech_settings", e);
                    }
                }
            });
        }

        setup.OpenIfNeeded();

        if (settings.Settings.OnboardingDone)
        {
            _ = Task.Run(async () =>
            {
                await TestSavedKeysInBackgroundAsync(secrets, app, _log).ConfigureAwait(false);
            });
        }

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

    private static async Task TestSavedKeysInBackgroundAsync(SecretStore secrets, AppServices app, IEventLog log)
    {
        var keysToTest = new List<(KeyKind Kind, string Key, string Name)>();
        string? anthropic = secrets.Get(SecretNames.Anthropic);
        if (!string.IsNullOrWhiteSpace(anthropic))
        {
            keysToTest.Add((KeyKind.Anthropic, anthropic, "Claude"));
        }

        string? fish = secrets.Get(SecretNames.Fish);
        if (!string.IsNullOrWhiteSpace(fish))
        {
            keysToTest.Add((KeyKind.FishAudio, fish, "Fish Audio"));
        }

        string? gemini = secrets.Get(SecretNames.Gemini);
        if (!string.IsNullOrWhiteSpace(gemini))
        {
            keysToTest.Add((KeyKind.Gemini, gemini, "Gemini"));
        }

        if (keysToTest.Count == 0)
        {
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Lang lang = app.UiLang;
        bool ar = lang == Lang.Arabic;

        foreach (var (kind, key, name) in keysToTest)
        {
            KeyTest test = await KeyTester.TestAsync(kind, key, cts.Token).ConfigureAwait(false);
            if (test == KeyTest.Wrong)
            {
                string msg = ar
                    ? $"مفتاح {name} المحفوظ غير صحيح. اتأكد إنك نسخته كله."
                    : $"The saved {name} key doesn't work. Check you copied all of it.";
                app.Speech.Speak(msg, Mubsir.Core.Speech.UtteranceKind.System, lang);
            }
            else if (test == KeyTest.NoCredit)
            {
                string msg = ar
                    ? $"حساب {name} مفيش فيه رصيد كافٍ. لازم تشحن الحساب."
                    : $"The {name} account has no credit. It needs topping up.";
                app.Speech.Speak(msg, Mubsir.Core.Speech.UtteranceKind.System, lang);
            }
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

    private void RequestQuit()
    {
        if (_mainThreadId != 0 && !User32.PostThreadMessageW(_mainThreadId, Win32.WM_QUIT, 0, 0))
        {
            _log.Error("post_quit_" + NativeQueries.LastError());
        }
    }
}
