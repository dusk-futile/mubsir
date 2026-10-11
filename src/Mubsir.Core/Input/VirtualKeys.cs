namespace Mubsir.Core.Input;

public static class VirtualKeys
{
    public const int BACK = 0x08;
    public const int TAB = 0x09;
    public const int RETURN = 0x0D;
    public const int SHIFT = 0x10;
    public const int CONTROL = 0x11;
    public const int MENU = 0x12;
    public const int PAUSE = 0x13;
    public const int CAPITAL = 0x14;
    public const int ESCAPE = 0x1B;
    public const int SPACE = 0x20;
    public const int PRIOR = 0x21;
    public const int NEXT = 0x22;
    public const int END = 0x23;
    public const int HOME = 0x24;
    public const int LEFT = 0x25;
    public const int UP = 0x26;
    public const int RIGHT = 0x27;
    public const int DOWN = 0x28;
    public const int INSERT = 0x2D;
    public const int DELETE = 0x2E;
    public const int LWIN = 0x5B;
    public const int RWIN = 0x5C;
    public const int APPS = 0x5D;
    public const int NUMPAD2 = 0x62;
    public const int NUMPAD4 = 0x64;
    public const int NUMPAD6 = 0x66;
    public const int NUMPAD8 = 0x68;
    public const int F1 = 0x70;
    public const int NUMLOCK = 0x90;
    public const int SCROLL = 0x91;
    public const int LSHIFT = 0xA0;
    public const int RSHIFT = 0xA1;
    public const int LCONTROL = 0xA2;
    public const int RCONTROL = 0xA3;
    public const int LMENU = 0xA4;
    public const int RMENU = 0xA5;
    public const int OEM_PERIOD = 0xBE;

    public static bool IsCtrl(int vk) => vk is CONTROL or LCONTROL or RCONTROL;

    public static bool IsShift(int vk) => vk is SHIFT or LSHIFT or RSHIFT;

    public static bool IsAlt(int vk) => vk is MENU or LMENU or RMENU;

    public static bool IsWin(int vk) => vk is LWIN or RWIN;

    public static bool IsModifier(int vk) => IsCtrl(vk) || IsShift(vk) || IsAlt(vk) || IsWin(vk);
}

public static class ScanCodes
{
    public const int Escape = 0x01;
    public const int Enter = 0x1C;
    public const int Ctrl = 0x1D;
    public const int LeftShift = 0x2A;
    public const int Period = 0x34;
    public const int RightShift = 0x36;
    public const int Alt = 0x38;
    public const int Space = 0x39;
    public const int CapsLock = 0x3A;
    public const int F1 = 0x3B;
    public const int ScrollLock = 0x46;
    public const int Up = 0x48;
    public const int Left = 0x4B;
    public const int Right = 0x4D;
    public const int Down = 0x50;
    public const int Insert = 0x52;
    public const int LeftWin = 0x5B;
    public const int Apps = 0x5D;
}
