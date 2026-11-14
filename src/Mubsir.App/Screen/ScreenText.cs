using System.Text;
using Mubsir.Core.Common;
using Mubsir.Core.Context;

namespace Mubsir.App.Screen;

internal static class ScreenText
{
    private static readonly Dictionary<string, (string Ar, string En)> Roles = new(StringComparer.Ordinal)
    {
        ["button"] = ("زر", "button"),
        ["edit"] = ("حقل نص", "edit field"),
        ["link"] = ("رابط", "link"),
        ["heading"] = ("عنوان", "heading"),
        ["text"] = ("نص", "text"),
        ["list"] = ("قائمة", "list"),
        ["listitem"] = ("عنصر قائمة", "list item"),
        ["checkbox"] = ("مربع اختيار", "check box"),
        ["radio"] = ("زر اختيار", "radio button"),
        ["combobox"] = ("قائمة منسدلة", "combo box"),
        ["menu"] = ("قائمة", "menu"),
        ["menuitem"] = ("عنصر قائمة", "menu item"),
        ["tab"] = ("علامات تبويب", "tab list"),
        ["tabitem"] = ("علامة تبويب", "tab"),
        ["image"] = ("صورة", "image"),
        ["table"] = ("جدول", "table"),
        ["cell"] = ("خلية", "cell"),
        ["dialog"] = ("مربع حوار", "dialog"),
        ["toolbar"] = ("شريط أدوات", "toolbar"),
        ["group"] = ("مجموعة", "group"),
        ["document"] = ("مستند", "document"),
        ["window"] = ("نافذة", "window"),
        ["pane"] = ("لوحة", "pane"),
        ["other"] = ("عنصر", "item"),
    };

    private static readonly HashSet<string> TextRoles = new(StringComparer.Ordinal)
    {
        "text", "heading", "link", "button", "listitem", "menuitem", "tabitem", "checkbox", "radio", "cell", "image",
    };

    public static string RoleName(UiNode node, Lang lang)
    {
        if (node.IsPassword)
        {
            return lang == Lang.Arabic ? "حقل كلمة مرور" : "password field";
        }

        if (node.IsSearchBox)
        {
            return lang == Lang.Arabic ? "مربع بحث" : "search box";
        }

        if (node.IsAddressBar)
        {
            return lang == Lang.Arabic ? "شريط العناوين" : "address bar";
        }

        var role = string.IsNullOrWhiteSpace(node.Role) ? "other" : node.Role;
        var (ar, en) = Roles.TryGetValue(role, out var names) ? names : Roles["other"];
        var name = lang == Lang.Arabic ? ar : en;
        if (role == "heading" && node.HeadingLevel is > 0 and < 10)
        {
            name += lang == Lang.Arabic ? $" مستوى {node.HeadingLevel}" : $" level {node.HeadingLevel}";
        }

        return name;
    }

    public static string Describe(UiNode node, Lang lang)
    {
        var role = RoleName(node, lang);
        var name = Short(node.Name ?? (node.IsPassword ? null : node.HelpText), 80);
        if (name is null)
        {
            return role;
        }

        return lang == Lang.Arabic ? $"{role} «{name}»" : $"{role} \"{name}\"";
    }

    public static string OnScreenText(UiNode? root, int maxChars)
    {
        if (root is null || maxChars <= 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        string? last = null;
        var stack = new Stack<(UiNode Node, string? ParentName)>();
        stack.Push((root, null));
        while (stack.Count > 0 && sb.Length < maxChars)
        {
            var (node, parentName) = stack.Pop();
            if (node.IsOffscreen)
            {
                continue;
            }

            string? line = null;
            var name = node.Name?.Trim();
            if (!string.IsNullOrEmpty(name) && TextRoles.Contains(node.Role) && name != parentName)
            {
                line = name;
            }

            if (!node.IsPassword && node.IsEditable && !string.IsNullOrWhiteSpace(node.Value) && node.Role != "document")
            {
                line = line is null ? node.Value.Trim() : line + ": " + node.Value.Trim();
            }

            if (line is not null && line != last)
            {
                sb.Append(line).Append('\n');
                last = line;
            }

            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push((node.Children[i], name));
            }
        }

        var text = sb.ToString().TrimEnd();
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    public static string? Short(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        var t = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }
}
