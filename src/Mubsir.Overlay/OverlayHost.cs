using System.Windows;
using System.Windows.Threading;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>
/// Normal mode's companion: the notch at the top of the screen and Hudhud in the corner. Runs on its own
/// UI thread, so it never waits on the host and the host never waits on it. All methods can be called
/// from any thread. Events are raised on the overlay's thread, so handlers should hand work off quickly.
/// This is only the view: the app decides what a click or a key press means.
/// </summary>
public sealed class OverlayHost : IDisposable
{
    private const int MaxFailures = 3;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private Companion? _companion;
    private Exception? _startFailure;
    private int _failures;

    private OverlayHost()
    {
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Mubsir overlay" };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    /// <summary>Something was chosen in the panel Hudhud opens when it is clicked.</summary>
    public event Action<CompanionAction>? ActionChosen;

    /// <summary>The notch was clicked: start talking.</summary>
    public event Action? NotchClicked;

    /// <summary>The Stop pill was pressed during Doing.</summary>
    public event Action? StopRequested;

    /// <summary>The on-screen "Press Ctrl" button was pressed during a payment.</summary>
    public event Action? ConfirmTapRequested;

    /// <summary>The Cancel button was pressed during a payment.</summary>
    public event Action? CancelRequested;

    /// <summary>Something went wrong inside the overlay. It carries on, or closes itself if it keeps failing,
    /// and never takes the app down: the voice agent must keep working without the visuals.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Shows the windows and returns when they are up.</summary>
    public static OverlayHost Start()
    {
        var host = new OverlayHost();
        host._thread.Start();
        host._ready.Wait();
        if (host._startFailure is { } failure)
        {
            host.Dispose();
            throw new InvalidOperationException("the overlay could not start", failure);
        }

        return host;
    }

    public void Show(CompanionState state, CompanionContent? content = null) => Post(c => c.Go(state, content));

    /// <summary>Voice level 0–1 from the microphone or the voice being played.</summary>
    public void SetVoiceLevel(double level) => Post(c => c.Notch.SetVoiceLevel(level));

    /// <summary>Lets the voice wave move by itself, for the demo.</summary>
    public void UseDemoVoice() => Post(c => c.Notch.DemoVoice = true);

    /// <summary>Wakes Hudhud if it sleeps. The app decides what Ctrl does otherwise.</summary>
    public void WakeHudhud() => Post(c => c.Hudhud.Wake());

    /// <summary>A small squeeze of the notch, to feel a key press land.</summary>
    public void NudgeNotch() => Post(c => c.Notch.Nudge(0, 70));

    /// <summary>Closes the panel if it is open (the app's Esc).</summary>
    public void ClosePanel() => Post(c => c.Panel.SetOpen(false));

    public void Dispose()
    {
        Dispatcher? dispatcher = _dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
        {
            dispatcher.BeginInvoke(() =>
            {
                _companion?.Close();
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            });
        }

        if (_thread.IsAlive && Thread.CurrentThread != _thread)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _ready.Dispose();
    }

    private void Post(Action<Companion> action)
    {
        Dispatcher? dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            if (_companion is { } companion)
            {
                action(companion);
            }
        });
    }

    private void ThreadMain()
    {
        try
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _dispatcher.UnhandledException += OnUnhandled;
            var companion = new Companion(OverlayAssets.Load());
            companion.Notch.Clicked += () => NotchClicked?.Invoke();
            companion.Notch.StopClicked += () => StopRequested?.Invoke();
            companion.Notch.TapClicked += () => ConfirmTapRequested?.Invoke();
            companion.Notch.CancelClicked += () => CancelRequested?.Invoke();
            companion.Panel.ActionChosen += action => ActionChosen?.Invoke(action);
            companion.Open();
            _companion = companion;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _startFailure = e;
            _ready.Set();
            return;
        }

        _ready.Set();
        Dispatcher.Run();
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Failed?.Invoke(e.Exception);
        if (++_failures >= MaxFailures)
        {
            _companion?.Close();
            _companion = null;
        }
    }

    /// <summary>The two windows and the clock that drives them, on the overlay's thread.</summary>
    private sealed class Companion
    {
        private readonly FrameLoop _loop = new();
        private bool _started;

        public Companion(OverlayAssets assets)
        {
            // Respects Windows' "Show animations" setting: values jump instead of moving.
            bool reduceMotion = !SystemParameters.ClientAreaAnimation;
            Notch = new NotchWindow(assets, _loop, reduceMotion);
            Hudhud = new HudhudWindow(assets, _loop, reduceMotion);
            Panel = new PanelWindow(assets, _loop, reduceMotion);
            _loop.Add(Notch);
            _loop.Add(Hudhud);
            _loop.Add(Panel);
            Hudhud.Clicked += Panel.Toggle;
            Panel.OpenChanged += Hudhud.Hold;
            Panel.CursorOnOpener = () => Hudhud.CursorOver;
            Notch.Hovered += Hudhud.Peek;
            Hudhud.VoiceSource = () => Notch.VoiceValue;
            Hudhud.AsleepChanged += Notch.SetAsleep;
        }

        public NotchWindow Notch { get; }

        public HudhudWindow Hudhud { get; }

        public PanelWindow Panel { get; }

        public void Open()
        {
            Notch.Show();
            Hudhud.Show();
            Panel.Show();
            _loop.Wake();

            // The notch comes out of the top edge, then Hudhud hops in.
            var start = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
            start.Tick += (_, _) =>
            {
                start.Stop();
                Hudhud.Enter();
                if (!_started)
                {
                    Notch.Go(CompanionState.Idle, null);
                }
            };
            start.Start();
        }

        public void Go(CompanionState state, CompanionContent? content)
        {
            _started = true;
            Panel.SetOpen(false);
            Notch.Go(state, content);
            Hudhud.Go(state);
        }

        public void Close()
        {
            _loop.Stop();
            Notch.Close();
            Hudhud.Close();
            Panel.Close();
        }
    }
}
