namespace Mubsir.App.Abstractions;

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

    public static string UndoJournalFile => Path.Combine(DataDir, "undo.jsonl");

    public static string NvdaQuietFile => Path.Combine(DataDir, "nvda-quiet.json");

    public static string ReportsDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
    }
}


