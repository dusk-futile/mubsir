using System.Runtime.InteropServices;

namespace Basira.App.Screen.Interop;

/// <summary>Win32 calls used to identify and capture the foreground window. Every caller checks the results.</summary>
internal static unsafe partial class ScreenNative
{
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const uint TokenQuery = 0x0008;
    public const int TokenElevationClass = 20;
    public const int ErrorAccessDenied = 5;

    public const uint DwmwaExtendedFrameBounds = 9;
    public const uint DwmwaCloaked = 14;

    public const uint Srccopy = 0x00CC0020;
    public const uint Captureblt = 0x40000000;
    public const uint DibRgbColors = 0;

    public const int SmXVirtualScreen = 76;
    public const int SmYVirtualScreen = 77;
    public const int SmCxVirtualScreen = 78;
    public const int SmCyVirtualScreen = 79;

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW", SetLastError = true)]
    public static partial int GetWindowTextLength(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true)]
    public static partial int GetWindowText(nint hwnd, char* buffer, int maxCount);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    public static partial int GetClassName(nint hwnd, char* buffer, int maxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsIconic(nint hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint process, uint flags, char* buffer, ref uint size);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint process, uint access, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint token, int infoClass, void* info, uint length, out uint returnLength);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmGetWindowAttribute(nint hwnd, uint attribute, void* value, uint size);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(nint hdc, BitmapInfoHeader* info, uint usage, out void* bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint obj);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint hdcDest, int x, int y, int width, int height, nint hdcSrc, int srcX, int srcY, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GdiFlush();

    /// <summary>Window title, or null. Never throws.</summary>
    public static string? WindowText(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        int len = GetWindowTextLength(hwnd);
        if (len <= 0)
        {
            return null;
        }

        len = Math.Min(len, 4096);
        var buffer = new char[len + 1];
        fixed (char* p = buffer)
        {
            int got = GetWindowText(hwnd, p, buffer.Length);
            return got > 0 ? new string(p, 0, got) : null;
        }
    }

    /// <summary>Win32 window class, or null.</summary>
    public static string? ClassName(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }

        char* buffer = stackalloc char[257];
        int got = GetClassName(hwnd, buffer, 257);
        return got > 0 ? new string(buffer, 0, got) : null;
    }

    /// <summary>Full image path of a process, or null (access denied, exited).</summary>
    public static string? ProcessImagePath(uint processId)
    {
        if (processId == 0)
        {
            return null;
        }

        nint process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            return QueryFullProcessImageName(process, 0, buffer, ref size) && size > 0 ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>
    /// True when the process runs elevated. A process whose token we may not open (another user, system, or an
    /// elevated process from our medium-integrity process) is treated as elevated: Basira cannot drive it.
    /// </summary>
    public static bool IsProcessElevated(uint processId)
    {
        if (processId == 0)
        {
            return false;
        }

        nint process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return Marshal.GetLastPInvokeError() == ErrorAccessDenied;
        }

        try
        {
            if (!OpenProcessToken(process, TokenQuery, out var token))
            {
                return Marshal.GetLastPInvokeError() == ErrorAccessDenied;
            }

            try
            {
                int elevated = 0;
                return GetTokenInformation(token, TokenElevationClass, &elevated, sizeof(int), out _) && elevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>The visible frame of a window in physical pixels (DWM bounds exclude the invisible resize border).</summary>
    public static bool TryGetFrameBounds(nint hwnd, out Rect rect)
    {
        rect = default;
        Rect r;
        if (DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, &r, (uint)sizeof(Rect)) == 0 && r.Width > 0 && r.Height > 0)
        {
            rect = r;
            return true;
        }

        if (GetWindowRect(hwnd, out r) && r.Width > 0 && r.Height > 0)
        {
            rect = r;
            return true;
        }

        return false;
    }

    /// <summary>True when DWM hides the window (e.g. a suspended UWP app or a window on another virtual desktop).</summary>
    public static bool IsCloaked(nint hwnd)
    {
        int cloaked = 0;
        return DwmGetWindowAttribute(hwnd, DwmwaCloaked, &cloaked, sizeof(int)) == 0 && cloaked != 0;
    }
}
