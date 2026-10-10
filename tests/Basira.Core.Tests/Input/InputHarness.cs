using Basira.Core.Common;
using Basira.Core.Input;

namespace Basira.Core.Tests.Input;

/// <summary>Physical keys used in tests (scan codes as on a US/Arabic PC keyboard).</summary>
internal static class K
{
    public static readonly KeyIdentity RightCtrl = KeyIdentity.RightCtrl;
    public static readonly KeyIdentity LeftCtrl = KeyIdentity.LeftCtrl;
    public static readonly KeyIdentity Apps = KeyIdentity.Apps;
    public static readonly KeyIdentity ScrollLock = KeyIdentity.ScrollLock;
    public static readonly KeyIdentity Space = new(VirtualKeys.SPACE, ScanCodes.Space, false);
    public static readonly KeyIdentity Enter = new(VirtualKeys.RETURN, ScanCodes.Enter, false);
    public static readonly KeyIdentity NumpadEnter = new(VirtualKeys.RETURN, ScanCodes.Enter, true);
    public static readonly KeyIdentity Esc = new(VirtualKeys.ESCAPE, ScanCodes.Escape, false);
    public static readonly KeyIdentity Period = new(VirtualKeys.OEM_PERIOD, ScanCodes.Period, false);
    public static readonly KeyIdentity F1 = new(VirtualKeys.F1, ScanCodes.F1, false);
    public static readonly KeyIdentity Left = new(VirtualKeys.LEFT, ScanCodes.Left, true);
    public static readonly KeyIdentity Right = new(VirtualKeys.RIGHT, ScanCodes.Right, true);
    public static readonly KeyIdentity Up = new(VirtualKeys.UP, ScanCodes.Up, true);
    public static readonly KeyIdentity Down = new(VirtualKeys.DOWN, ScanCodes.Down, true);
    public static readonly KeyIdentity NumpadLeft = new(VirtualKeys.NUMPAD4, ScanCodes.Left, false);
    public static readonly KeyIdentity A = new(0x41, 0x1E, false);
    public static readonly KeyIdentity C = new(0x43, 0x2E, false);
    public static readonly KeyIdentity Tab = new(VirtualKeys.TAB, 0x0F, false);
    public static readonly KeyIdentity Shift = new(VirtualKeys.LSHIFT, ScanCodes.LeftShift, false);
    public static readonly KeyIdentity Alt = new(VirtualKeys.LMENU, ScanCodes.Alt, false);
    public static readonly KeyIdentity Insert = new(VirtualKeys.INSERT, ScanCodes.Insert, true);
}

/// <summary>Drives an <see cref="InputStateMachine"/> with a manual clock.</summary>
internal sealed class InputHarness
{
    public InputHarness(InputSettings? settings = null)
    {
        Clock = new ManualClock(10_000);
        Machine = new InputStateMachine(settings ?? new InputSettings(), Clock);
    }

    public ManualClock Clock { get; }

    public InputStateMachine Machine { get; }

    public KeyDecision Down(KeyIdentity k, bool injected = false) =>
        Machine.OnKey(new KeyEvent(k.VirtualKey, k.ScanCode, k.Extended, IsDown: true, injected, Clock.NowMs));

    public KeyDecision Up(KeyIdentity k, bool injected = false) =>
        Machine.OnKey(new KeyEvent(k.VirtualKey, k.ScanCode, k.Extended, IsDown: false, injected, Clock.NowMs));

    /// <summary>Advance time in 50 ms ticks (like the app's timer) and collect what the ticks emitted.</summary>
    public List<InputAction> Wait(long ms)
    {
        var all = new List<InputAction>();
        while (ms > 0)
        {
            long step = Math.Min(50, ms);
            Clock.Advance(step);
            ms -= step;
            all.AddRange(Machine.OnTick());
        }

        return all;
    }

    /// <summary>Advance time without ticking (the hook's next event must detect what is due).</summary>
    public void Skip(long ms) => Clock.Advance(ms);

    /// <summary>Press and release with ticks in between; returns every action, down and up decisions.</summary>
    public (KeyDecision Down, List<InputAction> During, KeyDecision Up) Tap(KeyIdentity k, long holdMs = 60)
    {
        KeyDecision d = Down(k);
        List<InputAction> during = Wait(holdMs);
        KeyDecision u = Up(k);
        return (d, during, u);
    }

    /// <summary>Double-tap the key with default-friendly timing; returns the second release.</summary>
    public KeyDecision DoubleTap(KeyIdentity k)
    {
        Tap(k);
        Wait(100);
        return Tap(k).Up;
    }

    public void OpenLayer()
    {
        KeyDecision d = DoubleTap(K.RightCtrl);
        if (!d.Actions.Any(a => a is LayerOpened))
        {
            throw new InvalidOperationException("Layer did not open: " + d);
        }
    }
}

internal static class ActionAssert
{
    public static void Sequence(IReadOnlyList<InputAction> actual, params Type[] expected)
    {
        Assert.Equal(expected.Select(t => t.Name), actual.Select(a => a.GetType().Name));
    }

    public static void None(IReadOnlyList<InputAction> actual) => Assert.Empty(actual);

    public static void Has<T>(IReadOnlyList<InputAction> actual)
        where T : InputAction => Assert.Contains(actual, a => a is T);

    public static void Lacks<T>(IReadOnlyList<InputAction> actual)
        where T : InputAction => Assert.DoesNotContain(actual, a => a is T);
}
