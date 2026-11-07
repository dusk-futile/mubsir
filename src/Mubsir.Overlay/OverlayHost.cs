using System.Windows;
using System.Windows.Threading;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>
/// The notch at the top of the screen, which shows anyone watching what Mubsir is doing. Runs on its own
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

    /// <summary>How many frames have been drawn so far, for measuring the frame rate.</summary>
    public long FramesDrawn => _companion?.Frames ?? 0;

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
    public void UseDemoVoice(bool on = true) => Post(c => c.Notch.DemoVoice = on);

    /// <summary>A small squeeze of the notch, to feel a key press land.</summary>
    public void NudgeNotch() => Post(c => c.Notch.Nudge(0, 70));

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

    /// <summary>The notch and the clock that drives it, on the overlay's thread.</summary>
    private sealed class Companion
    {
        private readonly FrameLoop _loop = new();
        private readonly DispatcherTimer _fullScreen = new() { Interval = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer _rest = new() { Interval = TimeSpan.FromSeconds(20) };
        private CompanionState _state = CompanionState.Idle;
        private bool _started;
        private bool _hidden;

        public Companion(OverlayAssets assets)
        {
            // Respects Windows' "Show animations" setting: values jump instead of moving.
            bool reduceMotion = !SystemParameters.ClientAreaAnimation;
            Notch = new NotchWindow(assets, _loop, reduceMotion);
            _loop.Add(Notch);
            _fullScreen.Tick += (_, _) => WatchFullScreen();

            // After a while with nothing to show, the notch rests.
            _rest.Tick += (_, _) =>
            {
                _rest.Stop();
                Notch.SetAsleep(true);
            };
        }

        public NotchWindow Notch { get; }

        public long Frames => _loop.Frames;

        public void Open()
        {
            Notch.Show();
            _loop.Wake();

            // The notch comes out of the top edge.
            var start = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
            start.Tick += (_, _) =>
            {
                start.Stop();
                if (!_started)
                {
                    Notch.Go(CompanionState.Idle, null);
                }
            };
            start.Start();
            _fullScreen.Start();
        }

        public void Go(CompanionState state, CompanionContent? content)
        {
            _started = true;
            _state = state;
            if (state != CompanionState.Idle)
            {
                SetHidden(false);
            }

            Notch.Go(state, content);
            if (state == CompanionState.Idle)
            {
                _rest.Start();
            }
            else
            {
                _rest.Stop();
                Notch.SetAsleep(false);
            }
        }

        /// <summary>Stays out of the way of a movie, a game or a slideshow while there is nothing to show,
        /// and comes back the moment there is.</summary>
        private void WatchFullScreen()
        {
            SetHidden(_state == CompanionState.Idle && Win32.FullScreenAppRunning());
        }

        private void SetHidden(bool hidden)
        {
            if (hidden == _hidden)
            {
                return;
            }

            _hidden = hidden;
            if (hidden)
            {
                Notch.Hide();
            }
            else
            {
                Notch.Show();
            }

            _loop.Paused = hidden;
        }

        public void Close()
        {
            _fullScreen.Stop();
            _rest.Stop();
            _loop.Stop();
            Notch.Close();
        }
    }
}
