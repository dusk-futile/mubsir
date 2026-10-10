using Basira.Core.Input;

namespace Basira.Core.Tests.Input;

public class KeyIdentityTests
{
    [Fact]
    public void WellKnownKeys_HaveTheExpectedCodes()
    {
        Assert.Equal(new KeyIdentity(0xA3, 0x1D, true), KeyIdentity.RightCtrl);
        Assert.Equal(new KeyIdentity(0xA2, 0x1D, false), KeyIdentity.LeftCtrl);
        Assert.Equal(new KeyIdentity(0x5D, 0x5D, true), KeyIdentity.Apps);
        Assert.Equal(new KeyIdentity(0x91, 0x46, false), KeyIdentity.ScrollLock);
    }

    [Fact]
    public void ModifierHelpers()
    {
        Assert.True(KeyIdentity.RightCtrl.IsCtrl);
        Assert.True(KeyIdentity.RightCtrl.IsModifier);
        Assert.True(new KeyIdentity(VirtualKeys.CONTROL, 0, false).IsCtrl);
        Assert.True(new KeyIdentity(VirtualKeys.RSHIFT, 0x36, false).IsShift);
        Assert.True(new KeyIdentity(VirtualKeys.LMENU, 0x38, false).IsAlt);
        Assert.True(new KeyIdentity(VirtualKeys.RWIN, 0x5C, true).IsWin);
        Assert.False(KeyIdentity.Apps.IsModifier);
        Assert.False(KeyIdentity.ScrollLock.IsModifier);
        Assert.False(new KeyIdentity(VirtualKeys.CAPITAL, ScanCodes.CapsLock, false).IsModifier);
        Assert.False(new KeyIdentity(VirtualKeys.INSERT, ScanCodes.Insert, true).IsModifier);
    }

    [Fact]
    public void Matches_UsesPhysicalPosition()
    {
        var rightCtrl = new KeyEvent(VirtualKeys.RCONTROL, 0x1D, true, true, false, 0);
        var leftCtrl = new KeyEvent(VirtualKeys.LCONTROL, 0x1D, false, true, false, 0);
        var remapped = new KeyEvent(0xFF, 0x1D, true, true, false, 0);

        Assert.True(KeyIdentity.RightCtrl.Matches(rightCtrl));
        Assert.False(KeyIdentity.RightCtrl.Matches(leftCtrl));
        Assert.True(KeyIdentity.LeftCtrl.Matches(leftCtrl));
        Assert.True(KeyIdentity.RightCtrl.Matches(remapped));
    }

    [Fact]
    public void Matches_FallsBackToVirtualKeyWithoutScanCode()
    {
        var noScan = new KeyEvent(VirtualKeys.APPS, 0, false, true, false, 0);

        Assert.True(KeyIdentity.Apps.Matches(noScan));
        Assert.True(new KeyIdentity(VirtualKeys.APPS, 0, false).Matches(new KeyEvent(VirtualKeys.APPS, 0x5D, true, true, false, 0)));
        Assert.False(default(KeyIdentity).Matches(new KeyEvent(0, 0, false, true, false, 0)));
    }

    [Fact]
    public void IdentityRoundTrip_FromEvent()
    {
        var e = new KeyEvent(VirtualKeys.SCROLL, ScanCodes.ScrollLock, false, true, false, 5);

        Assert.Equal(KeyIdentity.ScrollLock, KeyIdentity.Of(e));
        Assert.Equal(KeyIdentity.ScrollLock, e.Identity);
        Assert.True(default(KeyIdentity).IsEmpty);
        Assert.False(KeyIdentity.Apps.IsEmpty);
    }

    [Fact]
    public void VirtualKeyConstants()
    {
        Assert.Equal(0x11, VirtualKeys.CONTROL);
        Assert.Equal(0xA2, VirtualKeys.LCONTROL);
        Assert.Equal(0xA3, VirtualKeys.RCONTROL);
        Assert.Equal(0x1B, VirtualKeys.ESCAPE);
        Assert.Equal(0x0D, VirtualKeys.RETURN);
        Assert.Equal(0x20, VirtualKeys.SPACE);
        Assert.Equal(0x25, VirtualKeys.LEFT);
        Assert.Equal(0x26, VirtualKeys.UP);
        Assert.Equal(0x27, VirtualKeys.RIGHT);
        Assert.Equal(0x28, VirtualKeys.DOWN);
        Assert.Equal(0xBE, VirtualKeys.OEM_PERIOD);
        Assert.Equal(0x70, VirtualKeys.F1);
        Assert.Equal(0x14, VirtualKeys.CAPITAL);
        Assert.Equal(0x2D, VirtualKeys.INSERT);
        Assert.True(VirtualKeys.IsModifier(VirtualKeys.LWIN));
        Assert.False(VirtualKeys.IsModifier(VirtualKeys.SPACE));
    }
}
