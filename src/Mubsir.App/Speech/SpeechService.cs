using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Mubsir.App.Native;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Speech;

internal sealed class SpeechService : IDisposable
{
    private const uint ErrorUnknownInterface = NvdaController.ErrorUnknownInterface;
    private const int NvdaRecheckMs = 5000;
    private const int NvdaHungBackoffMs = 60_000;

    private readonly object _gate = new();
    private readonly LinkedList<Job> _queue = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly NvdaController _nvda;
    private readonly OneCoreSpeech _oneCore;
    private readonly SettingsProvider _settings;
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<Earcon, nint> _earcons = new();
    private readonly Timer _watchdog;

    private long _generation;
    private Job? _current;
    private bool _busy;
    private int _workerId;
    private bool _disposed;
    private int _rate;

    private volatile bool _suspended;
    private volatile bool _nvdaRunning;
    private long _nvdaCheckedAt = long.MinValue;
    private long _nvdaUnhealthyUntil;
    private volatile bool _ssmlUnsupported;

    private volatile bool _inNvdaCall;
    private long _nvdaCallStartedAt;
    private long _nvdaCallBudgetMs;

    public SpeechService(NvdaController nvda, OneCoreSpeech oneCore, SettingsProvider settings, IEventLog log, IClock clock)
    {
        _nvda = nvda;
        _oneCore = oneCore;
        _settings = settings;
        _log = log;
        _clock = clock;
        _rate = settings.Settings.SpeechRate;
        _oneCore.Rate = _rate;
        NvdaController.SetMarkTarget(History);
        _watchdog = new Timer(_ => CheckHungNvdaCall(), null, 1000, 1000);
        StartWorker();
    }

    public event Action<bool>? SpeakingChanged;

    public SpeechHistory History { get; } = new();

    public bool IsSpeaking
    {
        get
        {
            lock (_gate)
            {
                return _busy || _queue.Count > 0;
            }
        }
    }

    public int Rate
    {
        get => Volatile.Read(ref _rate);
        set
        {
            int r = Math.Clamp(value, 0, 100);
            Volatile.Write(ref _rate, r);
            _oneCore.Rate = r;
        }
    }

    public bool Suspended => _suspended;

    public bool NvdaActive => UseNvdaNow();

    public void Speak(string text, UtteranceKind kind, Lang lang, bool interrupt = false, VerbalizeMode mode = VerbalizeMode.Normal) =>
        Enqueue(text, kind, lang, interrupt, mode, done: null);

    public Task SpeakAsync(string text, UtteranceKind kind, Lang lang, CancellationToken ct, bool interrupt = false)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(text, kind, lang, interrupt, VerbalizeMode.Normal, done);
        return WaitAsync(done, ct);
    }

    public void SpeakItems(IReadOnlyList<SpeechItem> items, Lang lang, bool interrupt = true) =>
        EnqueueItems(items, lang, interrupt, done: null);

    public Task SpeakItemsAsync(IReadOnlyList<SpeechItem> items, Lang lang, CancellationToken ct, bool interrupt = true)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EnqueueItems(items, lang, interrupt, done);
        return WaitAsync(done, ct);
    }

    public IReadOnlyList<int> Enqueue(string text, UtteranceKind kind, Lang lang, bool interrupt, VerbalizeMode mode, TaskCompletionSource? done)
    {
        if (string.IsNullOrWhiteSpace(text) || _suspended || _disposed)
        {
            done?.TrySetResult();
            return [];
        }

        IReadOnlyList<ComposedSentence> sentences;
        try
        {
            sentences = SpeechComposer.Compose(text, lang, History, kind, new SpeechOptions(Mode: mode), TagStyle());
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("speech_compose", e);
            done?.TrySetResult();
            return [];
        }

        AddJobs(sentences, lang, interrupt, done);
        return sentences.Select(s => s.HistoryIndex).ToArray();
    }

    public void Stop()
    {
        ClearQueue();
        CancelOutputs();
    }

    public void RequestStopFast() => Interlocked.Increment(ref _generation);

    public void PlayEarcon(Earcon earcon)
    {
        if (_suspended || _disposed)
        {
            return;
        }

        try
        {
            nint wav = _earcons.GetOrAdd(earcon, static e =>
            {
                byte[] bytes = EarconTones.Wav(e);
                return GCHandle.Alloc(bytes, GCHandleType.Pinned).AddrOfPinnedObject();
            });
            if (!WinMm.PlaySoundW(wav, 0, Win32.SND_MEMORY | Win32.SND_ASYNC | Win32.SND_NODEFAULT))
            {
                _log.Error("earcon_play_failed", LogKind.Speech);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("earcon", e);
        }
    }

    public void SetSuspended(bool suspended)
    {
        if (suspended)
        {
            Stop();
            _suspended = true;
        }
        else
        {
            _suspended = false;
            _nvdaCheckedAt = long.MinValue;
        }
    }

    public void SetNvdaRunning(bool running)
    {
        _nvdaRunning = running;
        Interlocked.Exchange(ref _nvdaCheckedAt, _clock.NowMs);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _watchdog.Dispose();
        Stop();
        _signal.Set();
    }

    private static async Task WaitAsync(TaskCompletionSource done, CancellationToken ct)
    {
        if (!ct.CanBeCanceled)
        {
            await done.Task.ConfigureAwait(false);
            return;
        }

        using (ct.Register(static s => ((TaskCompletionSource)s!).TrySetResult(), done))
        {
            await done.Task.ConfigureAwait(false);
        }
    }

    private void EnqueueItems(IReadOnlyList<SpeechItem> items, Lang lang, bool interrupt, TaskCompletionSource? done)
    {
        if (items.Count == 0 || _suspended || _disposed)
        {
            if (interrupt)
            {
                Stop();
            }

            done?.TrySetResult();
            return;
        }

        var sentences = new List<ComposedSentence>(items.Count);
        LangTagStyle style = TagStyle();
        foreach (SpeechItem item in items)
        {
            try
            {
                IReadOnlyList<TextRun> runs = SpeechTextPipeline.Prepare(item.Text, lang);
                string ssml = SsmlBuilder.Build(runs, SpeechComposer.MarkName(item.Index), style);
                sentences.Add(new ComposedSentence(item.Index, ssml, string.Concat(runs.Select(r => r.Text)), runs));
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("speech_prepare", e);
            }
        }

        AddJobs(sentences, lang, interrupt, done);
    }

    private void AddJobs(IReadOnlyList<ComposedSentence> sentences, Lang lang, bool interrupt, TaskCompletionSource? done)
    {
        if (interrupt)
        {
            Stop();
        }

        if (sentences.Count == 0)
        {
            done?.TrySetResult();
            return;
        }

        lock (_gate)
        {
            long generation = Interlocked.Read(ref _generation);
            for (int i = 0; i < sentences.Count; i++)
            {
                _queue.AddLast(new Job(sentences[i], lang, generation, i == sentences.Count - 1 ? done : null));
            }
        }

        _signal.Set();
    }

    private void ClearQueue()
    {
        var waiters = new List<TaskCompletionSource>();
        lock (_gate)
        {
            Interlocked.Increment(ref _generation);
            foreach (Job job in _queue)
            {
                if (job.Done is not null)
                {
                    waiters.Add(job.Done);
                }
            }

            _queue.Clear();
            if (_current?.Done is { } currentDone)
            {
                waiters.Add(currentDone);
            }
        }

        foreach (TaskCompletionSource w in waiters)
        {
            w.TrySetResult();
        }
    }

    private void CancelOutputs()
    {
        if (!_suspended && _settings.Settings.UseNvda && _nvda.DllAvailable && _nvdaRunning)
        {
            uint rc = _nvda.CancelSpeech();
            if (rc != 0)
            {
                _log.Error("nvda_cancel_" + rc, LogKind.Speech);
            }
        }

        _oneCore.Stop();
    }

    private LangTagStyle TagStyle() =>
        Enum.TryParse(_settings.Settings.NvdaLangTagStyle, ignoreCase: true, out LangTagStyle s) ? s : LangTagStyle.VoiceElement;

    private void StartWorker()
    {
        int id;
        lock (_gate)
        {
            id = ++_workerId;
        }

        var thread = new Thread(() => WorkerLoop(id))
        {
            IsBackground = true,
            Name = "Mubsir speech " + id,
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    private void WorkerLoop(int myId)
    {
        while (true)
        {
            Job? job = null;
            bool? changed = null;
            lock (_gate)
            {
                if (_disposed || myId != _workerId)
                {
                    return;
                }

                long generation = Interlocked.Read(ref _generation);
                while (_queue.First is { } first)
                {
                    _queue.RemoveFirst();
                    if (first.Value.Generation == generation)
                    {
                        job = first.Value;
                        break;
                    }

                    first.Value.Done?.TrySetResult();
                }

                _current = job;
                bool busy = job is not null;
                if (busy != _busy)
                {
                    _busy = busy;
                    changed = busy;
                }
            }

            if (changed is bool c)
            {
                RaiseSpeakingChanged(c);
            }

            if (job is null)
            {
                _signal.WaitOne(1000);
                continue;
            }

            try
            {
                SpeakOne(job, myId);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("speech_worker", e);
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_current, job))
                    {
                        _current = null;
                    }
                }

                job.Done?.TrySetResult();
            }
        }
    }

    private void SpeakOne(Job job, int myId)
    {
        if (_suspended || job.Generation != Interlocked.Read(ref _generation))
        {
            return;
        }

        if (UseNvdaNow())
        {
            uint rc = SpeakThroughNvda(job);
            if (myId != _workerId)
            {
                return;
            }

            if (rc == 0)
            {
                return;
            }

            _log.Error("nvda_speak_" + rc, LogKind.Speech);
            _nvdaRunning = false;
            Interlocked.Exchange(ref _nvdaCheckedAt, _clock.NowMs);
            if (job.Generation != Interlocked.Read(ref _generation) || _suspended)
            {
                return;
            }
        }

        History.MarkSpoken(job.Sentence.HistoryIndex);
        TimeSpan limit = TimeSpan.FromSeconds(30) + TimeSpan.FromMilliseconds(job.Sentence.PlainText.Length * 250L);
        try
        {
            if (!_oneCore.SpeakAsync(job.Sentence.Runs, CancellationToken.None).Wait(limit))
            {
                _log.Error("onecore_sentence_timeout", LogKind.Speech, LogOutcome.TimedOut);
                _oneCore.Stop();
            }
        }
        catch (AggregateException e)
        {
            _log.Failure("onecore_sentence", e.InnerException ?? e);
        }
    }

    private uint SpeakThroughNvda(Job job)
    {
        _nvdaCallStartedAt = _clock.NowMs;
        _nvdaCallBudgetMs = 15_000 + (job.Sentence.PlainText.Length * 150L);
        _inNvdaCall = true;
        try
        {
            if (_ssmlUnsupported)
            {
                return SpeakPlainThroughNvda(job);
            }

            uint rc = _nvda.SpeakSsml(job.Sentence.Ssml, symbolLevel: -1, priority: 0, asynchronous: false);
            if (rc == ErrorUnknownInterface)
            {
                _ssmlUnsupported = true;
                _log.Error("nvda_ssml_unsupported", LogKind.Speech);
                rc = SpeakPlainThroughNvda(job);
            }

            return rc;
        }
        finally
        {
            _inNvdaCall = false;
        }
    }

    private uint SpeakPlainThroughNvda(Job job)
    {
        History.MarkSpoken(job.Sentence.HistoryIndex);
        return _nvda.SpeakText(job.Sentence.PlainText);
    }

    private bool UseNvdaNow()
    {
        if (_suspended || !_settings.Settings.UseNvda || !_nvda.DllAvailable)
        {
            return false;
        }

        long now = _clock.NowMs;
        if (now < Interlocked.Read(ref _nvdaUnhealthyUntil))
        {
            return false;
        }

        long checkedAt = Interlocked.Read(ref _nvdaCheckedAt);
        if (checkedAt == long.MinValue || now - checkedAt > NvdaRecheckMs)
        {
            _nvdaRunning = _nvda.IsRunning();
            Interlocked.Exchange(ref _nvdaCheckedAt, now);
        }

        return _nvdaRunning;
    }

    private void CheckHungNvdaCall()
    {
        try
        {
            if (!_inNvdaCall || _disposed)
            {
                return;
            }

            long now = _clock.NowMs;
            if (now - _nvdaCallStartedAt <= _nvdaCallBudgetMs)
            {
                return;
            }

            _inNvdaCall = false;
            Interlocked.Exchange(ref _nvdaUnhealthyUntil, now + NvdaHungBackoffMs);
            _log.Error("nvda_call_hung", LogKind.Speech, LogOutcome.TimedOut);
            StartWorker();
            _signal.Set();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("speech_watchdog", e);
        }
    }

    private void RaiseSpeakingChanged(bool speaking)
    {
        try
        {
            SpeakingChanged?.Invoke(speaking);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("speaking_changed_handler", e);
        }
    }

    private sealed record Job(ComposedSentence Sentence, Lang Lang, long Generation, TaskCompletionSource? Done);
}
