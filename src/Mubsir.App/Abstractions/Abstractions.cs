using Mubsir.Core.Agent;
using Mubsir.Core.Common;

namespace Mubsir.App.Abstractions;

public sealed record RecordedAudio(byte[] Wav, int DurationMs, int SpeechMs, double Peak);

public sealed record EarsHints(
    Lang PreferredLang,
    string? Dialect,
    string? AppName,
    string? FocusedName,
    IReadOnlyList<string> LocalCommandNames);

public sealed record EarsResult(string Transcript, string? LocalCommand, string? LocalArgument, UsageRecord? Usage);


public sealed class EarsException(BrainErrorKind kind, string code) : Exception(code)
{
    public BrainErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}

public enum ReadScope
{
    Focus,
    Window,
    Document,
}

public sealed record ForegroundInfo(
    nint Hwnd,
    int ProcessId,
    string? ProcessName,
    string? WindowTitle,
    string? WindowClass,
    bool IsElevated,
    bool IsBrowser);



public static class SecretNames
{
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
    public const string Fish = "fish";
}

public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mubsir");

    public static string LogsDir => Path.Combine(DataDir, "logs");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");

    public static string ProfileFile => Path.Combine(DataDir, "profile.dat");

    public static string MemoryFile => Path.Combine(DataDir, "memory.dat");

    public static string SecretsFile => Path.Combine(DataDir, "secrets.dat");

    public static string CostFile => Path.Combine(DataDir, "cost.json");

    public static string PositionsFile => Path.Combine(DataDir, "positions.json");

    public static string BookNotesFile => Path.Combine(DataDir, "book-notes.json");

    public static string LibraryFile => Path.Combine(DataDir, "library.json");

    public static string RemindersFile => Path.Combine(DataDir, "reminders.json");

    public static string PageRepairsFile => Path.Combine(DataDir, "page-repairs.json");

    public static string UndoJournalFile => Path.Combine(DataDir, "undo.jsonl");

    public static string NvdaQuietFile => Path.Combine(DataDir, "nvda-quiet.json");

    public static string VoiceCacheDir => Path.Combine(DataDir, "voice-cache");

    public static string DefaultChatsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mubsir", "Chats");

    public static string ReportsDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
    }
}
