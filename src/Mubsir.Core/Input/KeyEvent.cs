using System.Globalization;
using System.Text.Json.Serialization;

namespace Mubsir.Core.Input;

public readonly record struct KeyEvent(int VirtualKey, int ScanCode, bool Extended, bool IsDown, bool IsInjected, long TimeMs)
{
    public KeyIdentity Identity => new(VirtualKey, ScanCode, Extended);

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{(IsDown ? "down" : "up")} sc={ScanCode:X2}{(Extended ? "+" : "")} vk={VirtualKey:X2} t={TimeMs}{(IsInjected ? " injected" : "")}");
}

public readonly record struct KeyIdentity(int VirtualKey, int ScanCode, bool Extended)
{
    public static KeyIdentity RightCtrl { get; } = new(VirtualKeys.RCONTROL, ScanCodes.Ctrl, true);

    public static KeyIdentity LeftCtrl { get; } = new(VirtualKeys.LCONTROL, ScanCodes.Ctrl, false);

    public static KeyIdentity Apps { get; } = new(VirtualKeys.APPS, ScanCodes.Apps, true);

    public static KeyIdentity ScrollLock { get; } = new(VirtualKeys.SCROLL, ScanCodes.ScrollLock, false);

    [JsonIgnore]
    public bool IsCtrl => VirtualKeys.IsCtrl(VirtualKey);

    [JsonIgnore]
    public bool IsShift => VirtualKeys.IsShift(VirtualKey);

    [JsonIgnore]
    public bool IsAlt => VirtualKeys.IsAlt(VirtualKey);

    [JsonIgnore]
    public bool IsWin => VirtualKeys.IsWin(VirtualKey);

    [JsonIgnore]
    public bool IsModifier => VirtualKeys.IsModifier(VirtualKey);

    [JsonIgnore]
    public bool IsEmpty => VirtualKey == 0 && ScanCode == 0;

    public static KeyIdentity Of(in KeyEvent e) => new(e.VirtualKey, e.ScanCode, e.Extended);

    public bool Matches(in KeyEvent e)
    {
        if (ScanCode != 0 && e.ScanCode != 0)
        {
            return ScanCode == e.ScanCode && Extended == e.Extended;
        }

        return VirtualKey != 0 && VirtualKey == e.VirtualKey;
    }

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"sc={ScanCode:X2}{(Extended ? "+" : "")} vk={VirtualKey:X2}");
}
