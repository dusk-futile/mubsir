using System.Text;

namespace Mubsir.App.Actions;

internal sealed record KeyStroke(IReadOnlyList<ushort> Modifiers, ushort Key, string Chord);

internal static class KeyChordParser
{
    public const ushort VkShift = 0x10;
    public const ushort VkControl = 0x11;
    public const ushort VkMenu = 0x12;
    public const ushort VkLWin = 0x5B;
    public const ushort VkReturn = 0x0D;

    private static readonly string[] ModifierOrder = ["ctrl", "shift", "alt", "win"];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["control"] = "ctrl",
        ["ctl"] = "ctrl",
        ["lctrl"] = "ctrl",
        ["rctrl"] = "ctrl",
        ["lshift"] = "shift",
        ["rshift"] = "shift",
        ["option"] = "alt",
        ["lalt"] = "alt",
        ["ralt"] = "alt",
        ["altgr"] = "alt",
        ["windows"] = "win",
        ["meta"] = "win",
        ["super"] = "win",
        ["cmd"] = "win",
        ["command"] = "win",
        ["lwin"] = "win",
        ["rwin"] = "win",
        ["winkey"] = "win",
        ["start"] = "win",
        ["return"] = "enter",
        ["numpadenter"] = "enter",
        ["ret"] = "enter",
        ["esc"] = "escape",
        ["del"] = "delete",
        ["ins"] = "insert",
        ["bksp"] = "backspace",
        ["pgdn"] = "pagedown",
        ["pgdown"] = "pagedown",
        ["page_down"] = "pagedown",
        ["pgup"] = "pageup",
        ["page_up"] = "pageup",
        ["arrowleft"] = "left",
        ["leftarrow"] = "left",
        ["arrowright"] = "right",
        ["rightarrow"] = "right",
        ["arrowup"] = "up",
        ["uparrow"] = "up",
        ["arrowdown"] = "down",
        ["downarrow"] = "down",
        ["spacebar"] = "space",
        ["volume_up"] = "volumeup",
        ["volume_down"] = "volumedown",
        ["mute"] = "volumemute",
        ["volume_mute"] = "volumemute",
        ["playpause"] = "mediaplaypause",
        ["play_pause"] = "mediaplaypause",
        ["nexttrack"] = "medianext",
        ["prevtrack"] = "mediaprev",
        ["mediaprevious"] = "mediaprev",
    };

    private static readonly Dictionary<string, ushort> Modifiers = new(StringComparer.Ordinal)
    {
        ["ctrl"] = VkControl,
        ["shift"] = VkShift,
        ["alt"] = VkMenu,
        ["win"] = VkLWin,
    };

    private static readonly Dictionary<string, ushort> Named = new(StringComparer.Ordinal)
    {
        ["enter"] = VkReturn,
        ["tab"] = 0x09,
        ["escape"] = 0x1B,
        ["space"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E,
        ["insert"] = 0x2D,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["left"] = 0x25,
        ["up"] = 0x26,
        ["right"] = 0x27,
        ["down"] = 0x28,
        ["volumemute"] = 0xAD,
        ["volumedown"] = 0xAE,
        ["volumeup"] = 0xAF,
        ["medianext"] = 0xB0,
        ["mediaprev"] = 0xB1,
        ["mediastop"] = 0xB2,
        ["mediaplaypause"] = 0xB3,
        ["apps"] = 0x5D,
        ["menu"] = 0x5D,
        ["printscreen"] = 0x2C,
        ["plus"] = 0xBB,
        ["minus"] = 0xBD,
        ["comma"] = 0xBC,
        ["period"] = 0xBE,
        [","] = 0xBC,
        ["."] = 0xBE,
        ["-"] = 0xBD,
        ["="] = 0xBB,
        ["/"] = 0xBF,
        [";"] = 0xBA,
        ["["] = 0xDB,
        ["]"] = 0xDD,
        ["\\"] = 0xDC,
        ["'"] = 0xDE,
        ["`"] = 0xC0,
    };

    private static readonly HashSet<ushort> ExtendedKeys =
[
    0x2E, 0x2D, 0x24, 0x23, 0x21, 0x22, 0x25, 0x26, 0x27, 0x28, VkLWin, 0x5D,
        0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB2, 0xB3, 0x2C,
    ];

    public static bool IsExtended(ushort vk) => ExtendedKeys.Contains(vk);

    public static List<string> Normalize(string? keys)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(keys))
        {
            return result;
        }

        var s = keys.Trim().ToLowerInvariant()
            .Replace("page down", "pagedown").Replace("page up", "pageup")
            .Replace("left arrow", "left").Replace("right arrow", "right")
            .Replace("up arrow", "up").Replace("down arrow", "down");

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i]) && (NextNonSpace(s, i) == '+' || PrevNonSpace(sb) == '+'))
            {
                continue;
            }

            sb.Append(s[i]);
        }

        foreach (var part in sb.ToString().Split([' ', ';', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var chord = NormalizeChord(part);
            if (chord.Length > 0)
            {
                result.Add(chord);
            }
        }

        return result;
    }

    public static string NormalizeChord(string chord)
    {
        var c = chord.Trim().ToLowerInvariant();
        if (c.Length == 0)
        {
            return "";
        }

        bool plusKey = c == "+" || c.EndsWith("++", StringComparison.Ordinal);
        var mods = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();
        foreach (var raw in c.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = Aliases.TryGetValue(raw, out var a) ? a : raw;
            if (Modifiers.ContainsKey(p))
            {
                mods.Add(p);
            }
            else
            {
                keys.Add(p);
            }
        }

        if (plusKey)
        {
            keys.Add("plus");
        }

        return string.Join('+', ModifierOrder.Where(mods.Contains).Concat(keys));
    }

    public static KeyStroke? Parse(string chord)
    {
        var normalized = NormalizeChord(chord);
        if (normalized.Length == 0)
        {
            return null;
        }

        var parts = normalized.Split('+');
        var mods = new List<ushort>();
        ushort? key = null;
        foreach (var p in parts)
        {
            if (Modifiers.TryGetValue(p, out var m))
            {
                mods.Add(m);
                continue;
            }

            if (key is not null)
            {
                return null;
            }

            key = KeyOf(p);
            if (key is null)
            {
                return null;
            }
        }

        if (key is null)
        {
            if (mods.Count == 1)
            {
                return new KeyStroke([], mods[0], normalized);
            }

            return null;
        }

        return new KeyStroke(mods, key.Value, normalized);
    }

    public static List<KeyStroke>? ParseAll(string? keys)
    {
        var chords = Normalize(keys);
        if (chords.Count == 0)
        {
            return null;
        }

        var strokes = new List<KeyStroke>(chords.Count);
        foreach (var c in chords)
        {
            if (Parse(c) is not { } stroke)
            {
                return null;
            }

            strokes.Add(stroke);
        }

        return strokes;
    }

    private static ushort? KeyOf(string name)
    {
        if (Named.TryGetValue(name, out var vk))
        {
            return vk;
        }

        if (name.Length == 1)
        {
            char ch = name[0];
            if (ch is >= 'a' and <= 'z')
            {
                return (ushort)char.ToUpperInvariant(ch);
            }

            if (ch is >= '0' and <= '9')
            {
                return ch;
            }
        }

        if (name.Length is >= 2 and <= 3 && name[0] == 'f' && int.TryParse(name.AsSpan(1), out int f) && f is >= 1 and <= 24)
        {
            return (ushort)(0x70 + f - 1);
        }

        return null;
    }

    private static char NextNonSpace(string s, int i)
    {
        for (int k = i + 1; k < s.Length; k++)
        {
            if (!char.IsWhiteSpace(s[k]))
            {
                return s[k];
            }
        }

        return '\0';
    }

    private static char PrevNonSpace(StringBuilder sb)
    {
        for (int k = sb.Length - 1; k >= 0; k--)
        {
            if (!char.IsWhiteSpace(sb[k]))
            {
                return sb[k];
            }
        }

        return '\0';
    }
}
