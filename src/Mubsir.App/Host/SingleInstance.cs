namespace Mubsir.App.Host;

internal sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\Mubsir-6f1c2d8e-5b7a-4f3e-9c1d-2a4b8e7f0c55";

    private readonly Mutex _mutex;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
    }

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
        }

        _mutex.Dispose();
    }
}
