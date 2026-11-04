using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>The tokens, the rig and Hudhud's part images, all embedded in this assembly.</summary>
internal sealed class OverlayAssets
{
    private OverlayAssets(MotionTokens tokens, HudhudRig rig, IReadOnlyDictionary<string, BitmapSource> images)
    {
        Tokens = tokens;
        Rig = rig;
        Images = images;
        Palette = new Palette(tokens);
    }

    public MotionTokens Tokens { get; }

    public HudhudRig Rig { get; }

    public IReadOnlyDictionary<string, BitmapSource> Images { get; }

    public Palette Palette { get; }

    public static OverlayAssets Load()
    {
        MotionTokens tokens = MotionTokens.Parse(ReadText("Mubsir.Overlay.motion-tokens.json"));
        HudhudRig rig = HudhudRig.Parse(ReadText("Mubsir.Overlay.Hudhud.rig.json"));
        var images = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        foreach (string image in rig.Images())
        {
            images[image] = ReadPng("Mubsir.Overlay.Hudhud." + image + ".png");
        }

        return new OverlayAssets(tokens, rig, images);
    }

    private static Stream Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name) ?? throw new FileNotFoundException("missing resource " + name);

    private static string ReadText(string name)
    {
        using Stream stream = Open(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static BitmapSource ReadPng(string name)
    {
        using Stream stream = Open(name);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }
}

/// <summary>Colours from the tokens, as frozen brushes (safe and cheap to share).</summary>
internal sealed class Palette
{
    public Palette(MotionTokens tokens)
    {
        Island = Parse(tokens, "island");
        Amber = Parse(tokens, "amber");
        Cream = Parse(tokens, "cream");
        Crest = Parse(tokens, "crest");
        Ok = Parse(tokens, "ok");
        Lilac = Parse(tokens, "lilac");
        Teal = Parse(tokens, "teal");
        IslandBrush = Solid(Island);
        AmberBrush = Solid(Amber);
        CreamBrush = Solid(Cream);
        CreamDim = Solid(Cream, 0.62);
        CreamFaint = Solid(Cream, 0.38);
        CrestBrush = Solid(Crest);
        OkBrush = Solid(Ok);
    }

    public Color Island { get; }

    public Color Amber { get; }

    public Color Cream { get; }

    public Color Crest { get; }

    public Color Ok { get; }

    public Color Lilac { get; }

    public Color Teal { get; }

    public Brush IslandBrush { get; }

    public Brush AmberBrush { get; }

    public Brush CreamBrush { get; }

    public Brush CreamDim { get; }

    public Brush CreamFaint { get; }

    public Brush CrestBrush { get; }

    public Brush OkBrush { get; }

    public static SolidColorBrush Solid(Color color, double opacity = 1)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), color.R, color.G, color.B);

    private static Color Parse(MotionTokens tokens, string name) =>
        (Color)ColorConverter.ConvertFromString(tokens.Colors[name]);
}

/// <summary>Small helpers for building the notch's content in code.</summary>
internal static class Ui
{
    // Segoe UI Variable on Windows 11, Segoe UI on Windows 10; both cover Arabic.
    private static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Tahoma");
    private static readonly Dictionary<Color, ImageSource> Marks = [];

    public static TextBlock Text(string text, double size, Brush brush, FontWeight? weight = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontFamily = Font,
            FontSize = size,
            Foreground = brush,
            FontWeight = weight ?? FontWeights.Normal,
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextOptions.SetTextRenderingMode(block, TextRenderingMode.Grayscale);
        return block;
    }

    public static TextBlock Label(string text, Brush brush) => Text(text.ToUpperInvariant(), 11.5, brush, FontWeights.SemiBold);

    /// <summary>The app icon's eye. Drawn once as vectors and shared, so it stays sharp at any scale.</summary>
    public static Image Mark(Color color, double width, double height)
    {
        if (!Marks.TryGetValue(color, out ImageSource? source))
        {
            source = BuildMark(color);
            Marks[color] = source;
        }

        return new Image { Source = source, Width = width, Height = height, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
    }

    private static ImageSource BuildMark(Color color)
    {
        Brush fill = Palette.Solid(color);
        var ring = new Pen(fill, 1.9);
        ring.Freeze();
        var group = new DrawingGroup();
        // A transparent box fixes the drawing's bounds to the icon's view box (4 12 72 58).
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(4, 12, 72, 58))));
        group.Children.Add(new GeometryDrawing(fill, null, Geometry.Parse("M18.7 37.9 C19.2 30 29.5 17.2 44 16.6 C57.5 16.4 66.5 26 71.9 33.2 C64.5 30.6 55.5 23.1 44 21.6 C31.5 21.6 22.6 29.9 18.7 37.9 Z")));
        group.Children.Add(new GeometryDrawing(fill, null, Geometry.Parse("M71.9 33.3 C68 35.2 65 36.9 61.5 38.7 C58.5 40.4 55.5 42.1 52.5 43.2 C48.5 44.8 45.5 45.7 42 45.7 C37.5 45.7 33.6 43.6 30 41.7 C25.6 39.3 21.2 37.6 18.3 37.6 C14.8 40 10.4 43.4 8.4 47.4 C7 49.9 6.5 52 6.5 54 C6.5 56.5 6.6 57.8 7 59.2 C7.8 63.6 10.4 66.6 13.9 66.6 C17.5 66.6 19.9 64.2 19.9 61.4 C19.9 60 19.4 58.9 18.6 58.3 C18 59.9 16.5 60.7 14.9 60.6 C13 60.5 10.9 59.3 10.8 57.3 C10.7 55.4 10.5 53.6 10.7 52 C11.1 49.4 13.4 47.4 16.8 46.2 C18.4 45.6 19.8 44.7 21.2 44.7 C24.2 44.7 27.6 46.8 31 48.4 C34.5 50 37.5 50.5 41 50.5 C45.5 50.5 49.5 50 53 48.4 C56.2 46.8 59.2 44.7 61.6 42.7 C65.2 39.8 68.6 36.8 71.9 33.3 Z")));
        group.Children.Add(new GeometryDrawing(fill, null, new EllipseGeometry(new Point(44, 34), 3.4, 3.4)));
        group.Children.Add(new GeometryDrawing(null, ring, new EllipseGeometry(new Point(44, 34), 6.3, 6.3)));
        group.Children.Add(new GeometryDrawing(null, ring, new EllipseGeometry(new Point(44, 34), 10.05, 10.05)));
        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}

/// <summary>How often an animated part needs to be drawn.</summary>
internal enum FrameNeed
{
    None,

    /// <summary>A slow timer is enough (a sleeping bird's breath, the notch's resting light).</summary>
    Slow,

    /// <summary>Every screen refresh.</summary>
    Fast,
}

internal interface IAnimated
{
    FrameNeed Tick(double now, double dt);
}

/// <summary>
/// Drives the animations only as much as they need: every screen refresh while something moves, ten
/// times a second for slow things, and not at all when everything rests, so an idle overlay costs
/// next to nothing on a 4 GB PC.
/// </summary>
internal sealed class FrameLoop
{
    private const double MaxStep = 0.05;
    private readonly List<IAnimated> _clients = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _slow = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _hooked;
    private double _last;
    private TimeSpan _lastRendering;

    public FrameLoop()
    {
        _slow.Tick += (_, _) => Run();
    }

    public double Now => _clock.Elapsed.TotalSeconds;

    public void Add(IAnimated client) => _clients.Add(client);

    /// <summary>Call whenever something starts moving.</summary>
    public void Wake()
    {
        if (_hooked)
        {
            return;
        }

        _slow.Stop();
        _last = Now;
        _hooked = true;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        _slow.Stop();
        if (_hooked)
        {
            _hooked = false;
            CompositionTarget.Rendering -= OnRendering;
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
                _slow.Stop();
                Wake();
                break;
            case FrameNeed.Slow:
                Stop();
                _slow.Start();
                break;
            default:
                Stop();
                break;
        }
    }
}

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

    private void Check()
    {
        if (PresentationSource.FromVisual(_window) is null || !Win32.TryGetCursor(out Point screen))
        {
            return;
        }

        Point p = _window.PointFromScreen(screen);
        Rect rect = _interactive();
        bool over = rect.Contains(p);
        Rect nearby = rect;
        nearby.Inflate(NearDistance, NearDistance);
        TimeSpan interval = nearby.Contains(p) ? Near : Far;
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

/// <summary>Window styles that keep the overlay out of the user's way.</summary>
internal static class Win32
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;
    private const int VkLeftButton = 0x01;

    /// <summary>Never takes keyboard focus and never shows in Alt+Tab.</summary>
    public static void MakeOverlay(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, (nint)(style | WsExNoActivate | WsExToolWindow));
    }

    public static void SetClickThrough(Window window, bool clickThrough)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        long next = clickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        if (next != style)
        {
            SetWindowLongPtr(hwnd, GwlExStyle, (nint)next);
        }
    }

    public static bool IsLeftButtonDown() => (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;

    public static bool TryGetCursor(out Point screen)
    {
        bool ok = GetCursorPos(out CursorPoint p);
        screen = new Point(p.X, p.Y);
        return ok;
    }

    /// <summary>The settings every overlay window shares.</summary>
    public static void Configure(Window window)
    {
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = true;
        window.Background = Brushes.Transparent;
        window.Topmost = true;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.ResizeMode = ResizeMode.NoResize;
        window.Focusable = false;
        window.SourceInitialized += (_, _) => MakeOverlay(window);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
