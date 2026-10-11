namespace Basira.App.Actions;

/// <summary>
/// Runs a shell call on a fresh STA thread with a timeout. ShellExecute and SHFileOperation may hand work to shell
/// extensions that expect an apartment-threaded caller; a hung extension only costs that thread, never the caller.
/// </summary>
internal static class StaRunner
{
    /// <returns>The work's result, or <paramref name="onTimeout"/> when it did not finish in time.</returns>
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
            Name = "basira-shell",
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
