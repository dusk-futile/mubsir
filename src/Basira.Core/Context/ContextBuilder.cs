using System.Text;
using Basira.Core.Policy;

namespace Basira.Core.Context;

/// <summary>Caps for the screen context sent to the model.</summary>
/// <param name="MaxElements">Most element lines printed.</param>
/// <param name="MaxTokens">Token budget for the header and element lines.</param>
/// <param name="IncludeVisibleText">Append <see cref="ScreenContext.VisibleText"/> under "Text:".</param>
/// <param name="MaxVisibleTextTokens">Separate token budget for the visible text.</param>
public sealed record ContextOptions(
    int MaxElements = 300,
    int MaxTokens = 3000,
    bool IncludeVisibleText = true,
    int MaxVisibleTextTokens = 2500);

/// <summary>The compact, redacted screen description and the ids it printed.</summary>
/// <param name="Text">Text for the model.</param>
/// <param name="ElementCount">Number of element lines printed.</param>
/// <param name="Truncated">True when anything was left out (elements, text, or the UIA walk itself).</param>
/// <param name="IdMap">Printed id to node, used to resolve tool calls such as invoke(id).</param>
/// <param name="EstimatedTokens"><see cref="TokenEstimator.Estimate"/> of <paramref name="Text"/>.</param>
public sealed record BuiltContext(
    string Text,
    int ElementCount,
    bool Truncated,
    IReadOnlyDictionary<int, UiNode> IdMap,
    int EstimatedTokens);

/// <summary>
/// Turns a <see cref="ScreenContext"/> into the compact text the model reads, for example:
/// <code>
/// App: Google Chrome | Window: "YouTube" | Site: youtube.com
/// Focus: [7] edit "Search"
/// [3] link "Home"
/// [7] edit "Search" value=""
/// </code>
/// Everything that can leave the PC passes through <see cref="Redactor"/>; password values are never printed.
/// </summary>
public static class ContextBuilder
{
    public const int MaxNameChars = 120;
    public const int MaxValueChars = 200;
    public const int MaxIndentDepth = 8;
    public const string PasswordMask = "•••";
    public const string HiddenMask = "[hidden]";
    public const string TruncatedNote = "Note: content truncated; only part of the window is listed.";
    public const string ElevatedNote = "Note: administrator window. Basira cannot control it; NVDA can read it.";

    private const int MaxWalkDepth = 256;
    private const int MaxSelectionTokens = 500;

    private static readonly HashSet<string> ContainerRoles = new(StringComparer.Ordinal)
    {
        "window", "pane", "document", "group", "list", "listitem", "table", "cell",
        "toolbar", "menu", "tab", "dialog", "other",
    };

    private static readonly HashSet<string> InteractiveRoles = new(StringComparer.Ordinal)
    {
        "button", "edit", "link", "checkbox", "radio", "combobox", "menuitem", "tabitem",
    };

    public static BuiltContext Build(ScreenContext ctx, ContextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var o = options ?? new ContextOptions();

        var walker = new Walker(ctx);
        walker.Run();

        // Header and focus line.
        string headerLine = HeaderLine(ctx) + "\n";
        var afterNotes = new StringBuilder();
        if (ctx.IsElevated)
        {
            afterNotes.Append(ElevatedNote).Append('\n');
        }

        var focusLine = walker.FocusLine();
        if (focusLine is not null)
        {
            afterNotes.Append("Focus: ").Append(focusLine).Append('\n');
        }

        string rest = afterNotes.ToString();
        int elementBudget = Math.Max(0, o.MaxTokens
            - TokenEstimator.Estimate(headerLine + rest)
            - TokenEstimator.Estimate(TruncatedNote + "\n"));

        bool truncated = ctx.Truncated;
        var selected = walker.Select(Math.Max(0, o.MaxElements), elementBudget, ref truncated);

        // Selection and visible text.
        string? selectionBlock = null;
        if (!string.IsNullOrWhiteSpace(ctx.Selection))
        {
            var (sel, cut) = RedactAndCap(ctx.Selection, Math.Min(MaxSelectionTokens, Math.Max(0, o.MaxVisibleTextTokens)));
            truncated |= cut;
            if (sel.Length > 0)
            {
                selectionBlock = "Selection: \"" + Escape(CollapseWhitespace(sel)) + "\"\n";
            }
        }

        string? textBlock = null;
        if (o.IncludeVisibleText && !string.IsNullOrWhiteSpace(ctx.VisibleText))
        {
            var (text, cut) = RedactAndCap(ctx.VisibleText, Math.Max(0, o.MaxVisibleTextTokens));
            truncated |= cut;
            text = TidyLines(text);
            if (text.Length > 0)
            {
                textBlock = "Text:\n" + text + "\n";
            }
        }

        var sb = new StringBuilder(headerLine.Length + rest.Length + 64 * selected.Count);
        sb.Append(headerLine);
        if (truncated)
        {
            sb.Append(TruncatedNote).Append('\n');
        }

        sb.Append(rest);
        foreach (var c in selected)
        {
            sb.Append(c.Line).Append('\n');
        }

        sb.Append(selectionBlock).Append(textBlock);

        var final = sb.ToString().TrimEnd('\n');
        var idMap = walker.BuildIdMap(selected);
        return new BuiltContext(final, selected.Count, truncated, idMap, TokenEstimator.Estimate(final));
    }

    private static string HeaderLine(ScreenContext ctx)
    {
        var parts = new List<string>(3);
        var app = FirstNonBlank(ctx.AppName, ctx.ProcessName);
        if (app is not null)
        {
            parts.Add("App: " + Clip(Redactor.Redact(CollapseWhitespace(app)), 60));
        }

        if (!string.IsNullOrWhiteSpace(ctx.WindowTitle))
        {
            parts.Add("Window: \"" + Escape(Clip(Redactor.Redact(CollapseWhitespace(ctx.WindowTitle)), MaxNameChars)) + "\"");
        }

        var site = SiteOf(ctx);
        if (site is not null)
        {
            parts.Add("Site: " + site);
        }

        return parts.Count == 0 ? "App: unknown" : string.Join(" | ", parts);
    }

    internal static string? SiteOf(ScreenContext ctx)
    {
        var host = ctx.Domain;
        if (string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(ctx.Url)
            && Uri.TryCreate(ctx.Url.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            host = uri.IdnHost;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static (string Text, bool Cut) RedactAndCap(string raw, int maxTokens)
    {
        // Cap the raw text first so huge documents are not regex-scanned in full, redact,
        // then cut to the token budget (redaction before the final cut keeps numbers whole).
        int rawCap = maxTokens * 5 + 64;
        bool cut = false;
        if (raw.Length > rawCap)
        {
            raw = raw[..SafeCut(raw, rawCap)];
            cut = true;
        }

        var redacted = Redactor.Redact(raw);
        int len = TokenEstimator.PrefixLengthWithin(redacted, maxTokens);
        if (len < redacted.Length)
        {
            cut = true;
            redacted = CutAtBoundary(redacted, len);
        }
        else if (cut)
        {
            // The raw cap may have split a number; drop the trailing digit run.
            redacted = DropTrailingDigits(redacted);
        }

        return (cut && redacted.Length > 0 ? redacted.TrimEnd() + "…" : redacted, cut);
    }

    private static string CutAtBoundary(string text, int len)
    {
        if (len <= 0)
        {
            return "";
        }

        var head = text[..len];
        int ws = head.LastIndexOfAny([' ', '\n', '\t']);
        if (ws > len - 40 && ws > 0)
        {
            head = head[..ws];
        }

        return DropTrailingDigits(head);
    }

    private static string DropTrailingDigits(string text)
    {
        int end = text.Length;
        while (end > 0 && (char.IsDigit(text[end - 1]) || text[end - 1] is ' ' or '-' or '+'))
        {
            end--;
        }

        return end == text.Length ? text : text[..end];
    }

    private static int SafeCut(string s, int at) =>
        at > 0 && at < s.Length && char.IsLowSurrogate(s[at]) ? at - 1 : at;

    private static string TidyLines(string text)
    {
        var sb = new StringBuilder(text.Length);
        int blank = 0;
        foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
            {
                blank++;
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(blank > 0 ? "\n\n" : "\n");
            }

            blank = 0;
            sb.Append(line);
        }

        return sb.ToString();
    }

    internal static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = sb.Length > 0;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    internal static string Clip(string s, int max)
    {
        if (s.Length <= max)
        {
            return s;
        }

        int cut = SafeCut(s, max - 1);
        return s[..cut].TrimEnd() + "…";
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }

        return null;
    }

    private sealed class Candidate(UiNode node, Candidate? parent, int depth, int order)
    {
        public UiNode Node { get; } = node;

        public Candidate? Parent { get; } = parent;

        public int Depth { get; } = depth;

        public int Order { get; } = order;

        public int PrintedId { get; set; }

        public bool Forced { get; set; }

        public bool Selected { get; set; }

        public string Line { get; set; } = "";
    }

    /// <summary>Walks the UIA tree once, deciding which nodes print and at which depth.</summary>
    private sealed class Walker(ScreenContext ctx)
    {
        private readonly List<Candidate> _candidates = [];
        private readonly HashSet<int> _usedIds = [];
        private readonly HashSet<UiNode> _focusPath = new(ReferenceEqualityComparer.Instance);
        private UiNode? _focused;
        private int _focusedId;
        private Candidate? _focusedCandidate;
        private int _nextId = 1;

        public void Run()
        {
            int maxId = 0;
            if (ctx.Root is not null)
            {
                MaxId(ctx.Root, 0, ref maxId);
            }

            if (ctx.Focused is { Id: > 0 } f)
            {
                maxId = Math.Max(maxId, f.Id);
            }

            _nextId = maxId + 1;

            if (ctx.Root is not null)
            {
                var path = new List<UiNode>();
                if (FindFocusPath(ctx.Root, path, 0))
                {
                    _focused = path[^1];
                    foreach (var n in path)
                    {
                        _focusPath.Add(n);
                    }
                }

                Walk(ctx.Root, parent: null, depth: 0, walkDepth: 0, isRoot: true);
            }

            _focused ??= ctx.Focused;
            if (_focusedCandidate is not null)
            {
                _focusedId = _focusedCandidate.PrintedId;
            }
            else if (_focused is not null)
            {
                _focusedId = Assign(_focused);
            }
        }

        public string? FocusLine()
        {
            if (_focused is null)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.Append('[').Append(_focusedId).Append("] ").Append(RoleText(_focused));
            var name = DisplayName(_focused);
            if (name.Length > 0)
            {
                sb.Append(" \"").Append(Escape(name)).Append('"');
            }

            return sb.ToString();
        }

        public List<Candidate> Select(int maxElements, int tokenBudget, ref bool truncated)
        {
            int count = 0;
            int tokens = 0;
            foreach (var c in _candidates.Where(c => c.Forced))
            {
                c.Selected = true;
                count++;
                tokens += TokenEstimator.Estimate(c.Line + "\n");
            }

            // Breadth-first: shallow elements first, document order within a level.
            var bfs = _candidates.Where(c => !c.Forced).OrderBy(c => c.Depth).ThenBy(c => c.Order);
            foreach (var c in bfs)
            {
                if (c.Parent is { Selected: false })
                {
                    truncated = true;
                    continue;
                }

                int cost = TokenEstimator.Estimate(c.Line + "\n");
                if (count >= maxElements || tokens + cost > tokenBudget)
                {
                    truncated = true;
                    break;
                }

                c.Selected = true;
                count++;
                tokens += cost;
            }

            if (_candidates.Any(c => !c.Selected))
            {
                truncated = true;
            }

            return _candidates.Where(c => c.Selected).ToList(); // already in document order
        }

        public Dictionary<int, UiNode> BuildIdMap(List<Candidate> selected)
        {
            var map = new Dictionary<int, UiNode>(selected.Count + 1);
            foreach (var c in selected)
            {
                map[c.PrintedId] = c.Node;
            }

            if (_focused is not null && _focusedId > 0)
            {
                map.TryAdd(_focusedId, _focused);
            }

            return map;
        }

        private void Walk(UiNode node, Candidate? parent, int depth, int walkDepth, bool isRoot)
        {
            if (walkDepth > MaxWalkDepth)
            {
                return;
            }

            bool onPath = _focusPath.Contains(node);
            if (node.IsOffscreen && !onPath)
            {
                return;
            }

            bool isFocused = ReferenceEquals(node, _focused);
            Candidate? next = parent;
            int childDepth = depth;
            if (isFocused || ShouldPrint(node, parent, isRoot))
            {
                var c = new Candidate(node, parent, depth, _candidates.Count)
                {
                    Forced = onPath,
                };
                c.PrintedId = Assign(node);
                c.Line = FormatLine(node, c.PrintedId, depth);
                _candidates.Add(c);
                if (isFocused)
                {
                    _focusedCandidate = c;
                }

                next = c;
                childDepth = depth + 1;
            }

            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    Walk(child, next, childDepth, walkDepth + 1, isRoot: false);
                }
            }
        }

        private static bool ShouldPrint(UiNode node, Candidate? parent, bool isRoot)
        {
            var role = RoleOf(node);
            if (isRoot && role == "window")
            {
                return false; // the header already names the window
            }

            bool named = !string.IsNullOrWhiteSpace(node.Name);
            if (!named)
            {
                if (!node.IsEnabled)
                {
                    return false;
                }

                // Unnamed containers are flattened; unnamed text or images say nothing.
                return InteractiveRoles.Contains(role) || (node.IsEditable && role != "document");
            }

            if (role == "text" && parent is not null
                && string.Equals(CollapseWhitespace(parent.Node.Name ?? ""), CollapseWhitespace(node.Name!), StringComparison.Ordinal))
            {
                return false; // duplicate of the parent's label, e.g. link "Home" > text "Home"
            }

            return true;
        }

        private bool FindFocusPath(UiNode node, List<UiNode> path, int depth)
        {
            if (depth > MaxWalkDepth)
            {
                return false;
            }

            path.Add(node);
            if (IsFocusedNode(node))
            {
                return true;
            }

            foreach (var child in node.Children)
            {
                if (child is not null && FindFocusPath(child, path, depth + 1))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private bool IsFocusedNode(UiNode node)
        {
            var f = ctx.Focused;
            if (f is not null)
            {
                if (ReferenceEquals(node, f))
                {
                    return true;
                }

                if (f.RuntimeId is { Length: > 0 } rid && rid == node.RuntimeId)
                {
                    return true;
                }

                if (f.RuntimeId is null && f.Id > 0 && f.Id == node.Id && f.Role == node.Role)
                {
                    return true;
                }
            }

            return f is null && node.IsFocused;
        }

        private static void MaxId(UiNode node, int depth, ref int max)
        {
            if (depth > MaxWalkDepth)
            {
                return;
            }

            if (node.Id > max)
            {
                max = node.Id;
            }

            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    MaxId(child, depth + 1, ref max);
                }
            }
        }

        private int Assign(UiNode node)
        {
            if (node.Id > 0 && _usedIds.Add(node.Id))
            {
                return node.Id;
            }

            while (!_usedIds.Add(_nextId))
            {
                _nextId++;
            }

            return _nextId++;
        }
    }

    private static string RoleOf(UiNode node) =>
        string.IsNullOrWhiteSpace(node.Role) ? "other" : node.Role.Trim().ToLowerInvariant();

    private static string RoleText(UiNode node)
    {
        var role = RoleOf(node);
        return role == "heading" && node.HeadingLevel is > 0 and < 10 ? $"heading({node.HeadingLevel})" : role;
    }

    private static string DisplayName(UiNode node)
    {
        var raw = node.Name;
        if (string.IsNullOrWhiteSpace(raw) && InteractiveRoles.Contains(RoleOf(node)))
        {
            raw = node.HelpText;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        // Some sites echo a typed password into the field's name; never let it through.
        if (node.IsPassword && !string.IsNullOrEmpty(node.Value) && raw.Contains(node.Value, StringComparison.Ordinal))
        {
            return "password";
        }

        return Clip(Redactor.Redact(CollapseWhitespace(raw)), MaxNameChars);
    }

    private static string FormatLine(UiNode node, int id, int depth)
    {
        var sb = new StringBuilder();
        sb.Append(' ', 2 * Math.Min(depth, MaxIndentDepth));
        sb.Append('[').Append(id).Append("] ").Append(RoleText(node));
        var name = DisplayName(node);
        if (name.Length > 0)
        {
            sb.Append(" \"").Append(Escape(name)).Append('"');
        }

        if (!node.IsEnabled)
        {
            sb.Append(" disabled");
        }

        if (node.IsPassword)
        {
            sb.Append(" password");
        }

        if (node.IsSearchBox)
        {
            sb.Append(" search");
        }

        if (node.IsAddressBar)
        {
            sb.Append(" address-bar");
        }

        var value = ValueText(node);
        if (value is not null)
        {
            sb.Append(" value=\"").Append(Escape(value)).Append('"');
        }

        return sb.ToString();
    }

    private static string? ValueText(UiNode node)
    {
        var role = RoleOf(node);
        if (node.IsPassword)
        {
            return PasswordMask;
        }

        bool field = role is "edit" or "combobox" || (node.IsEditable && role != "document");
        if (field && SensitiveDetector.IsSensitiveField(node))
        {
            return string.IsNullOrEmpty(node.Value) ? "" : HiddenMask;
        }

        if (field)
        {
            return Clip(Redactor.Redact(CollapseWhitespace(node.Value ?? "")), MaxValueChars);
        }

        if (role is "link" or "document" or "text" or "heading" or "image" || string.IsNullOrWhiteSpace(node.Value))
        {
            return null;
        }

        var v = CollapseWhitespace(node.Value);
        if (string.Equals(v, CollapseWhitespace(node.Name ?? ""), StringComparison.Ordinal))
        {
            return null;
        }

        return Clip(Redactor.Redact(v), MaxValueChars);
    }
}
