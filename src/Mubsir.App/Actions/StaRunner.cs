namespace Mubsir.App.Actions;

internal static class StaRunner
{
    public static T Run<T>(Func<T> work, TimeSpan timeout, T onTimeout)
    {
        T result = onTimeout;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception e)
            {
                error = e;
            }
        })
        {
            IsBackground = true,
            Name = "mubsir-shell",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(timeout))
        {
            return onTimeout;
        }

        if (error is not null)
        {
            throw new InvalidOperationException("shell_call_failed", error);
        }

        return result;
    }

    public static Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, T onTimeout, CancellationToken ct) =>
        Task.Run(() => Run(work, timeout, onTimeout), ct);
}
