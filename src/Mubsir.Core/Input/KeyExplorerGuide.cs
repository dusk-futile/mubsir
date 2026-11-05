using Mubsir.Core.Localization;

namespace Mubsir.Core.Input;

public static class KeyExplorerGuide
{
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
            (ScanCodes.Insert, false) when key.VirtualKey == VirtualKeys.INSERT => StringKey.KeyHelpNvdaKey,
            _ => StringKey.KeyExplorerNoAction,
        };
    }
}
