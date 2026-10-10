using Basira.Core.Localization;

namespace Basira.Core.Input;

/// <summary>
/// What the key explorer says for a <see cref="DescribeKey"/>: the key's role in Basira.
/// Keys are recognized by physical position, like the command layer.
/// </summary>
public static class KeyExplorerGuide
{
    /// <summary>The sentence describing <paramref name="key"/> under <paramref name="settings"/>.</summary>
    public static StringKey Describe(KeyEvent key, InputSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        bool talk = settings.TalkKey.Matches(key);
        bool layer = settings.LayerKey.Matches(key);
        if (talk)
        {
            return layer ? StringKey.KeyHelpTalkAndLayer : StringKey.KeyHelpTalk;
        }

        if (layer)
        {
            return StringKey.KeyHelpLayer;
        }

        if (VirtualKeys.IsCtrl(key.VirtualKey))
        {
            return StringKey.KeyHelpControl;
        }

        return (key.ScanCode, key.Extended) switch
        {
            (ScanCodes.Space, false) => StringKey.KeyHelpSpace,
            (ScanCodes.Left, true) => StringKey.KeyHelpLeftArrow,
            (ScanCodes.Right, true) => StringKey.KeyHelpRightArrow,
            (ScanCodes.Up, true) => StringKey.KeyHelpUpArrow,
            (ScanCodes.Down, true) => StringKey.KeyHelpDownArrow,
            (ScanCodes.Period, false) => StringKey.KeyHelpPeriod,
            (ScanCodes.Enter, _) => StringKey.KeyHelpEnter,
            (ScanCodes.F1, _) => StringKey.KeyHelpF1,
            (ScanCodes.CapsLock, false) => StringKey.KeyHelpNvdaKey,
            (ScanCodes.Insert, true) => StringKey.KeyHelpNvdaKey,
            (ScanCodes.Insert, false) when key.VirtualKey == VirtualKeys.INSERT => StringKey.KeyHelpNvdaKey, // numpad Insert, NumLock off
            _ => StringKey.KeyExplorerNoAction,
        };
    }
}
