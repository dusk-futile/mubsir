using System.Collections.Frozen;

namespace Basira.Core.Brain;

/// <summary>Exact agent tool names shared by the catalog, the policy and the Windows executor.</summary>
public static class ToolNames
{
    public const string ReadScreen = "read_screen";
    public const string Invoke = "invoke";
    public const string SetText = "set_text";
    public const string TypeText = "type_text";
    public const string PressKeys = "press_keys";
    public const string Scroll = "scroll";
    public const string Focus = "focus";
    public const string ListWindows = "list_windows";
    public const string SwitchTo = "switch_to";
    public const string OpenApp = "open_app";
    public const string OpenUrl = "open_url";
    public const string BrowseSearch = "browse_search";
    public const string DescribeScreen = "describe_screen";
    public const string OcrWindow = "ocr_window";
    public const string ReadDocument = "read_document";
    public const string Media = "media";
    public const string ListFolder = "list_folder";
    public const string FindFiles = "find_files";
    public const string MoveFile = "move_file";
    public const string CopyFile = "copy_file";
    public const string RenameFile = "rename_file";
    public const string Recycle = "recycle";
    public const string SystemStatus = "system_status";
    public const string SetVolume = "set_volume";
    public const string OpenSettings = "open_settings";
    public const string WindowsUpdateStatus = "windows_update_status";
    public const string RestartPc = "restart_pc";
    public const string Remember = "remember";
    public const string AskUser = "ask_user";

    public static IReadOnlySet<string> All { get; } = new[]
    {
        ReadScreen, Invoke, SetText, TypeText, PressKeys, Scroll, Focus, ListWindows, SwitchTo, OpenApp, OpenUrl,
        BrowseSearch, DescribeScreen, OcrWindow, ReadDocument, Media, ListFolder, FindFiles, MoveFile, CopyFile,
        RenameFile, Recycle, SystemStatus, SetVolume, OpenSettings, WindowsUpdateStatus, RestartPc, Remember, AskUser,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Tools that read or change files and are limited to the user's library folders.</summary>
    public static IReadOnlySet<string> FileTools { get; } = new[]
    {
        MoveFile, CopyFile, RenameFile, Recycle, ListFolder, FindFiles, ReadDocument,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool IsKnown(string? name) => name is not null && All.Contains(name);
}
