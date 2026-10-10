using System.Globalization;

namespace Basira.Core.Input;

/// <summary>
/// One physical or injected key transition as seen by the low-level keyboard hook.
/// <paramref name="TimeMs"/> is informational (traces, key explorer); the state machine times
/// everything with its injected clock.
/// </summary>
public readonly record struct KeyEvent(int VirtualKey, int ScanCode, bool Extended, bool IsDown, bool IsInjected, long TimeMs)
{
    public KeyIdentity Identity => new(VirtualKey, ScanCode, Extended);

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{(IsDown ? "down" : "up")} sc={ScanCode:X2}{(Extended ? "+" : "")} vk={VirtualKey:X2} t={TimeMs}{(IsInjected ? " injected" : "")}");
}

/// <summary>
/// A key as Basira stores it in settings (e.g. the talk key chosen at setup by pressing it).
/// Matching uses the physical scan code and extended flag when known, the virtual key otherwise.
/// </summary>
public readonly record struct KeyIdentity(int VirtualKey, int ScanCode, bool Extended)
{
    public static KeyIdentity RightCtrl { get; } = new(VirtualKeys.RCONTROL, ScanCodes.Ctrl, true);

    public static KeyIdentity LeftCtrl { get; } = new(VirtualKeys.LCONTROL, ScanCodes.Ctrl, false);

    /// <summary>The Application (context menu) key.</summary>
    public static KeyIdentity Apps { get; } = new(VirtualKeys.APPS, ScanCodes.Apps, true);

    public static KeyIdentity ScrollLock { get; } = new(VirtualKeys.SCROLL, ScanCodes.ScrollLock, false);

    public bool IsCtrl => VirtualKeys.IsCtrl(VirtualKey);

    public bool IsShift => VirtualKeys.IsShift(VirtualKey);

    public bool IsAlt => VirtualKeys.IsAlt(VirtualKey);

    public bool IsWin => VirtualKeys.IsWin(VirtualKey);

    /// <summary>Ctrl, Shift, Alt or Windows key.</summary>
    public bool IsModifier => VirtualKeys.IsModifier(VirtualKey);

    /// <summary>True when no key is configured (all fields zero).</summary>
    public bool IsEmpty => VirtualKey == 0 && ScanCode == 0;

    public static KeyIdentity Of(in KeyEvent e) => new(e.VirtualKey, e.ScanCode, e.Extended);

    /// <summary>Whether <paramref name="e"/> is this key: same physical position, or same virtual key when a scan code is missing.</summary>
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
