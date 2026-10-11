namespace Mubsir.App.Native;

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
