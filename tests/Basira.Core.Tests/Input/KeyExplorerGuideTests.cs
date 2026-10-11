using Basira.Core.Common;
using Basira.Core.Input;
using Basira.Core.Localization;

namespace Basira.Core.Tests.Input;

public class KeyExplorerGuideTests
{
    private static KeyEvent Down(KeyIdentity k) => new(k.VirtualKey, k.ScanCode, k.Extended, true, false, 0);

    [Fact]
    public void DefaultTalkKeyIsAlsoTheLayerKey()
    {
        Assert.Equal(StringKey.KeyHelpTalkAndLayer, KeyExplorerGuide.Describe(Down(K.RightCtrl), new InputSettings()));
    }

    [Fact]
    public void SeparateTalkAndLayerKeys()
    {
        var s = new InputSettings { TalkKey = K.Apps, LayerKey = K.RightCtrl };

        Assert.Equal(StringKey.KeyHelpTalk, KeyExplorerGuide.Describe(Down(K.Apps), s));
        Assert.Equal(StringKey.KeyHelpLayer, KeyExplorerGuide.Describe(Down(K.RightCtrl), s));
        Assert.Equal(StringKey.KeyHelpControl, KeyExplorerGuide.Describe(Down(K.LeftCtrl), s));
    }

    [Theory]
    [InlineData(nameof(K.LeftCtrl), StringKey.KeyHelpControl)]
    [InlineData(nameof(K.Space), StringKey.KeyHelpSpace)]
    [InlineData(nameof(K.Left), StringKey.KeyHelpLeftArrow)]
    [InlineData(nameof(K.Right), StringKey.KeyHelpRightArrow)]
    [InlineData(nameof(K.Up), StringKey.KeyHelpUpArrow)]
    [InlineData(nameof(K.Down), StringKey.KeyHelpDownArrow)]
    [InlineData(nameof(K.Period), StringKey.KeyHelpPeriod)]
    [InlineData(nameof(K.Enter), StringKey.KeyHelpEnter)]
    [InlineData(nameof(K.NumpadEnter), StringKey.KeyHelpEnter)]
    [InlineData(nameof(K.F1), StringKey.KeyHelpF1)]
    [InlineData(nameof(K.Insert), StringKey.KeyHelpNvdaKey)]
    [InlineData(nameof(K.A), StringKey.KeyExplorerNoAction)]
    [InlineData(nameof(K.Tab), StringKey.KeyExplorerNoAction)]
    [InlineData(nameof(K.NumpadLeft), StringKey.KeyExplorerNoAction)]
    public void LayerAndSpecialKeys(string keyName, StringKey expected)
    {
        var k = (KeyIdentity)typeof(K).GetField(keyName)!.GetValue(null)!;

        Assert.Equal(expected, KeyExplorerGuide.Describe(Down(k), new InputSettings()));
    }

    [Fact]
    public void CapsLockIsTheLaptopNvdaKey_NumpadZeroIsNot()
    {
        var capsLock = new KeyIdentity(VirtualKeys.CAPITAL, ScanCodes.CapsLock, false);
        var numpadInsert = new KeyIdentity(VirtualKeys.INSERT, ScanCodes.Insert, false);
        var numpadZero = new KeyIdentity(0x60, ScanCodes.Insert, false);

        Assert.Equal(StringKey.KeyHelpNvdaKey, KeyExplorerGuide.Describe(Down(capsLock), new InputSettings()));
        Assert.Equal(StringKey.KeyHelpNvdaKey, KeyExplorerGuide.Describe(Down(numpadInsert), new InputSettings()));
        Assert.Equal(StringKey.KeyExplorerNoAction, KeyExplorerGuide.Describe(Down(numpadZero), new InputSettings()));
    }

    [Fact]
    public void EveryDescriptionIsSpeakableInBothLanguages()
    {
        foreach (KeyIdentity k in new[] { K.RightCtrl, K.LeftCtrl, K.Space, K.Left, K.Right, K.Up, K.Down, K.Period, K.Enter, K.F1, K.Insert, K.A })
        {
            StringKey key = KeyExplorerGuide.Describe(Down(k), new InputSettings());
            Assert.False(string.IsNullOrWhiteSpace(Strings.Get(key, Lang.Arabic)));
            Assert.False(string.IsNullOrWhiteSpace(Strings.Get(key, Lang.English)));
        }

        Assert.Throws<ArgumentNullException>(() => KeyExplorerGuide.Describe(Down(K.A), null!));
    }
}
