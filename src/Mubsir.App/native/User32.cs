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
