using System.Text;

namespace Mubsir.Core.Context;

public sealed record ScreenContext
{
    public string? AppName { get; init; }

    public string? ProcessName { get; init; }

    public string? WindowTitle { get; init; }

    public string? WindowClass { get; init; }

    public string? Url { get; init; }

    public string? Domain { get; init; }

    public bool IsBrowser { get; init; }

    public UiNode? Focused { get; init; }

    public UiNode? Root { get; init; }

    public string? VisibleText { get; init; }

    public string? Selection { get; init; }

    public bool IsElevated { get; init; }

    public bool Truncated { get; init; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051", Justification = "The record's ToString calls it.")]
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ProcessName = ").Append(ProcessName)
            .Append(", IsBrowser = ").Append(IsBrowser)
            .Append(", IsElevated = ").Append(IsElevated)
            .Append(", Truncated = ").Append(Truncated);
        return true;
    }
}

public sealed record UiNode
{
    public int Id { get; init; }

    public string Role { get; init; } = "other";

    public string? Name { get; init; }

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

    public string? AutocompleteHint { get; init; }

    public string? RuntimeId { get; init; }

    public IReadOnlyList<UiNode> Children { get; init; } = [];

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0051", Justification = "The record's ToString calls it.")]
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
