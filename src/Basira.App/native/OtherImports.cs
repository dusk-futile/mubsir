using System.Runtime.InteropServices;

namespace Basira.App.Native;

/// <summary>kernel32.dll imports.</summary>
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

/// <summary>advapi32.dll imports.</summary>
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

/// <summary>shell32.dll imports.</summary>
internal static partial class Shell32
{
    [LibraryImport("shell32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);
}

/// <summary>wtsapi32.dll imports.</summary>
internal static partial class WtsApi32
{
    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSRegisterSessionNotification(nint hWnd, uint dwFlags);

    [LibraryImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WTSUnRegisterSessionNotification(nint hWnd);
}

/// <summary>winmm.dll imports: earcon playback and microphone capture.</summary>
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
