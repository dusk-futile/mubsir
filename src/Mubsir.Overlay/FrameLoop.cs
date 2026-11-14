using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mubsir.Overlay;

/// <summary>How often an animated part needs to be drawn.</summary>
internal enum FrameNeed
{
    None,

    /// <summary>Ten times a second is enough (a sleeping bird's breath, the notch's resting light).</summary>
    Slow,

    /// <summary>Twenty times a second is enough (a resting bird's breath, a small fraction of a pixel per frame).</summary>
    Medium,

    /// <summary>Every screen refresh.</summary>
    Fast,
}

internal interface IAnimated
{
    FrameNeed Tick(double now, double dt);
}

/// <summary>
/// Drives the animations only as much as they need: every screen refresh while something really moves,
/// twenty or ten times a second for slow things, and not at all when everything rests, so an idle overlay
/// costs next to nothing on a 4 GB PC.
/// </summary>
internal sealed class FrameLoop
{
    private const double MaxStep = 0.05;
    private readonly List<IAnimated> _clients = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private static readonly TimeSpan SlowInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MediumInterval = TimeSpan.FromMilliseconds(50);
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = SlowInterval };
    private bool _hooked;
    private bool _paused;
    private long _frames;
    private double _last;
    private TimeSpan _lastRendering;

    public FrameLoop()
    {
        _timer.Tick += (_, _) => Run();
    }

    public double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>How many frames have been drawn so far, for measuring the frame rate.</summary>
    public long Frames => Interlocked.Read(ref _frames);

    /// <summary>While paused (the windows are hidden) nothing is drawn, whatever asks for it.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value)
            {
                Stop();
            }
            else
            {
                Wake();
            }
        }
    }

    public void Add(IAnimated client) => _clients.Add(client);

    /// <summary>Call whenever something starts moving.</summary>
    public void Wake()
    {
        if (_hooked || _paused)
        {
            return;
        }

        _timer.Stop();
        _last = Now;
        _hooked = true;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        _timer.Stop();
        Unhook();
    }

    private void Unhook()
    {
        if (_hooked)
        {
            _hooked = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void RunOnTimer(TimeSpan interval)
    {
        Unhook();
        if (_paused)
        {
            return;
        }

        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // WPF can raise Rendering more than once per frame; the rendering time tells them apart.
        if (e is RenderingEventArgs args)
        {
            if (args.RenderingTime == _lastRendering)
            {
                return;
            }

            _lastRendering = args.RenderingTime;
        }

        Run();
    }

    private void Run()
    {
        Interlocked.Increment(ref _frames);
        double now = Now;
        double dt = Math.Min(MaxStep, now - _last);
        _last = now;
        FrameNeed need = FrameNeed.None;
        foreach (IAnimated client in _clients)
        {
            FrameNeed n = client.Tick(now, dt);
            if (n > need)
            {
                need = n;
            }
        }

        switch (need)
        {
            case FrameNeed.Fast:
                Wake();
                break;
            case FrameNeed.Medium:
                RunOnTimer(MediumInterval);
                break;
            case FrameNeed.Slow:
                RunOnTimer(SlowInterval);
                break;
            default:
                Stop();
                break;
        }
    }
}
