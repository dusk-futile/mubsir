using System.Diagnostics;
using System.Runtime.InteropServices;
using Mubsir.Core.Context;
using Interop.UIAutomationClient;

namespace Mubsir.App.Screen;

internal sealed record UiaWalk(
    UiNode? Root,
    UiNode? Focused,
    IReadOnlyDictionary<int, IUIAutomationElement> Elements,
    IUIAutomationElement? TextElement,
    string? Url,
    string? Domain,
    bool Truncated);

internal static class UiaSnapshotter
{
    public const int MaxDepth = 12;
    public const int MaxNodes = 300;
    public const int MaxValueChars = 2000;

    private static readonly int[] CachedProperties =
    [
        UiaIds.RuntimeId, UiaIds.ControlType, UiaIds.LocalizedControlType, UiaIds.Name, UiaIds.AutomationId, UiaIds.ClassName,
        UiaIds.IsEnabled, UiaIds.IsOffscreen, UiaIds.IsPassword, UiaIds.HasKeyboardFocus, UiaIds.HelpText, UiaIds.AriaRole,
        UiaIds.AriaProperties, UiaIds.ValueValue, UiaIds.ValueIsReadOnly, UiaIds.IsInvokePatternAvailable,
        UiaIds.IsValuePatternAvailable, UiaIds.IsTogglePatternAvailable, UiaIds.IsSelectionItemPatternAvailable,
        UiaIds.IsExpandCollapsePatternAvailable, UiaIds.IsScrollPatternAvailable, UiaIds.IsTextPatternAvailable,
        UiaIds.HeadingLevel, UiaIds.IsDialog, UiaIds.ProcessId,
    ];

    public static IUIAutomationCacheRequest CreateCacheRequest(IUIAutomation uia)
    {
        var request = uia.CreateCacheRequest();
        foreach (var id in CachedProperties)
        {
            try
            {
                request.AddProperty(id);
            }
            catch (COMException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        request.TreeScope = TreeScope.TreeScope_Element;
        request.AutomationElementMode = AutomationElementMode.AutomationElementMode_Full;
        return request;
    }

    public static UiaWalk Walk(IUIAutomation uia, nint hwnd, bool inBrowser, ReadScope scope, TimeSpan budget, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        var request = CreateCacheRequest(uia);
        var root = uia.ElementFromHandleBuildCache(hwnd, request);
        if (root is null)
        {
            return new UiaWalk(null, null, new Dictionary<int, IUIAutomationElement>(), null, null, null, true);
        }

        var state = new WalkState(uia, request, inBrowser, budget, clock, ct);
        var rootDraft = state.Make(root, 0);
        var focusChain = state.FocusChain(rootDraft);

        NodeDraft treeRoot;
        switch (scope)
        {
            case ReadScope.Focus when focusChain.Count > 0:
                treeRoot = state.WalkFocus(rootDraft, focusChain);
                break;
            case ReadScope.Document:
                treeRoot = state.FindDocument(rootDraft) ?? rootDraft;
                state.Breadth(treeRoot, focusChain);
                break;
            default:
                treeRoot = rootDraft;
                state.Breadth(treeRoot, focusChain);
                break;
        }

        NodeDraft? focusedDraft = focusChain.Count > 0 ? focusChain[^1] : null;
        if (focusedDraft is not null && state.FindByRuntimeId(treeRoot, focusedDraft.RuntimeId) is { } inTree)
        {
            focusedDraft = inTree;
        }

        var textDraft = state.DocumentDraft ?? (state.Truncated ? state.FindDocument(rootDraft, searchOnly: true) : null);
        if (textDraft is null && focusedDraft is { HasText: true, ControlType: UiaIds.Edit or UiaIds.Document })
        {
            textDraft = focusedDraft;
        }

        string? url = null;
        string? domain = null;
        if (inBrowser)
        {
            var raw = state.AddressBarValue ?? state.DocumentDraft?.Value ?? textDraft?.Value;
            (url, domain) = UiaClassifier.UrlAndDomain(raw);
        }

        var built = new Dictionary<NodeDraft, UiNode>(ReferenceEqualityComparer.Instance);
        var rootNode = state.Build(treeRoot, built);
        UiNode? focusedNode = null;
        if (focusedDraft is not null)
        {
            focusedNode = built.TryGetValue(focusedDraft, out var n) ? n : state.Build(focusedDraft, built, withChildren: false);
        }

        var elements = new Dictionary<int, IUIAutomationElement>(built.Count);
        foreach (var (draft, node) in built)
        {
            elements[node.Id] = draft.Element;
        }

        return new UiaWalk(rootNode, focusedNode, elements, textDraft?.Element, url, domain, state.Truncated);
    }

    public static UiNode? Describe(IUIAutomation uia, IUIAutomationElement? element, bool inBrowser)
    {
        if (element is null)
        {
            return null;
        }

        var request = CreateCacheRequest(uia);
        var cached = element.BuildUpdatedCache(request);
        var state = new WalkState(uia, request, inBrowser, TimeSpan.FromSeconds(1), Stopwatch.StartNew(), CancellationToken.None);
        var draft = state.Make(cached, 0);
        return state.Build(draft, new Dictionary<NodeDraft, UiNode>(ReferenceEqualityComparer.Instance), withChildren: false);
    }

    internal static string? CachedString(IUIAutomationElement e, int property)
    {
        try
        {
            return e.GetCachedPropertyValue(property) as string;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    internal static bool CachedBool(IUIAutomationElement e, int property, bool fallback = false)
    {
        try
        {
            return e.GetCachedPropertyValue(property) is bool b ? b : fallback;
        }
        catch (COMException)
        {
            return fallback;
        }
    }

    internal static int CachedInt(IUIAutomationElement e, int property, int fallback = 0)
    {
        try
        {
            return e.GetCachedPropertyValue(property) is int i ? i : fallback;
        }
        catch (COMException)
        {
            return fallback;
        }
    }

    internal static string? CachedRuntimeId(IUIAutomationElement e)
    {
        try
        {
            return e.GetCachedPropertyValue(UiaIds.RuntimeId) is int[] ids ? UiaClassifier.RuntimeIdKey(ids) : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal sealed class NodeDraft(IUIAutomationElement element)
    {
        public IUIAutomationElement Element { get; } = element;

        public int Id { get; set; }

        public int Depth { get; set; }

        public int ControlType { get; set; }

        public string? Name { get; set; }

        public string? AutomationId { get; set; }

        public string? ClassName { get; set; }

        public string? HelpText { get; set; }

        public string? AriaRole { get; set; }

        public string? AriaProperties { get; set; }

        public string? Value { get; set; }

        public string? RuntimeId { get; set; }

        public int ProcessId { get; set; }

        public int HeadingRaw { get; set; }

        public bool IsEnabled { get; set; } = true;

        public bool IsOffscreen { get; set; }

        public bool IsPassword { get; set; }

        public bool HasFocus { get; set; }

        public bool IsDialog { get; set; }

        public bool HasValue { get; set; }

        public bool ValueReadOnly { get; set; }

        public bool HasText { get; set; }

        public List<NodeDraft> Children { get; } = [];

        public bool ChildrenRead { get; set; }
    }

    private sealed class WalkState(
        IUIAutomation uia, IUIAutomationCacheRequest request, bool inBrowser, TimeSpan budget, Stopwatch clock, CancellationToken ct)
    {
        private readonly Dictionary<string, NodeDraft> _byRuntimeId = new(StringComparer.Ordinal);
        private int _nextId = 1;
        private int _count;

        public bool Truncated { get; private set; }

        public string? AddressBarValue { get; private set; }

        public NodeDraft? DocumentDraft { get; private set; }

        private bool OutOfBudget => clock.Elapsed > budget || ct.IsCancellationRequested;

        public NodeDraft Make(IUIAutomationElement e, int depth)
        {
            var d = new NodeDraft(e)
            {
                Id = _nextId++,
                Depth = depth,
                ControlType = CachedInt(e, UiaIds.ControlType),
                Name = Trim(CachedString(e, UiaIds.Name)),
                AutomationId = Trim(CachedString(e, UiaIds.AutomationId)),
                ClassName = Trim(CachedString(e, UiaIds.ClassName)),
                HelpText = Trim(CachedString(e, UiaIds.HelpText)),
                AriaRole = Trim(CachedString(e, UiaIds.AriaRole)),
                AriaProperties = Trim(CachedString(e, UiaIds.AriaProperties)),
                RuntimeId = CachedRuntimeId(e),
                ProcessId = CachedInt(e, UiaIds.ProcessId),
                HeadingRaw = CachedInt(e, UiaIds.HeadingLevel, UiaIds.HeadingLevelNone),
                IsEnabled = CachedBool(e, UiaIds.IsEnabled, true),
                IsOffscreen = CachedBool(e, UiaIds.IsOffscreen),
                IsPassword = CachedBool(e, UiaIds.IsPassword),
                HasFocus = CachedBool(e, UiaIds.HasKeyboardFocus),
                IsDialog = CachedBool(e, UiaIds.IsDialog),
                HasValue = CachedBool(e, UiaIds.IsValuePatternAvailable),
                ValueReadOnly = CachedBool(e, UiaIds.ValueIsReadOnly, true),
                HasText = CachedBool(e, UiaIds.IsTextPatternAvailable),
            };

            if (!d.IsPassword && d.HasValue)
            {
                var v = CachedString(e, UiaIds.ValueValue);
                d.Value = v is { Length: > MaxValueChars } ? v[..MaxValueChars] : v;
            }

            if (d.RuntimeId is not null)
            {
                _byRuntimeId.TryAdd(d.RuntimeId, d);
            }

            var role = UiaClassifier.Role(d.ControlType, d.AriaRole, d.ClassName, d.IsDialog, null);
            if (AddressBarValue is null && UiaClassifier.IsAddressBar(role, d.Name, d.AutomationId, d.ClassName, inBrowser))
            {
                AddressBarValue = d.Value;
            }

            if (DocumentDraft is null && d.ControlType == UiaIds.Document && d.HasText)
            {
                DocumentDraft = d;
            }

            _count++;
            return d;
        }

        public List<NodeDraft> FocusChain(NodeDraft root)
        {
            var chain = new List<NodeDraft>();
            try
            {
                var focused = uia.GetFocusedElementBuildCache(request);
                if (focused is null)
                {
                    return chain;
                }

                var walker = uia.ControlViewWalker;
                var current = focused;
                var up = new List<IUIAutomationElement>();
                for (int i = 0; i < 40 && current is not null && !OutOfBudget; i++)
                {
                    if (SameElement(current, root))
                    {
                        chain.Add(root);
                        for (int k = up.Count - 1; k >= 0; k--)
                        {
                            chain.Add(Make(up[k], chain.Count));
                        }

                        return chain;
                    }

                    up.Add(current);
                    current = walker.GetParentElementBuildCache(current, request);
                }
            }
            catch (COMException)
            {
            }
            catch (InvalidCastException)
            {
            }

            chain.Clear();
            return chain;
        }

        public NodeDraft WalkFocus(NodeDraft root, List<NodeDraft> chain)
        {
            for (int i = 0; i + 1 < chain.Count; i++)
            {
                chain[i].Children.Add(chain[i + 1]);
                chain[i].ChildrenRead = true;
            }

            var focused = chain[^1];
            if (chain.Count >= 2)
            {
                var parent = chain[^2];
                parent.Children.Clear();
                foreach (var sibling in ReadChildren(parent))
                {
                    parent.Children.Add(sibling.RuntimeId is not null && sibling.RuntimeId == focused.RuntimeId ? focused : sibling);
                }

                if (!parent.Children.Contains(focused))
                {
                    parent.Children.Add(focused);
                }
            }

            if (!OutOfBudget)
            {
                focused.Children.AddRange(ReadChildren(focused));
            }

            return root;
        }

        public void Breadth(NodeDraft start, List<NodeDraft> focusChain)
        {
            var focusIds = new HashSet<string>(focusChain.Where(d => d.RuntimeId is not null).Select(d => d.RuntimeId!), StringComparer.Ordinal);
            var queue = new Queue<NodeDraft>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                if (_count >= MaxNodes || OutOfBudget)
                {
                    Truncated = true;
                    return;
                }

                var d = queue.Dequeue();
                if (d.Depth - start.Depth >= MaxDepth)
                {
                    Truncated = true;
                    continue;
                }

                if (d.IsOffscreen && (d.RuntimeId is null || !focusIds.Contains(d.RuntimeId)))
                {
                    continue;
                }

                foreach (var child in ReadChildren(d))
                {
                    d.Children.Add(child);
                    queue.Enqueue(child);
                    if (_count >= MaxNodes)
                    {
                        Truncated = true;
                        break;
                    }
                }
            }
        }

        public NodeDraft? FindDocument(NodeDraft root, bool searchOnly = false)
        {
            if (DocumentDraft is not null)
            {
                return DocumentDraft;
            }

            if (root.ControlType == UiaIds.Document)
            {
                return root;
            }

            if (OutOfBudget && searchOnly)
            {
                return null;
            }

            try
            {
                var condition = uia.CreatePropertyCondition(UiaIds.ControlType, UiaIds.Document);
                var found = root.Element.FindFirstBuildCache(TreeScope.TreeScope_Descendants, condition, request);
                if (found is null)
                {
                    return null;
                }

                var d = Make(found, root.Depth + 1);
                DocumentDraft ??= d;
                return d;
            }
            catch (COMException)
            {
                return null;
            }
        }

        public NodeDraft? FindByRuntimeId(NodeDraft root, string? runtimeId)
        {
            if (runtimeId is null)
            {
                return null;
            }

            var stack = new Stack<NodeDraft>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                if (d.RuntimeId == runtimeId)
                {
                    return d;
                }

                foreach (var c in d.Children)
                {
                    stack.Push(c);
                }
            }

            return null;
        }

        public UiNode Build(NodeDraft d, Dictionary<NodeDraft, UiNode> built, bool withChildren = true)
        {
            var children = new List<UiNode>(withChildren ? d.Children.Count : 0);
            if (withChildren)
            {
                foreach (var c in d.Children)
                {
                    children.Add(Build(c, built));
                }
            }

            int? heading = UiaClassifier.HeadingLevel(d.HeadingRaw, d.AriaRole, d.AriaProperties);
            var role = UiaClassifier.Role(d.ControlType, d.AriaRole, d.ClassName, d.IsDialog, heading);
            var node = new UiNode
            {
                Id = d.Id,
                Role = role,
                Name = d.Name,
                Value = d.IsPassword ? null : d.Value,
                AutomationId = d.AutomationId,
                ClassName = d.ClassName,
                HelpText = d.HelpText,
                IsEnabled = d.IsEnabled,
                IsFocused = d.HasFocus,
                IsOffscreen = d.IsOffscreen,
                IsPassword = d.IsPassword,
                IsEditable = UiaClassifier.IsEditable(d.ControlType, d.HasValue, d.ValueReadOnly, d.HasText, d.IsEnabled),
                IsSearchBox = UiaClassifier.IsSearchBox(role, d.AriaRole, d.Name, d.AutomationId, d.ClassName),
                IsAddressBar = UiaClassifier.IsAddressBar(role, d.Name, d.AutomationId, d.ClassName, inBrowser),
                HeadingLevel = heading,
                AutocompleteHint = UiaClassifier.AriaProperty(d.AriaProperties, "autocomplete"),
                RuntimeId = d.RuntimeId,
                Children = children,
            };

            built.TryAdd(d, node);
            return node;
        }

        private List<NodeDraft> ReadChildren(NodeDraft parent)
        {
            var result = new List<NodeDraft>();
            if (parent.ChildrenRead && parent.Children.Count > 0)
            {
                return result;
            }

            parent.ChildrenRead = true;
            try
            {
                var array = parent.Element.FindAllBuildCache(TreeScope.TreeScope_Children, uia.ControlViewCondition, request);
                if (array is null)
                {
                    return result;
                }

                int n = array.Length;
                for (int i = 0; i < n; i++)
                {
                    if (_count >= MaxNodes)
                    {
                        Truncated = true;
                        break;
                    }

                    var e = array.GetElement(i);
                    if (e is null)
                    {
                        continue;
                    }

                    var rid = CachedRuntimeId(e);
                    if (rid is not null && _byRuntimeId.TryGetValue(rid, out var existing) && existing.Depth > parent.Depth)
                    {
                        existing.Depth = parent.Depth + 1;
                        result.Add(existing);
                        continue;
                    }

                    result.Add(Make(e, parent.Depth + 1));
                }
            }
            catch (COMException)
            {
                Truncated = true;
            }

            return result;
        }

        private bool SameElement(IUIAutomationElement a, NodeDraft b)
        {
            try
            {
                var rid = CachedRuntimeId(a);
                if (rid is not null && b.RuntimeId is not null)
                {
                    return rid == b.RuntimeId;
                }

                return uia.CompareElements(a, b.Element) != 0;
            }
            catch (COMException)
            {
                return false;
            }
        }

        private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
