using System.Runtime.InteropServices;
using Mubsir.App.Actions.Interop;
using Mubsir.App.Screen.Interop;

namespace Mubsir.App.Actions;

internal sealed record WindowEntry(nint Hwnd, string Title, string? ProcessName, string? AppName);

internal static unsafe class WindowSwitcher
{
    public const int MaxWindows = 40;

    public static List<WindowEntry> List(Func<int, string?, string?>? friendlyName = null)
    {
        var handles = new List<nint>();
        var gc = GCHandle.Alloc(handles);
        try
        {
            ActionsNative.EnumWindows(&Collect, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        var result = new List<WindowEntry>();
        foreach (var hwnd in handles)
        {
            if (result.Count >= MaxWindows)
            {
                break;
            }

            if (!IsAltTabWindow(hwnd))
            {
                continue;
            }

            var title = ScreenNative.WindowText(hwnd);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            ScreenNative.GetWindowThreadProcessId(hwnd, out uint pid);
            string? process = null;
            try
            {
                var path = ScreenNative.ProcessImagePath(pid);
                process = path is null ? null : Path.GetFileName(path);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
            }

            result.Add(new WindowEntry(hwnd, title, process, friendlyName?.Invoke((int)pid, process)));
        }

        return result;
    }

    public static bool BringToFront(nint hwnd)
    {
        if (hwnd == 0 || !ScreenNative.IsWindow(hwnd))
        {
            return false;
        }

        if (ScreenNative.IsIconic(hwnd))
        {
            ActionsNative.ShowWindow(hwnd, ActionsNative.SwRestore);
        }

        nint fg = ScreenNative.GetForegroundWindow();
        uint fgThread = fg == 0 ? 0 : ScreenNative.GetWindowThreadProcessId(fg, out _);
        uint self = ActionsNative.GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != self && ActionsNative.AttachThreadInput(self, fgThread, true);
        try
        {
            ActionsNative.BringWindowToTop(hwnd);
            ActionsNative.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                ActionsNative.AttachThreadInput(self, fgThread, false);
            }
        }

        if (WaitForeground(hwnd, 300))
        {
            return true;
        }

        ActionsNative.SwitchToThisWindow(hwnd, true);
        return WaitForeground(hwnd, 500);
    }

    public static bool WaitForeground(nint hwnd, int ms)
    {
        long until = Environment.TickCount64 + ms;
        do
        {
            if (ScreenNative.GetForegroundWindow() == hwnd)
            {
                return true;
            }

            Thread.Sleep(50);
        }
        while (Environment.TickCount64 < until);
        return ScreenNative.GetForegroundWindow() == hwnd;
    }

    private static bool IsAltTabWindow(nint hwnd)
    {
        if (!ScreenNative.IsWindowVisible(hwnd) || ScreenNative.IsCloaked(hwnd))
        {
            return false;
        }

        long ex = ActionsNative.GetWindowLongPtr(hwnd, ActionsNative.GwlExStyle);
        if ((ex & ActionsNative.WsExAppWindow) != 0)
        {
            return true;
        }

        if ((ex & ActionsNative.WsExToolWindow) != 0 || (ex & ActionsNative.WsExNoActivate) != 0)
        {
            return false;
        }

        return ActionsNative.GetWindow(hwnd, ActionsNative.GwOwner) == 0;
    }

    [UnmanagedCallersOnly]
    private static int Collect(nint hwnd, nint lParam)
    {
        try
        {
            if (GCHandle.FromIntPtr(lParam).Target is List<nint> list && list.Count < 1000)
            {
                list.Add(hwnd);
            }
        }
        catch (Exception)
        {
        }

        return 1;
    }
}
