using System.Text;

namespace Mubsir.Core.Context;

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
