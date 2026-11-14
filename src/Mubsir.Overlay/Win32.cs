using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Mubsir.Overlay;

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

    /// <summary>A full-screen app has the screen: a game, a movie, a slideshow.</summary>
    public static bool FullScreenAppRunning()
    {
        const int Busy = 2;
        const int RunningFullScreen = 3;
        const int PresentationMode = 4;
        return SHQueryUserNotificationState(out int state) == 0 && state is Busy or RunningFullScreen or PresentationMode;
    }

    /// <summary>True if the left button is down now or was pressed since the last call, so a quick
    /// click between two frames still counts.</summary>
    public static bool LeftButtonPressed() => GetAsyncKeyState(VkLeftButton) != 0;

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

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
