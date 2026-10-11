using System.Collections.Frozen;

namespace Mubsir.App.Screen;

internal static class UiaClassifier
{
    public static readonly FrozenSet<string> BrowserProcesses = new[]
{
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> AddressBarNames = new[]
    {
        "Address and search bar", "شريط العناوين والبحث", "Search or enter address", "Search with Google or enter address",
        "Address bar", "شريط العناوين",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static string Role(int controlType, string? ariaRole, string? className, bool isDialog, int? headingLevel)
    {
        var aria = (ariaRole ?? "").Trim().ToLowerInvariant();
        if (headingLevel is > 0 || aria == "heading")
        {
            return "heading";
        }

        if (isDialog || aria is "dialog" or "alertdialog")
        {
            return "dialog";
        }

        string role = controlType switch
        {
            UiaIds.Button or UiaIds.SplitButton => "button",
            UiaIds.CheckBox => "checkbox",
            UiaIds.RadioButton => "radio",
            UiaIds.ComboBox => "combobox",
            UiaIds.Edit => "edit",
            UiaIds.Hyperlink => "link",
            UiaIds.Image => "image",
            UiaIds.ListItem or UiaIds.TreeItem or UiaIds.DataItem => "listitem",
            UiaIds.List or UiaIds.Tree or UiaIds.DataGrid => "list",
            UiaIds.Menu or UiaIds.MenuBar => "menu",
            UiaIds.MenuItem => "menuitem",
            UiaIds.Tab => "tab",
            UiaIds.TabItem => "tabitem",
            UiaIds.Text => "text",
            UiaIds.ToolBar => "toolbar",
            UiaIds.Group => "group",
            UiaIds.Document => "document",
            UiaIds.Window => className == "#32770" ? "dialog" : "window",
            UiaIds.Pane => "pane",
            UiaIds.Table => "table",
            UiaIds.HeaderItem => "cell",
            _ => "other",
        };

        if (role is "other" or "group" or "pane" or "text")
        {
            role = aria switch
            {
                "button" => "button",
                "link" => "link",
                "textbox" or "searchbox" => "edit",
                "checkbox" or "switch" => "checkbox",
                "radio" => "radio",
                "combobox" => "combobox",
                "listbox" or "list" => role == "text" ? role : "list",
                "option" or "listitem" => role == "text" ? role : "listitem",
                "menuitem" => "menuitem",
                "tab" => "tabitem",
                "tablist" => "tab",
                "img" => "image",
                "table" or "grid" => "table",
                "cell" or "gridcell" => "cell",
                _ => role,
            };
        }

        return role;
    }

    public static int? HeadingLevel(int uiaHeadingLevel, string? ariaRole, string? ariaProperties)
    {
        if (uiaHeadingLevel is > UiaIds.HeadingLevelNone and <= UiaIds.HeadingLevelNone + 9)
        {
            return uiaHeadingLevel - UiaIds.HeadingLevelNone;
        }

        if (string.Equals(ariaRole?.Trim(), "heading", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(AriaProperty(ariaProperties, "level"), out var level) && level is > 0 and < 10)
        {
            return level;
        }

        return null;
    }

    public static string? AriaProperty(string? ariaProperties, string key)
    {
        if (string.IsNullOrEmpty(ariaProperties))
        {
            return null;
        }

        foreach (var (k, v) in ParseAria(ariaProperties))
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }

        return null;
    }

    internal static IEnumerable<(string Key, string Value)> ParseAria(string text)
    {
        var key = new System.Text.StringBuilder();
        var value = new System.Text.StringBuilder();
        bool inValue = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                (inValue ? value : key).Append(text[++i]);
                continue;
            }

            if (c == ';')
            {
                if (key.Length > 0)
                {
                    yield return (key.ToString().Trim(), value.ToString().Trim());
                }

                key.Clear();
                value.Clear();
                inValue = false;
                continue;
            }

            if (c == '=' && !inValue)
            {
                inValue = true;
                continue;
            }

            (inValue ? value : key).Append(c);
        }

        if (key.Length > 0)
        {
            yield return (key.ToString().Trim(), value.ToString().Trim());
        }
    }

    public static bool IsSearchBox(string role, string? ariaRole, string? name, string? automationId, string? className)
    {
        if (string.Equals(ariaRole?.Trim(), "searchbox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (role is not ("edit" or "combobox"))
        {
            return false;
        }

        return Mentions(name) || Mentions(automationId) || Mentions(className);

        static bool Mentions(string? s) =>
            !string.IsNullOrEmpty(s)
            && (s.Contains("search", StringComparison.OrdinalIgnoreCase) || s.Contains("بحث", StringComparison.Ordinal)
                || s.Contains("ابحث", StringComparison.Ordinal));
    }

    public static bool IsAddressBar(string role, string? name, string? automationId, string? className, bool inBrowser)
    {
        if (!inBrowser || role is not ("edit" or "combobox"))
        {
            return false;
        }

        if (string.Equals(className, "OmniboxViewViews", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(automationId, "urlbar-input", StringComparison.Ordinal) || string.Equals(automationId, "urlbar", StringComparison.Ordinal))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(name) && AddressBarNames.Contains(name.Trim());
    }

    public static bool IsEditable(int controlType, bool hasValuePattern, bool valueReadOnly, bool hasTextPattern, bool isEnabled)
    {
        if (!isEnabled)
        {
            return false;
        }

        if (controlType is UiaIds.Edit or UiaIds.ComboBox)
        {
            return hasValuePattern ? !valueReadOnly : hasTextPattern;
        }

        if (controlType == UiaIds.Document)
        {
            return hasValuePattern && !valueReadOnly;
        }

        return (controlType is UiaIds.Group or UiaIds.Custom or UiaIds.Pane) && hasValuePattern && !valueReadOnly;
    }

    public static string? ProcessBaseName(string? processNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(processNameOrPath))
        {
            return null;
        }

        var name = Path.GetFileName(processNameOrPath.Trim());
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    public static bool IsBrowser(string? processNameOrPath) =>
        ProcessBaseName(processNameOrPath) is { } n && BrowserProcesses.Contains(n);

    public static (string? Url, string? Domain) UrlAndDomain(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var text = raw.Trim();
        if (text.Contains(' ', StringComparison.Ordinal) || text.Length > 2048)
        {
            return (null, null);
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            if (!text.Contains('.', StringComparison.Ordinal))
            {
                return (null, null);
            }

            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return (null, null);
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return (uri.AbsoluteUri, host.Length == 0 ? null : host);
    }

    public static string? RuntimeIdKey(int[]? runtimeId) =>
    runtimeId is { Length: > 0 } ? string.Join('.', runtimeId) : null;
}
