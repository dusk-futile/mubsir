using System.Collections.Concurrent;
using Interop.UIAutomationClient;

namespace Mubsir.App.Screen;

internal sealed class UiaThread : IDisposable
{
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
            return;
        }

        var worker = new Worker(this, $"{_name}-{_workers.Count + 1}");
        _workers.Add(worker);
        worker.Start();
    }

    private abstract class WorkItem
    {
        private int _state;

        public Worker? Worker { get; private set; }

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
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                _exited = true;
            }
        }
    }
}

internal sealed class UiaUnavailableException(string code, Exception? inner) : Exception("uia_unavailable:" + code, inner);
