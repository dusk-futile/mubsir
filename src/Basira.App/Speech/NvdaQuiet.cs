using System.Globalization;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.App.Storage;
using Basira.Core.Common;
using Basira.Core.Logging;

namespace Basira.App.Speech;

/// <summary>
/// Asks the Basira Bridge NVDA add-on to quiet NVDA's focus announcements for a while, so they do not talk over
/// agent narration. Writes <see cref="AppPaths.NvdaQuietFile"/> as {"until": unix-ms}; clearing writes 0.
/// Writes are throttled so a busy agent does not hammer the disk. Thread-safe.
/// </summary>
internal sealed class NvdaQuiet
{
    private const long MinRewriteMs = 2000;

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly string _path;
    private long _writtenUntil;

    public NvdaQuiet(IClock clock, IEventLog log, string? path = null)
    {
        _clock = clock;
        _log = log;
        _path = path ?? AppPaths.NvdaQuietFile;
    }

    /// <summary>Quiet NVDA's focus chatter until now + <paramref name="duration"/> (extends, never shortens).</summary>
    public void QuietFor(TimeSpan duration)
    {
        long until = _clock.UtcNow.ToUnixTimeMilliseconds() + (long)duration.TotalMilliseconds;
        lock (_gate)
        {
            if (until <= _writtenUntil || until - _writtenUntil < MinRewriteMs && _writtenUntil > 0)
            {
                return;
            }

            Write(until);
        }
    }

    /// <summary>NVDA speaks normally again.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_writtenUntil == 0)
            {
                return;
            }

            Write(0);
        }
    }

    private void Write(long until)
    {
        try
        {
            AtomicFile.WriteAllText(_path, "{\"until\": " + until.ToString(CultureInfo.InvariantCulture) + "}");
            _writtenUntil = until;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.Failure("nvda_quiet_write", e);
        }
    }
}
