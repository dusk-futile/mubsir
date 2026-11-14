using System.Runtime.InteropServices;

namespace Mubsir.App.Native;

internal static unsafe partial class User32
{
    private const string Dll = "user32.dll";

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint SetWindowsHookExW(int idHook, delegate* unmanaged[Stdcall]<int, nint, nint, nint> lpfn, nint hmod, uint dwThreadId);

    [LibraryImport(Dll)]
    public static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport(Dll)]
    public static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(Dll)]
    public static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(Dll)]
    public static partial void PostQuitMessage(int nExitCode);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial ushort RegisterClassExW(in WNDCLASSEXW lpwcx);

    [LibraryImport(Dll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport(Dll)]
    public static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        delegate* unmanaged[Stdcall]<nint, uint, nint, int, int, uint, uint, void> pfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWinEvent(nint hWinEventHook);

    [LibraryImport(Dll)]
    public static partial nint GetForegroundWindow();

    [LibraryImport(Dll, SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetCursorPos(int x, int y);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllowSetForegroundWindow(uint dwProcessId);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BringWindowToTop(nint hWnd);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport(Dll)]
    public static partial nint SetFocus(nint hWnd);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint LoadIconW(nint hInstance, nint lpIconName);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint LoadImageW(nint hInst, nint name, uint type, int cx, int cy, uint fuLoad);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    [LibraryImport(Dll)]
    public static partial short GetAsyncKeyState(int vKey);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool KillTimer(nint hWnd, nuint uIDEvent);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial int GetWindowTextLengthW(nint hWnd);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial int GetWindowTextW(nint hWnd, char* lpString, int nMaxCount);

    [LibraryImport(Dll)]
    public static partial int GetKeyNameTextW(int lParam, char* lpString, int cchSize);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint CreatePopupMenu();

    [LibraryImport(Dll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint hMenu);

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport(Dll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessageW(string lpString);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint OpenInputDesktop(uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetUserObjectInformationW(nint hObj, int nIndex, void* pvInfo, uint nLength, out uint lpnLengthNeeded);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseDesktop(nint hDesktop);

    [LibraryImport(Dll)]
    public static partial int GetSystemMetrics(int nIndex);
}

internal static partial class Kernel32
{
    private const string Dll = "kernel32.dll";

    [LibraryImport(Dll, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport(Dll, SetLastError = true)]
    public static partial nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [LibraryImport(Dll)]
    public static partial uint GetCurrentThreadId();

    public const int ThreadModeBackgroundBegin = 0x00010000;

    [LibraryImport(Dll)]
    public static partial nint GetCurrentThread();

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetThreadPriority(nint hThread, int nPriority);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AttachConsole(int dwProcessId);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AllocConsole();

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FreeConsole();
}

internal static unsafe partial class Advapi32
{
    private const string Dll = "advapi32.dll";

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [LibraryImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, void* tokenInformation, uint tokenInformationLength, out uint returnLength);
}

internal static partial class Shell32
{
    [LibraryImport("shell32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);
}

internal static partial class WtsApi32
{
    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSRegisterSessionNotification(nint hWnd, uint dwFlags);

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSUnRegisterSessionNotification(nint hWnd);
}

internal static unsafe partial class WinMm
{
    private const string Dll = "winmm.dll";

    [LibraryImport(Dll)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PlaySoundW(nint pszSound, nint hmod, uint fdwSound);

    [LibraryImport(Dll)]
    public static partial uint waveInGetNumDevs();

    [LibraryImport(Dll)]
    public static partial uint waveInOpen(out nint phwi, uint uDeviceID, in WAVEFORMATEX pwfx, nint dwCallback, nint dwInstance, uint fdwOpen);

    [LibraryImport(Dll)]
    public static partial uint waveInPrepareHeader(nint hwi, WAVEHDR* pwh, uint cbwh);

    [LibraryImport(Dll)]
    public static partial uint waveInUnprepareHeader(nint hwi, WAVEHDR* pwh, uint cbwh);

    [LibraryImport(Dll)]
    public static partial uint waveInAddBuffer(nint hwi, WAVEHDR* pwh, uint cbwh);

    [LibraryImport(Dll)]
    public static partial uint waveInStart(nint hwi);

    [LibraryImport(Dll)]
    public static partial uint waveInStop(nint hwi);

    [LibraryImport(Dll)]
    public static partial uint waveInReset(nint hwi);

    [LibraryImport(Dll)]
    public static partial uint waveInClose(nint hwi);
}

internal static unsafe class NativeQueries
{
    public static bool IsProcessElevated(uint processId)
    {
        if (processId == 0)
        {
            return true;
        }

        nint process = Kernel32.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return true;
        }

        try
        {
            if (!Advapi32.OpenProcessToken(process, Win32.TOKEN_QUERY, out nint token) || token == 0)
            {
                return true;
            }

            try
            {
                TOKEN_ELEVATION elevation;
                if (!Advapi32.GetTokenInformation(token, Win32.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), out _))
                {
                    return true;
                }

                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                Kernel32.CloseHandle(token);
            }
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    public static (nint Hwnd, uint ProcessId, uint ThreadId) Foreground()
    {
        nint hwnd = User32.GetForegroundWindow();
        if (hwnd == 0)
        {
            return (0, 0, 0);
        }

        uint tid = User32.GetWindowThreadProcessId(hwnd, out uint pid);
        return (hwnd, pid, tid);
    }

    public static bool IsInputDesktopDefault()
    {
        nint desk = User32.OpenInputDesktop(0, false, Win32.DESKTOP_READOBJECTS);
        if (desk == 0)
        {
            return false;
        }

        try
        {
            char* name = stackalloc char[64];
            if (!User32.GetUserObjectInformationW(desk, Win32.UOI_NAME, name, 64 * sizeof(char), out _))
            {
                return false;
            }

            return new string(name).Equals("Default", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            User32.CloseDesktop(desk);
        }
    }

    public static long? TotalMemoryMb()
    {
        var status = new MEMORYSTATUSEX { DwLength = (uint)sizeof(MEMORYSTATUSEX) };
        return Kernel32.GlobalMemoryStatusEx(ref status) ? (long)(status.UllTotalPhys / (1024 * 1024)) : null;
    }

    public static (bool HasBattery, int? Percent, bool Charging)? PowerStatus()
    {
        if (!Kernel32.GetSystemPowerStatus(out SYSTEM_POWER_STATUS s))
        {
            return null;
        }

        const byte NoSystemBattery = 128;
        const byte Unknown = 255;
        if ((s.BatteryFlag & NoSystemBattery) != 0 || s.BatteryFlag == Unknown)
        {
            return (false, null, s.ACLineStatus == 1);
        }

        int? percent = s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : null;
        const byte Charging = 8;
        return (true, percent, (s.BatteryFlag & Charging) != 0 || s.ACLineStatus == 1);
    }

    public static string? KeyName(int scanCode, bool extended)
    {
        if (scanCode <= 0)
        {
            return null;
        }

        int lParam = (scanCode & 0xFF) << 16 | (extended ? 1 << 24 : 0);
        char* buffer = stackalloc char[64];
        int n = User32.GetKeyNameTextW(lParam, buffer, 64);
        return n > 0 ? new string(buffer, 0, n) : null;
    }

    public static int LastError() => Marshal.GetLastPInvokeError();
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint Hwnd;
    public uint Message;
    public nint WParam;
    public nint LParam;
    public uint Time;
    public POINT Pt;
    public uint LPrivate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WNDCLASSEXW
{
    public uint CbSize;
    public uint Style;
    public nint LpfnWndProc;
    public int CbClsExtra;
    public int CbWndExtra;
    public nint HInstance;
    public nint HIcon;
    public nint HCursor;
    public nint HbrBackground;
    public nint LpszMenuName;
    public nint LpszClassName;
    public nint HIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint VkCode;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public nuint DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct NOTIFYICONDATAW
{
    public uint CbSize;
    public nint HWnd;
    public uint UID;
    public uint UFlags;
    public uint UCallbackMessage;
    public nint HIcon;
    public fixed char SzTip[128];
    public uint DwState;
    public uint DwStateMask;
    public fixed char SzInfo[256];
    public uint UTimeoutOrVersion;
    public fixed char SzInfoTitle[64];
    public uint DwInfoFlags;
    public Guid GuidItem;
    public nint HBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MEMORYSTATUSEX
{
    public uint DwLength;
    public uint DwMemoryLoad;
    public ulong UllTotalPhys;
    public ulong UllAvailPhys;
    public ulong UllTotalPageFile;
    public ulong UllAvailPageFile;
    public ulong UllTotalVirtual;
    public ulong UllAvailVirtual;
    public ulong UllAvailExtendedVirtual;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_ELEVATION
{
    public uint TokenIsElevated;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_POWER_STATUS
{
    public byte ACLineStatus;
    public byte BatteryFlag;
    public byte BatteryLifePercent;
    public byte SystemStatusFlag;
    public uint BatteryLifeTime;
    public uint BatteryFullLifeTime;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct WAVEFORMATEX
{
    public ushort WFormatTag;
    public ushort NChannels;
    public uint NSamplesPerSec;
    public uint NAvgBytesPerSec;
    public ushort NBlockAlign;
    public ushort WBitsPerSample;
    public ushort CbSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WAVEHDR
{
    public nint LpData;
    public uint DwBufferLength;
    public uint DwBytesRecorded;
    public nuint DwUser;
    public uint DwFlags;
    public uint DwLoops;
    public nint LpNext;
    public nuint Reserved;
}

internal static class Win32
{
    public const int WH_KEYBOARD_LL = 13;
    public const int HC_ACTION = 0;
    public const uint LLKHF_EXTENDED = 0x01;
    public const uint LLKHF_LOWER_IL_INJECTED = 0x02;
    public const uint LLKHF_INJECTED = 0x10;

    public const uint WM_NULL = 0x0000;
    public const uint WM_CREATE = 0x0001;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_QUERYENDSESSION = 0x0011;
    public const uint WM_ENDSESSION = 0x0016;
    public const uint WM_SETFONT = 0x0030;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_WTSSESSION_CHANGE = 0x02B1;
    public const uint WM_APP = 0x8000;

    public const int WA_INACTIVE = 0;

    public const uint WS_OVERLAPPED = 0x00000000;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CAPTION = 0x00C00000;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_BORDER = 0x00800000;
    public const uint WS_TABSTOP = 0x00010000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    public const uint ES_AUTOHSCROLL = 0x0080;
    public const uint SS_LEFT = 0x0000;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const int SW_SHOW = 5;
    public const int SW_SHOWNORMAL = 1;
    public const uint EM_LIMITTEXT = 0x00C5;
    public static readonly nint HWND_MESSAGE = -3;

    public const int VK_RETURN = 0x0D;
    public const int VK_ESCAPE = 0x1B;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_DESKTOPSWITCH = 0x0020;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const int OBJID_WINDOW = 0;

    public const uint NOTIFY_FOR_THIS_SESSION = 0;
    public const int WTS_CONSOLE_CONNECT = 0x1;
    public const int WTS_CONSOLE_DISCONNECT = 0x2;
    public const int WTS_REMOTE_CONNECT = 0x3;
    public const int WTS_REMOTE_DISCONNECT = 0x4;
    public const int WTS_SESSION_LOCK = 0x7;
    public const int WTS_SESSION_UNLOCK = 0x8;

    public const uint DESKTOP_READOBJECTS = 0x0001;
    public const int UOI_NAME = 2;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint TOKEN_QUERY = 0x0008;
    public const int TokenElevation = 20;
    public const uint ASFW_ANY = unchecked((uint)-1);
    public const int ATTACH_PARENT_PROCESS = -1;

    public const uint NIM_ADD = 0x0;
    public const uint NIM_MODIFY = 0x1;
    public const uint NIM_DELETE = 0x2;
    public const uint NIM_SETVERSION = 0x4;
    public const uint NIF_MESSAGE = 0x1;
    public const uint NIF_ICON = 0x2;
    public const uint NIF_TIP = 0x4;
    public const uint NIF_SHOWTIP = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;
    public static readonly nint IDI_APPLICATION = 32512;
    public const uint IMAGE_ICON = 1;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;

    public const uint MF_STRING = 0x0000;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_NONOTIFY = 0x0080;

    public const uint SND_ASYNC = 0x0001;
    public const uint SND_NODEFAULT = 0x0002;
    public const uint SND_MEMORY = 0x0004;

    public const uint WAVE_MAPPER = unchecked((uint)-1);
    public const ushort WAVE_FORMAT_PCM = 1;
    public const uint CALLBACK_EVENT = 0x00050000;
    public const uint WHDR_DONE = 0x00000001;
    public const uint MMSYSERR_NOERROR = 0;

    public const int ERROR_ALREADY_EXISTS = 183;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;
}
