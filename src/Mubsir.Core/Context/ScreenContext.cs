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

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ProcessName = ").Append(ProcessName)
            .Append(", IsBrowser = ").Append(IsBrowser)
            .Append(", IsElevated = ").Append(IsElevated)
            .Append(", Truncated = ").Append(Truncated);
        return true;
    }
}
