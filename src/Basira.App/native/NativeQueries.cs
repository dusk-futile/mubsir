using System.Runtime.InteropServices;

namespace Basira.App.Native;

/// <summary>Small, checked wrappers over read-only Win32 queries.</summary>
internal static unsafe class NativeQueries
{
    /// <summary>
    /// Whether the process runs elevated. A process Basira cannot open or query counts as elevated:
    /// a non-elevated Basira cannot control it anyway.
    /// </summary>
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

    /// <summary>The foreground window and its process id (0 when there is none).</summary>
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

    /// <summary>
    /// True when the input desktop is the user's normal desktop ("Default"). False on the lock screen, the UAC
    /// secure desktop, or when the input desktop cannot be opened (which happens for those secure desktops).
    /// </summary>
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

    /// <summary>Total physical memory in MB, or null.</summary>
    public static long? TotalMemoryMb()
    {
        var status = new MEMORYSTATUSEX { DwLength = (uint)sizeof(MEMORYSTATUSEX) };
        return Kernel32.GlobalMemoryStatusEx(ref status) ? (long)(status.UllTotalPhys / (1024 * 1024)) : null;
    }

    /// <summary>Battery state: null when it cannot be read; HasBattery false on desktops.</summary>
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

    /// <summary>The keyboard layout's name for a key ("Space", "Right Ctrl"), or null.</summary>
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
