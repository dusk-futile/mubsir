using System.Windows;
using System.Windows.Threading;

namespace Mubsir.Overlay;

/// <summary>
/// Keeps a window click-through until the cursor is really on its interactive part. Soft shadows and
/// glows have visible pixels, and Windows would otherwise swallow clicks on them, so clicks meant for
/// the app underneath always get there. The cursor is checked often only while it is close by.
/// </summary>
internal sealed class ClickThrough
{
    private static readonly TimeSpan Near = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan Far = TimeSpan.FromMilliseconds(120);
    private const double NearDistance = 80;
    private readonly Window _window;
    private readonly Func<Rect> _interactive;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input) { Interval = Far };
    private bool _over;

    public ClickThrough(Window window, Func<Rect> interactive)
    {
        _window = window;
        _interactive = interactive;
        _timer.Tick += (_, _) => Check();
        window.SourceInitialized += (_, _) =>
        {
            Win32.SetClickThrough(window, true);
            _timer.Start();
        };
        window.Closed += (_, _) => _timer.Stop();
    }

    /// <summary>The cursor came onto or left the interactive part.</summary>
    public event Action<bool>? HoverChanged;

    public bool IsOver => _over;

    /// <summary>Looks at the cursor right now instead of waiting for the next check.</summary>
    public bool CheckNow()
    {
        Check();
        return _over;
    }

    private void Check()
    {
        if (PresentationSource.FromVisual(_window) is null || !Win32.TryGetCursor(out Point screen))
        {
            return;
        }

        Point p = _window.PointFromScreen(screen);
        // Empty means nothing here is interactive right now, like a closed panel.
        Rect rect = _interactive();
        bool over = !rect.IsEmpty && rect.Contains(p);
        bool near = false;
        if (!rect.IsEmpty)
        {
            rect.Inflate(NearDistance, NearDistance);
            near = rect.Contains(p);
        }

        TimeSpan interval = near ? Near : Far;
        if (_timer.Interval != interval)
        {
            _timer.Interval = interval;
        }

        if (over != _over)
        {
            _over = over;
            Win32.SetClickThrough(_window, !over);
            HoverChanged?.Invoke(over);
        }
    }
}
