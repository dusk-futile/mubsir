using System.Text;

namespace Basira.Core.Context;

/// <summary>
/// One UI Automation element as the agent sees it. Built by the Windows layer from a cached UIA walk.
/// <see cref="Id"/> is the number printed to the model and used by tools such as invoke(id).
/// </summary>
public sealed record UiNode
{
    public int Id { get; init; }

    /// <summary>
    /// Normalized role: window, pane, document, button, edit, link, heading, text, list, listitem, checkbox,
    /// radio, combobox, menu, menuitem, tab, tabitem, image, table, cell, dialog, toolbar, group or other.
    /// </summary>
    public string Role { get; init; } = "other";

    public string? Name { get; init; }

    /// <summary>Value pattern text. For password fields this is never shown to the model.</summary>
    public string? Value { get; init; }

    public string? AutomationId { get; init; }

    public string? ClassName { get; init; }

    public string? HelpText { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsFocused { get; init; }

    public bool IsOffscreen { get; init; }

    public bool IsPassword { get; init; }

    public bool IsEditable { get; init; }

    public bool IsSearchBox { get; init; }

    public bool IsAddressBar { get; init; }

    public int? HeadingLevel { get; init; }

    /// <summary>HTML autocomplete token when the browser exposes it, e.g. "cc-number" or "one-time-code".</summary>
    public string? AutocompleteHint { get; init; }

    /// <summary>Opaque UIA runtime id, used by the Windows layer to find the element again.</summary>
    public string? RuntimeId { get; init; }

    public IReadOnlyList<UiNode> Children { get; init; } = [];

    // Metadata only: names and values are content and must not end up in logs through ToString().
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Id = ").Append(Id)
            .Append(", Role = ").Append(Role)
            .Append(", Children = ").Append(Children.Count);
        if (IsPassword)
        {
            builder.Append(", IsPassword = True");
        }

        return true;
    }
}
