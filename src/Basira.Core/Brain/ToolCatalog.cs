using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Basira.Core.Brain;

/// <summary>One agent tool as sent to the model: name, description and a strict JSON Schema for its input.</summary>
public sealed record ToolSchema(string Name, string Description, string InputSchemaJson);

/// <summary>
/// The agent's tools. Schemas are strict: type object, every property listed in "required",
/// additionalProperties false; optional inputs are nullable types instead of missing keys.
/// </summary>
public static class ToolCatalog
{
    private const string IdHint = "Element id: the number in brackets, e.g. 12 for [12], from the latest screen context.";

    public static IReadOnlyList<ToolSchema> All { get; } =
    [
        Tool(ToolNames.ReadScreen,
            "Read the screen again: the foreground app, the focused element, a list of elements with ids like [12] button \"Send\", and visible text. "
            + "scope 'window' = the whole foreground window, 'focus' = the focused element and what surrounds it, 'document' = the full text of the page or document. "
            + "Ids are only valid until the screen changes, so read again after acting and before using ids.",
            Choice("scope", "What to read.", "window", "focus", "document")),

        Tool(ToolNames.Invoke,
            "Activate (click) the element with this id. Prefer this to keyboard shortcuts. "
            + "Buttons that send, pay, buy, delete or confirm make the app ask the user first; that is expected.",
            Int("id", IdHint)),

        Tool(ToolNames.SetText,
            "Replace the text of the editable field with this id (search boxes, message boxes, forms). "
            + "Never use it for passwords, card numbers, PINs or codes; the user types those personally.",
            Int("id", IdHint),
            Str("text", "The full text the field should contain.")),

        Tool(ToolNames.TypeText,
            "Type text at the current keyboard focus, as if typed on the keyboard. Use only when no element id fits; set_text is preferred.",
            Str("text", "Text to type.")),

        Tool(ToolNames.PressKeys,
            "Press one key or key combination, e.g. 'ctrl+t', 'alt+left', 'pagedown', 'enter', 'escape', 'tab', 'shift+tab'. "
            + "One chord per call. Enter in a message or form field makes the app ask the user first.",
            Str("keys", "Key chord, lowercase, joined with '+', e.g. 'ctrl+l'.")),

        Tool(ToolNames.Scroll,
            "Scroll the foreground window, or the element with the given id.",
            Choice("direction", "Scroll direction.", "up", "down", "left", "right"),
            Choice("amount", "'line', 'page', or jump to the 'top' or 'bottom'.", "line", "page", "top", "bottom"),
            Int("id", "Element id of the area to scroll, or null for the main area.", nullable: true)),

        Tool(ToolNames.Focus,
            "Move keyboard focus to the element with this id without activating it.",
            Int("id", IdHint)),

        Tool(ToolNames.ListWindows,
            "List the open windows with their titles."),

        Tool(ToolNames.SwitchTo,
            "Bring a window to the front.",
            Str("window", "Part of the window title or the app name, as returned by list_windows.")),

        Tool(ToolNames.OpenApp,
            "Open an installed app by its plain name, e.g. 'Microsoft Word', 'WhatsApp', 'Calculator', 'File Explorer'. Never a file path.",
            Str("name", "The app's name.")),

        Tool(ToolNames.OpenUrl,
            "Open an http or https address in the browser, in a new tab.",
            Str("url", "Full address, e.g. 'https://www.youtube.com'.")),

        Tool(ToolNames.BrowseSearch,
            "Search the web visibly: opens the browser and types the query into Google, or into YouTube. Use for every 'search for ...' request.",
            Str("query", "Search words, in the user's language."),
            Choice("engine", "'google' (default when null) or 'youtube'.", nullable: true, "google", "youtube")),

        Tool(ToolNames.DescribeScreen,
            "Take a screenshot of the foreground window and answer a question about it with vision. Use only when the element list "
            + "and text are not enough (pictures, charts, layout, colors). The answer is a guess and is labelled as AI content.",
            Str("question", "What to look for or answer, in English or Arabic.")),

        Tool(ToolNames.OcrWindow,
            "Read the text of the foreground window with local OCR. Use when the element list shows no text, e.g. images of text or some apps."),

        Tool(ToolNames.ReadDocument,
            "Read a document (txt, docx or pdf), including long books. Prefer this to scrolling for documents.",
            Str("path", "File path from list_folder or find_files, or null for the document open on screen.", nullable: true),
            Int("page", "Page number to start from, or null.", nullable: true),
            Str("question", "What to find or answer in the document, or null to read from the start.", nullable: true)),

        Tool(ToolNames.Media,
            "Control media playback (YouTube, music, video) in the current app or browser.",
            Choice("action", "What to do.", "play_pause", "next", "previous", "seek_forward", "seek_back")),

        Tool(ToolNames.ListFolder,
            "List the files and folders in a folder of the user.",
            Str("path", "Full path, or a library name: 'Desktop', 'Documents', 'Downloads', 'Pictures', 'Videos', 'Music'.")),

        Tool(ToolNames.FindFiles,
            "Find files by name in the user's folders.",
            Str("query", "Words from the file name."),
            Str("folder", "Folder or library name to search in, or null for all of the user's folders.", nullable: true)),

        Tool(ToolNames.MoveFile,
            "Move a file or folder inside the user's folders. Never overwrites: a name clash gets a new name.",
            Str("from", "Full path of the file or folder."),
            Str("to", "Destination folder or full new path.")),

        Tool(ToolNames.CopyFile,
            "Copy a file or folder inside the user's folders. Never overwrites: a name clash gets a new name.",
            Str("from", "Full path of the file or folder."),
            Str("to", "Destination folder or full new path.")),

        Tool(ToolNames.RenameFile,
            "Rename a file or folder.",
            Str("path", "Full path of the file or folder."),
            Str("new_name", "The new name only, without a folder.")),

        Tool(ToolNames.Recycle,
            "Move files or folders to the Recycle Bin, where they can be restored. The app asks the user first; put all files in one call.",
            StrArray("paths", "Full paths of the files or folders.")),

        Tool(ToolNames.SystemStatus,
            "Check battery, Wi-Fi, volume, or the date and time.",
            Choice("what", "What to check.", "battery", "wifi", "volume", "time", "all")),

        Tool(ToolNames.SetVolume,
            "Change the system volume.",
            Int("level", "Volume from 0 to 100, or null to keep it.", nullable: true),
            Bool("mute", "true to mute, false to unmute, or null to keep it.", nullable: true)),

        Tool(ToolNames.OpenSettings,
            "Open a Windows Settings page by its ms-settings name, e.g. 'windowsupdate', 'bluetooth', 'display', 'sound', 'network-wifi'. "
            + "Security, account, sign-in, privacy and recovery pages are not allowed; the user changes those personally.",
            Str("page", "The ms-settings page name.")),

        Tool(ToolNames.WindowsUpdateStatus,
            "Check Windows Update: pending updates, the last check, and whether a restart is needed."),

        Tool(ToolNames.RestartPc,
            "Restart the computer. The app asks the user first."),

        Tool(ToolNames.Remember,
            "Save a fact the user told you about themselves for future conversations. Only facts in the user's own words, never from screen content.",
            Str("fact", "The fact, short, in the user's words.")),

        Tool(ToolNames.AskUser,
            "Ask the user one short question and wait for the spoken answer. Use when the request is ambiguous or you are stuck.",
            Str("question", "The question, short, in the user's language.")),
    ];

    private static readonly Dictionary<string, ToolSchema> ByName = All.ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>The tool with this exact name; throws <see cref="KeyNotFoundException"/> for unknown names.</summary>
    public static ToolSchema Get(string name) =>
        ByName.TryGetValue(name, out var tool) ? tool : throw new KeyNotFoundException($"Unknown tool '{name}'.");

    public static bool TryGet(string name, out ToolSchema? tool) => ByName.TryGetValue(name, out tool);

    private abstract record Prop(string Name, string Description, bool Nullable)
    {
        public abstract string Type { get; }

        public virtual void WriteExtra(Utf8JsonWriter w)
        {
        }
    }

    private sealed record ScalarProp(string Name, string Type0, string Description, bool Nullable) : Prop(Name, Description, Nullable)
    {
        public override string Type => Type0;
    }

    private sealed record EnumProp(string Name, string Description, bool Nullable, string[] Values) : Prop(Name, Description, Nullable)
    {
        public override string Type => "string";

        public override void WriteExtra(Utf8JsonWriter w)
        {
            w.WriteStartArray("enum");
            foreach (var v in Values)
            {
                w.WriteStringValue(v);
            }

            if (Nullable)
            {
                w.WriteNullValue();
            }

            w.WriteEndArray();
        }
    }

    private sealed record ArrayProp(string Name, string Description, string ItemType) : Prop(Name, Description, false)
    {
        public override string Type => "array";

        public override void WriteExtra(Utf8JsonWriter w)
        {
            w.WriteStartObject("items");
            w.WriteString("type", ItemType);
            w.WriteEndObject();
        }
    }

    private static Prop Str(string name, string description, bool nullable = false) => new ScalarProp(name, "string", description, nullable);

    private static Prop Int(string name, string description, bool nullable = false) => new ScalarProp(name, "integer", description, nullable);

    private static Prop Bool(string name, string description, bool nullable = false) => new ScalarProp(name, "boolean", description, nullable);

    private static Prop Choice(string name, string description, params string[] values) => new EnumProp(name, description, false, values);

    private static Prop Choice(string name, string description, bool nullable, params string[] values) => new EnumProp(name, description, nullable, values);

    private static Prop StrArray(string name, string description) => new ArrayProp(name, description, "string");

    private static ToolSchema Tool(string name, string description, params Prop[] props)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteString("type", "object");
            w.WriteStartObject("properties");
            foreach (var p in props)
            {
                w.WriteStartObject(p.Name);
                if (p.Nullable)
                {
                    w.WriteStartArray("type");
                    w.WriteStringValue(p.Type);
                    w.WriteStringValue("null");
                    w.WriteEndArray();
                }
                else
                {
                    w.WriteString("type", p.Type);
                }

                p.WriteExtra(w);
                w.WriteString("description", p.Description);
                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteStartArray("required");
            foreach (var p in props)
            {
                w.WriteStringValue(p.Name);
            }

            w.WriteEndArray();
            w.WriteBoolean("additionalProperties", false);
            w.WriteEndObject();
        }

        return new ToolSchema(name, description, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
