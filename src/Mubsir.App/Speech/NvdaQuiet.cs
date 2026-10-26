using System.Globalization;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;

namespace Mubsir.App.Speech;

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
