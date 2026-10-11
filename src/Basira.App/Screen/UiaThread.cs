using System.Collections.Concurrent;
using Interop.UIAutomationClient;

namespace Basira.App.Screen;

/// <summary>
/// Owns a CUIAutomation8 instance on a dedicated MTA thread and runs UIA work there, one item at a time, each awaited
/// with a timeout. UIA calls into a hung app can block for a long time despite the transaction timeout; when an item
/// overruns, that worker is written off and a fresh worker (with its own CUIAutomation8) takes over the queue, so one
/// frozen window never wedges Basira's reading for good.
/// </summary>
internal sealed class UiaThread : IDisposable
{
    /// <summary>Cross-process UIA calls give up after this long (IUIAutomation2.TransactionTimeout/ConnectionTimeout).</summary>
    public const uint UiaCallTimeoutMs = 2000;

    private const int MaxWorkers = 4;

    private readonly string _name;
    private readonly BlockingCollection<WorkItem> _queue = new(new ConcurrentQueue<WorkItem>());
    private readonly object _gate = new();
    private readonly List<Worker> _workers = [];
    private bool _disposed;

    public UiaThread(string name)
    {
        _name = name;
    }

    /// <summary>Runs <paramref name="work"/> on a UIA worker thread.</summary>
    /// <exception cref="TimeoutException">The work did not finish in time.</exception>
    /// <exception cref="UiaUnavailableException">UI Automation could not be created on this PC.</exception>
    public async Task<T> RunAsync<T>(Func<IUIAutomation, T> work, TimeSpan timeout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();
        var item = new WorkItem<T>(work, ct);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureWorkerLocked();
            _queue.Add(item);
        }

        try
        {
            return await item.Task.WaitAsync(timeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            item.Abandon();
            if (item.Worker is { } stuck)
            {
                // The call is still inside UIA: write this worker off and let a fresh one serve the queue.
                lock (_gate)
                {
                    stuck.Wedged = true;
                    EnsureWorkerLocked();
                }
            }

            throw;
        }
        catch (OperationCanceledException)
        {
            item.Abandon();
            throw;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.CompleteAdding();
        }
    }

    private void EnsureWorkerLocked()
    {
        _workers.RemoveAll(w => w.Exited);
        if (_workers.Any(w => !w.Wedged))
        {
            return;
        }

        if (_workers.Count >= MaxWorkers)
        {
            return; // every worker is stuck in a frozen app; items wait or time out until one returns
        }

        var worker = new Worker(this, $"{_name}-{_workers.Count + 1}");
        _workers.Add(worker);
        worker.Start();
    }

    private abstract class WorkItem
    {
        private int _state; // 0 queued, 1 running, 2 abandoned

        public Worker? Worker { get; private set; }

        /// <summary>Claims the item for a worker; false if the caller already gave up on it.</summary>
        public bool TryStart(Worker worker)
        {
            Worker = worker;
            return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        }

        public void Abandon() => Interlocked.Exchange(ref _state, 2);

        public abstract void Run(IUIAutomation uia);

        public abstract void Fail(Exception e);
    }

    private sealed class WorkItem<T>(Func<IUIAutomation, T> work, CancellationToken ct) : WorkItem
    {
        private readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _tcs.Task;

        public override void Run(IUIAutomation uia)
        {
            if (ct.IsCancellationRequested)
            {
                _tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                _tcs.TrySetResult(work(uia));
            }
            catch (Exception e)
            {
                _tcs.TrySetException(e);
            }
        }

        public override void Fail(Exception e) => _tcs.TrySetException(e);
    }

    private sealed class Worker(UiaThread owner, string name)
    {
        private volatile bool _wedged;
        private volatile bool _exited;

        public bool Wedged
        {
            get => _wedged;
            set => _wedged = value;
        }

        public bool Exited => _exited;

        public void Start()
        {
            var thread = new Thread(Loop) { IsBackground = true, Name = name };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        private void Loop()
        {
            IUIAutomation? uia = null;
            Exception? createError = null;
            try
            {
                uia = new CUIAutomation8();
                if (uia is IUIAutomation2 uia2)
                {
                    uia2.TransactionTimeout = UiaCallTimeoutMs;
                    uia2.ConnectionTimeout = UiaCallTimeoutMs;
                }
            }
            catch (Exception e)
            {
                try
                {
                    // Windows without UIAutomationCore v8 (should not happen on Windows 10): fall back to the v1 class.
                    uia = new CUIAutomation();
                }
                catch (Exception e2)
                {
                    createError = new UiaUnavailableException(e2.GetType().Name, e);
                }
            }

            try
            {
                foreach (var item in owner._queue.GetConsumingEnumerable())
                {
                    if (!item.TryStart(this))
                    {
                        continue;
                    }

                    if (uia is null)
                    {
                        item.Fail(createError ?? new UiaUnavailableException("none", null));
                        continue;
                    }

                    item.Run(uia);
                    if (_wedged)
                    {
                        break; // a replacement already serves the queue
                    }
                }
            }
            catch (Exception)
            {
                // Never let a UIA failure escape a background thread (it would end the process).
            }
            finally
            {
                _exited = true;
            }
        }
    }
}

/// <summary>UI Automation is not available (COM class missing or blocked).</summary>
internal sealed class UiaUnavailableException(string code, Exception? inner) : Exception("uia_unavailable:" + code, inner);
