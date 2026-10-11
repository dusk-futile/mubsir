using System.Text;

namespace Basira.Core.Policy;

/// <summary>Parses and normalizes key chords ("Ctrl + Enter" -> "ctrl+enter"); modifiers in the order ctrl, shift, alt, win.</summary>
internal static class KeyChords
{
    private static readonly string[] ModifierOrder = ["ctrl", "shift", "alt", "win"];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["control"] = "ctrl", ["ctl"] = "ctrl", ["strg"] = "ctrl", ["lctrl"] = "ctrl", ["rctrl"] = "ctrl",
        ["lshift"] = "shift", ["rshift"] = "shift",
        ["option"] = "alt", ["lalt"] = "alt", ["ralt"] = "alt", ["altgr"] = "alt",
        ["windows"] = "win", ["meta"] = "win", ["super"] = "win", ["cmd"] = "win", ["command"] = "win",
        ["lwin"] = "win", ["rwin"] = "win", ["winkey"] = "win", ["start"] = "win",
        ["return"] = "enter", ["numpadenter"] = "enter", ["enterkey"] = "enter", ["ret"] = "enter",
        ["esc"] = "escape", ["del"] = "delete", ["ins"] = "insert", ["bksp"] = "backspace",
        ["pgdn"] = "pagedown", ["pgdown"] = "pagedown", ["page_down"] = "pagedown",
        ["pgup"] = "pageup", ["page_up"] = "pageup",
        ["arrowleft"] = "left", ["leftarrow"] = "left", ["arrowright"] = "right", ["rightarrow"] = "right",
        ["arrowup"] = "up", ["uparrow"] = "up", ["arrowdown"] = "down", ["downarrow"] = "down",
        ["spacebar"] = "space",
    };

    public static readonly IReadOnlySet<string> Dangerous = new HashSet<string>(StringComparer.Ordinal) { "win+r", "win+x" };

    private static readonly HashSet<string> Navigation = new(StringComparer.Ordinal)
    {
        "up", "down", "left", "right", "pageup", "pagedown", "home", "end", "tab", "shift+tab", "escape",
        "ctrl+home", "ctrl+end",
    };

    private static readonly HashSet<string> SubmitChords = new(StringComparer.Ordinal)
    {
        "enter", "ctrl+enter", "alt+s", "ctrl+shift+enter",
    };

    /// <summary>All chords in a key string; several chords may be separated by spaces or commas.</summary>
    public static List<string> Parse(string? keys)
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

        // Join spaces around '+' so "ctrl + t" stays one chord.
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i]) && (NextNonSpace(s, i) == '+' || PrevNonSpace(sb) == '+'))
            {
                continue;
            }

            sb.Append(s[i]);
        }

        foreach (var part in sb.ToString().Split([' ', ',', ';', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var chord = Normalize(part);
            if (chord.Length > 0)
            {
                result.Add(chord);
            }
        }

        return result;
    }

    public static string Normalize(string chord)
    {
        var c = chord.Trim().ToLowerInvariant();
        if (c.Length == 0)
        {
            return "";
        }

        // A trailing "++" means the plus key itself.
        bool plusKey = c == "+" || c.EndsWith("++", StringComparison.Ordinal);
        var parts = c.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<string>();
        foreach (var raw in parts)
        {
            var p = Aliases.TryGetValue(raw, out var a) ? a : raw;
            if (Array.IndexOf(ModifierOrder, p) >= 0)
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

        var ordered = ModifierOrder.Where(mods.Contains).Concat(keys);
        return string.Join('+', ordered);
    }

    public static bool IsNavigation(string chord) => Navigation.Contains(chord);

    public static bool IsSubmit(string chord) => SubmitChords.Contains(chord);

    /// <summary>Enter or Space: activates a focused button or link.</summary>
    public static bool IsActivate(string chord) => chord is "enter" or "space";

    /// <summary>A plain character (optionally with Shift): this is typing, not a shortcut.</summary>
    public static bool IsTyping(string chord)
    {
        var parts = chord.Split('+');
        var key = parts[^1];
        bool onlyShift = parts.Length == 1 || (parts.Length == 2 && parts[0] == "shift");
        return onlyShift && (key.Length == 1 || key is "space" or "plus");
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
