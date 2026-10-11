using System.Text.Json;
using Basira.Core.Agent;
using Basira.Core.Brain;
using Basira.Core.Router;

namespace Basira.App.Orchestration;

/// <summary>
/// Local commands that act on the PC become ordinary tool calls, so they pass through the same executor and
/// policy as the agent's actions (pure, testable). Commands Basira handles by itself map to null.
/// </summary>
internal static class LocalCommandMap
{
    /// <summary>The tool call for <paramref name="command"/>, or null when it is handled without a tool.</summary>
    /// <param name="argument">The app name for <see cref="LocalCommand.OpenApp"/>.</param>
    public static ToolCall? ToToolCall(LocalCommand command, string? argument, string id)
    {
        return command switch
        {
            LocalCommand.ScrollDown => Scroll(id, "down", "line"),
            LocalCommand.ScrollUp => Scroll(id, "up", "line"),
            LocalCommand.PageDown => Scroll(id, "down", "page"),
            LocalCommand.PageUp => Scroll(id, "up", "page"),
            LocalCommand.GoToTop => Scroll(id, "up", "top"),
            LocalCommand.GoToBottom => Scroll(id, "down", "bottom"),
            LocalCommand.GoBack => Keys(id, "alt+left"),
            LocalCommand.GoForward => Keys(id, "alt+right"),
            LocalCommand.NewTab => Keys(id, "ctrl+t"),
            LocalCommand.CloseTab => Keys(id, "ctrl+w"),
            LocalCommand.NewWindow => Keys(id, "ctrl+n"),
            LocalCommand.VolumeUp => Keys(id, "volumeup"),
            LocalCommand.VolumeDown => Keys(id, "volumedown"),
            LocalCommand.Mute => Call(id, ToolNames.SetVolume, w =>
            {
                w.WriteNull("level");
                w.WriteBoolean("mute", true);
            }),
            LocalCommand.SwitchWindow => Call(id, ToolNames.ListWindows, _ => { }),
            LocalCommand.BatteryStatus => Call(id, ToolNames.SystemStatus, w => w.WriteString("what", "battery")),
            LocalCommand.OpenApp when !string.IsNullOrWhiteSpace(argument) =>
                Call(id, ToolNames.OpenApp, w => w.WriteString("name", argument.Trim())),
            _ => null,
        };
    }

    /// <summary>Whether the command's tool result is worth speaking (lists and status), rather than letting NVDA announce the change.</summary>
    public static bool SpeaksResult(LocalCommand command) => command is LocalCommand.SwitchWindow or LocalCommand.BatteryStatus;

    /// <summary>Whether the step phrase is spoken before acting, so a misheard command can be stopped.</summary>
    public static bool NarratesFirst(LocalCommand command) => command is LocalCommand.OpenApp
        or LocalCommand.GoBack
        or LocalCommand.GoForward
        or LocalCommand.NewTab
        or LocalCommand.CloseTab
        or LocalCommand.NewWindow;

    private static ToolCall Scroll(string id, string direction, string amount) => Call(id, ToolNames.Scroll, w =>
    {
        w.WriteString("direction", direction);
        w.WriteString("amount", amount);
        w.WriteNull("id");
    });

    private static ToolCall Keys(string id, string chord) => Call(id, ToolNames.PressKeys, w => w.WriteString("keys", chord));

    private static ToolCall Call(string id, string tool, Action<Utf8JsonWriter> writeArgs)
    {
        using var buffer = new MemoryStream(128);
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            writeArgs(w);
            w.WriteEndObject();
        }

        using JsonDocument doc = JsonDocument.Parse(buffer.ToArray());
        return new ToolCall(id, tool, doc.RootElement.Clone());
    }
}
