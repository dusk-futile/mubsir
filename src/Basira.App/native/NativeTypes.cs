using System.Runtime.InteropServices;

namespace Basira.App.Native;

// Win32 structures used by the host. Layouts follow the Windows SDK headers (x64).

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

/// <summary>WAVEFORMATEX is byte-packed in mmreg.h (18 bytes).</summary>
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
