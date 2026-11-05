using System.Security.Cryptography;
using System.Text;
using Mubsir.App.Abstractions;
using Mubsir.Core.Logging;

namespace Mubsir.App.Speech;

internal sealed class FishVoiceCache
{
    public const int MaxChars = 120;
    public const long MaxSizeBytes = 50 * 1024 * 1024; // 50 MB
    public const long TargetSizeBytes = 45 * 1024 * 1024; // Trim down to 45 MB when over 50 MB

    private readonly string _cacheDir;
    private readonly IEventLog _log;
    private readonly object _gate = new();

    private readonly long _maxBytes;
    private readonly long _targetBytes;

    public FishVoiceCache(IEventLog log, string? cacheDir = null, long maxBytes = MaxSizeBytes, long targetBytes = TargetSizeBytes, int limit = MaxChars)
    {
        _log = log;
        _maxBytes = maxBytes;
        _targetBytes = targetBytes;
        Limit = limit;
        _cacheDir = cacheDir ?? Path.Combine(AppPaths.DataDir, "voice-cache");
    }

    public string CacheDir => _cacheDir;

    /// <summary>Longer sentences than this aren't kept.</summary>
    public int Limit { get; }

    public bool TryGet(string text, string? voice, double speed, out byte[] audio)
    {
        audio = [];
        if (string.IsNullOrWhiteSpace(text) || text.Length > Limit)
        {
            return false;
        }

        string path = GetFilePath(text, voice, speed);
        lock (_gate)
        {
            try
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    if (bytes.Length > 0)
                    {
                        try
                        {
                            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
                        }
                        catch (Exception)
                        {
                            // Best-effort
                        }

                        audio = bytes;
                        return true;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Fail(LogKind.Speech, "cache_read_" + MetaLog.Code(e));
            }
        }

        return false;
    }

    public bool Has(string text, string? voice, double speed)
    {
        lock (_gate)
        {
            return File.Exists(GetFilePath(text, voice, speed));
        }
    }

    public void Put(string text, string? voice, double speed, byte[] audio)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > Limit || audio.Length == 0)
        {
            return;
        }

        string path = GetFilePath(text, voice, speed);
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temp, audio);
                File.Move(temp, path, overwrite: true);
                try
                {
                    File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
                }
                catch (Exception)
                {
                    // Best-effort
                }

                PruneOldestIfNeeded();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Fail(LogKind.Speech, "cache_write_" + MetaLog.Code(e));
            }
        }
    }

    public string GetFilePath(string text, string? voice, double speed)
    {
        string key = $"{voice ?? ""}|{speed:F2}|{text.Trim()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        string hex = Convert.ToHexStringLower(hash);
        return Path.Combine(_cacheDir, hex + ".mp3");
    }

    private void PruneOldestIfNeeded()
    {
        try
        {
            if (!Directory.Exists(_cacheDir))
            {
                return;
            }

            var dir = new DirectoryInfo(_cacheDir);
            FileInfo[] files = dir.GetFiles("*.mp3");
            long total = 0;
            foreach (FileInfo f in files)
            {
                total += f.Length;
            }

            if (total <= _maxBytes)
            {
                return;
            }

            Array.Sort(files, (a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc));
            foreach (FileInfo f in files)
            {
                if (total <= _targetBytes)
                {
                    break;
                }

                try
                {
                    long len = f.Length;
                    f.Delete();
                    total -= len;
                }
                catch (Exception)
                {
                    // Ignored
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Fail(LogKind.Speech, "cache_prune_" + MetaLog.Code(e));
        }
    }
}
