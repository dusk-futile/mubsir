using System.Text;

namespace Basira.Core.Context;

/// <summary>Snapshot of the foreground window, captured by the Windows layer.</summary>
public sealed record ScreenContext
{
    public string? AppName { get; init; }

    /// <summary>Executable name of the foreground process, e.g. "chrome.exe".</summary>
    public string? ProcessName { get; init; }

    public string? WindowTitle { get; init; }

    /// <summary>Win32 window class, e.g. "#32770" for standard dialogs.</summary>
    public string? WindowClass { get; init; }

    public string? Url { get; init; }

    /// <summary>Host of <see cref="Url"/> when the window is a browser, e.g. "youtube.com".</summary>
    public string? Domain { get; init; }

    public bool IsBrowser { get; init; }

    public UiNode? Focused { get; init; }

    public UiNode? Root { get; init; }

    public string? VisibleText { get; init; }

    public string? Selection { get; init; }

    /// <summary>True when the window runs as administrator; Basira cannot control it.</summary>
    public bool IsElevated { get; init; }

    /// <summary>True when the UIA walk stopped early (element or time limit).</summary>
    public bool Truncated { get; init; }

    // Metadata only: titles, URLs and text are content and must not end up in logs through ToString().
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("ProcessName = ").Append(ProcessName)
            .Append(", IsBrowser = ").Append(IsBrowser)
            .Append(", IsElevated = ").Append(IsElevated)
            .Append(", Truncated = ").Append(Truncated);
        return true;
    }
}
