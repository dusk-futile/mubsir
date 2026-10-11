using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.Core.Common;
using Basira.Core.Logging;

namespace Basira.App.Storage;

/// <summary>
/// Facts the user asked Basira to remember: dated lines in DPAPI-encrypted <c>memory.dat</c>, capped at
/// <see cref="MemoryText.MaxChars"/> characters (oldest lines dropped). Off in shared-PC mode. Thread-safe.
/// </summary>
internal sealed class MemoryStore : IMemoryStore
{
    private readonly object _gate = new();
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly ISettingsProvider _settings;
    private readonly string _path;
    private string? _cache;

    public MemoryStore(IEventLog log, IClock clock, ISettingsProvider settings, string? path = null)
    {
        _log = log;
        _clock = clock;
        _settings = settings;
        _path = path ?? AppPaths.MemoryFile;
    }

    public string ReadAll()
    {
        if (_settings.Settings.SharedPcMode)
        {
            return string.Empty;
        }

        lock (_gate)
        {
            return _cache ??= Load();
        }
    }

    public void Append(string fact)
    {
        if (string.IsNullOrWhiteSpace(fact) || _settings.Settings.SharedPcMode)
        {
            return;
        }

        lock (_gate)
        {
            string current = _cache ?? Load();
            string line = MemoryText.FormatLine(_clock.UtcNow.ToLocalTime(), fact);
            string next = MemoryText.Append(current, line, MemoryText.MaxChars);
            try
            {
                Dpapi.WriteFile(_path, next);
                _cache = next;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException)
            {
                _log.Failure("memory_write", e);
                _cache = next; // still remembered for this session
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _cache = string.Empty;
            try
            {
                File.Delete(_path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Failure("memory_clear", e);
            }
        }
    }

    private string Load()
    {
        try
        {
            return Dpapi.ReadFile(_path) ?? string.Empty;
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException)
        {
            _log.Failure("memory_read", e);
            AtomicFile.QuarantineQuietly(_path);
            return string.Empty;
        }
    }
}

/// <summary>Pure text rules for the memory file (testable).</summary>
internal static class MemoryText
{
    public const int MaxChars = 4000;
    public const int MaxFactChars = 300;

    /// <summary>"2026-10-11: fact" on one line, whitespace collapsed, at most <see cref="MaxFactChars"/> characters of fact.</summary>
    public static string FormatLine(DateTimeOffset at, string fact)
    {
        var sb = new StringBuilder(fact.Length);
        bool space = false;
        foreach (char c in fact.Trim())
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = sb.Length > 0;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(c);
            if (sb.Length >= MaxFactChars)
            {
                break;
            }
        }

        return at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ": " + sb;
    }

    /// <summary>Appends a line, then drops the oldest lines until the text fits in <paramref name="maxChars"/>.</summary>
    public static string Append(string existing, string line, int maxChars)
    {
        var lines = (existing ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        lines.Add(line);

        int total = lines.Sum(l => l.Length) + Math.Max(0, lines.Count - 1);
        while (lines.Count > 1 && total > maxChars)
        {
            total -= lines[0].Length + 1;
            lines.RemoveAt(0);
        }

        string text = string.Join('\n', lines);
        return text.Length <= maxChars ? text : text[^maxChars..];
    }
}
