using System.Globalization;
using System.IO.Compression;
using System.Text;
using Basira.App.Abstractions;
using Basira.Core.Common;
using Basira.Core.Config;
using Basira.Core.Logging;

namespace Basira.App.Diagnostics;

/// <summary>
/// "Save a report for my family": a zip on the Desktop with the metadata-only logs, the settings without secrets
/// (and without the bank list), and a short system summary. No user name, PC name, paths or screen text.
/// </summary>
internal sealed class DiagnosticsReport
{
    private readonly IEventLog _log;
    private readonly ISettingsProvider _settings;
    private readonly IClock _clock;
    private readonly Func<IReadOnlyList<KeyValuePair<string, string>>> _systemFacts;

    /// <param name="systemFacts">Short facts such as "nvda_running: yes"; must never contain content or names.</param>
    public DiagnosticsReport(IEventLog log, ISettingsProvider settings, IClock clock, Func<IReadOnlyList<KeyValuePair<string, string>>> systemFacts)
    {
        _log = log;
        _settings = settings;
        _clock = clock;
        _systemFacts = systemFacts;
    }

    /// <summary>Writes the zip and returns its path, or null on failure (logged).</summary>
    public string? Create()
    {
        string name = "Basira-report-" + _clock.UtcNow.ToLocalTime().ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".zip";
        string path = Path.Combine(AppPaths.ReportsDir, name);
        string temp = path + ".part";
        try
        {
            Directory.CreateDirectory(AppPaths.ReportsDir);
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                AddLogs(zip);
                AddText(zip, "settings-redacted.json", RedactedSettings());
                AddText(zip, "system.txt", SystemSummary());
            }

            File.Move(temp, path, overwrite: true);
            _log.Event(LogKind.Startup, tool: "report_saved");
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log.Failure("report", e);
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            return null;
        }
    }

    private static void AddText(ZipArchive zip, string entryName, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private void AddLogs(ZipArchive zip)
    {
        if (!Directory.Exists(AppPaths.LogsDir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(AppPaths.LogsDir, "*.jsonl"))
        {
            try
            {
                ZipArchiveEntry entry = zip.CreateEntry("logs/" + Path.GetFileName(file), CompressionLevel.Optimal);
                using Stream target = entry.Open();

                // Today's file is open for appending; share it.
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                source.CopyTo(target);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Failure("report_log_copy", e);
            }
        }
    }

    private string RedactedSettings()
    {
        BasiraSettings s = _settings.Settings;
        int banks = s.BankDomains?.Count ?? 0;
        return SettingsSerializer.Serialize(s with { BankDomains = [$"({banks} bank and payment sites, not included)"] });
    }

    private string SystemSummary()
    {
        var sb = new StringBuilder();
        sb.Append("Basira diagnostics report (metadata only)\n");
        sb.Append("created_utc: ").Append(_clock.UtcNow.ToString("u", CultureInfo.InvariantCulture)).Append('\n');
        try
        {
            foreach (var (key, value) in _systemFacts())
            {
                sb.Append(key).Append(": ").Append(value).Append('\n');
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            sb.Append("facts_error: ").Append(e.GetType().Name).Append('\n');
        }

        return sb.ToString();
    }
}
