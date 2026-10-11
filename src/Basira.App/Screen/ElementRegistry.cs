using Basira.Core.Context;
using Interop.UIAutomationClient;

namespace Basira.App.Screen;

/// <summary>One screen snapshot: what the model saw and the live elements behind its ids.</summary>
internal sealed record ScreenSnapshot(
    long Generation,
    nint Hwnd,
    ScreenContext Screen,
    BuiltContext Built,
    IReadOnlyDictionary<int, IUIAutomationElement> Elements,
    long TakenAtMs);

/// <summary>
/// Printed node id -> live UIA element for the latest snapshot. Replaced as a whole on every snapshot, so an id from
/// an older screen never reaches a new element. Thread-safe; the elements themselves are only used on UIA workers.
/// </summary>
internal sealed class ElementRegistry
{
    private readonly object _gate = new();
    private ScreenSnapshot? _current;
    private long _generation;

    public ScreenSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public long NextGeneration() => Interlocked.Increment(ref _generation);

    public void Replace(ScreenSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_current is null || snapshot.Generation >= _current.Generation)
            {
                _current = snapshot;
            }
        }
    }

    /// <summary>The node and element the model meant by <paramref name="printedId"/>, from the latest snapshot.</summary>
    public bool TryResolve(int printedId, out UiNode? node, out IUIAutomationElement? element, out ScreenSnapshot? snapshot)
    {
        node = null;
        element = null;
        snapshot = Current;
        if (snapshot is null)
        {
            return false;
        }

        if (!snapshot.Built.IdMap.TryGetValue(printedId, out node))
        {
            // Printed ids equal node ids whenever they are unique, which the snapshotter guarantees.
            node = FindById(snapshot.Screen.Root, printedId) ?? (snapshot.Screen.Focused?.Id == printedId ? snapshot.Screen.Focused : null);
        }

        if (node is null)
        {
            return false;
        }

        return snapshot.Elements.TryGetValue(node.Id, out element);
    }

    /// <summary>The live element for a node of the latest snapshot.</summary>
    public IUIAutomationElement? ElementOf(UiNode? node)
    {
        var snapshot = Current;
        return node is not null && snapshot is not null && snapshot.Elements.TryGetValue(node.Id, out var e) ? e : null;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }

    private static UiNode? FindById(UiNode? root, int id)
    {
        if (root is null)
        {
            return null;
        }

        var stack = new Stack<UiNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.Id == id)
            {
                return n;
            }

            foreach (var c in n.Children)
            {
                stack.Push(c);
            }
        }

        return null;
    }
}
