namespace Basira.App.Host;

/// <summary>One Basira per user session: a named mutex in the session's Local namespace.</summary>
internal sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\Basira-6f1c2d8e-5b7a-4f3e-9c1d-2a4b8e7f0c55";

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>Owns the mutex, or returns null when another instance holds it.</summary>
    public static SingleInstance? TryAcquire(string name = MutexName)
    {
        var mutex = new Mutex(initiallyOwned: false, name);
        try
        {
            if (mutex.WaitOne(TimeSpan.Zero))
            {
                return new SingleInstance(mutex);
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous instance died without releasing it; the mutex is now ours.
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread (released on another thread at shutdown); disposing still closes it.
        }

        _mutex.Dispose();
    }
}
