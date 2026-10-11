namespace Basira.App.Abstractions;

/// <summary>Where Basira keeps its per-user data. Everything lives under %LOCALAPPDATA%\Basira.</summary>
public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Basira");

    public static string LogsDir => Path.Combine(DataDir, "logs");

    /// <summary>Plain JSON settings (no secrets) so a sighted helper can inspect them.</summary>
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");

    /// <summary>DPAPI-encrypted profile JSON.</summary>
    public static string ProfileFile => Path.Combine(DataDir, "profile.dat");

    /// <summary>DPAPI-encrypted remembered facts.</summary>
    public static string MemoryFile => Path.Combine(DataDir, "memory.dat");

    /// <summary>DPAPI-encrypted API keys and tokens.</summary>
    public static string SecretsFile => Path.Combine(DataDir, "secrets.dat");

    public static string CostFile => Path.Combine(DataDir, "cost.json");

    /// <summary>Last reading position per document.</summary>
    public static string PositionsFile => Path.Combine(DataDir, "positions.json");

    /// <summary>Journal of file moves, renames and recycles, for undo.</summary>
    public static string UndoJournalFile => Path.Combine(DataDir, "undo.jsonl");

    /// <summary>Read by the Basira Bridge NVDA add-on: {"until": unix-ms} quiets focus announcements.</summary>
    public static string NvdaQuietFile => Path.Combine(DataDir, "nvda-quiet.json");

    public static string ReportsDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
    }
}

/// <summary>Facts the user asked Basira to remember (encrypted at rest).</summary>
public interface IMemoryStore
{
    string ReadAll();

    void Append(string fact);

    void Clear();
}

/// <summary>Lets an agent tool ask the user a follow-up question and wait for the spoken or typed answer.</summary>
public interface IUserPrompter
{
    /// <returns>The user's answer, or null on timeout or cancel.</returns>
    Task<string?> AskAsync(string question, CancellationToken ct);
}
