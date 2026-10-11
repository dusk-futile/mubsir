using System.Runtime.InteropServices;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.App.Native;
using Basira.Core.Logging;

namespace Basira.App.Audio;

/// <summary>
/// Push-to-talk capture through winmm waveIn: WAVE_MAPPER, 16 kHz mono PCM16, six 40 ms buffers, CALLBACK_EVENT with
/// a worker thread. <see cref="Start"/> opens and starts at once (talk-key down), so the first word is kept.
/// A missing or blocked microphone gives an empty clip with peak 0, never an exception.
/// </summary>
internal sealed class WaveInRecorder : IAudioRecorder
{
    public const int SampleRate = 16000;
    public const int MaxSeconds = 120;

    private readonly object _gate = new();
    private readonly IEventLog _log;
    private Session? _session;

    public WaveInRecorder(IEventLog log)
    {
        _log = log;
    }

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _session is not null;
            }
        }
    }

    public void Start()
    {
        Session? previous;
        lock (_gate)
        {
            previous = _session;
            _session = null;
        }

        previous?.Finish(); // a lost key-up: drop the old clip

        var session = Session.Open(_log);
        lock (_gate)
        {
            _session = session;
        }
    }

    public RecordedAudio Stop()
    {
        Session? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }

        byte[] pcm = session?.Finish() ?? [];
        AudioLevels levels = AudioMath.Analyze(pcm, SampleRate);
        return new RecordedAudio(AudioMath.ToWav(pcm, SampleRate), AudioMath.DurationMs(pcm.Length, SampleRate), levels.SpeechMs, levels.Peak);
    }

    public void Cancel()
    {
        Session? session;
        lock (_gate)
        {
            session = _session;
            _session = null;
        }

        session?.Finish();
    }

    public void Dispose() => Cancel();

    /// <summary>One open device and its buffers, from talk-key down to release.</summary>
    private sealed unsafe class Session
    {
        private const int BufferCount = 6;
        private const int BufferMs = 40;
        private const int BytesPerBuffer = SampleRate * 2 * BufferMs / 1000; // 1280
        private const int MaxBytes = SampleRate * 2 * MaxSeconds;

        private readonly IEventLog _log;
        private readonly AutoResetEvent _event = new(false);
        private readonly MemoryStream _pcm = new(64 * 1024);
        private nint _hwi;
        private WAVEHDR* _headers;
        private byte* _data;
        private int _next;
        private Thread? _worker;
        private volatile bool _stopping;
        private bool _finished;

        private Session(IEventLog log)
        {
            _log = log;
        }

        public static Session Open(IEventLog log)
        {
            var s = new Session(log);
            try
            {
                s.OpenDevice();
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.Failure("wavein_open", e);
                s.ReleaseDevice();
            }

            return s;
        }

        /// <summary>Stops capture and returns the PCM recorded so far. Safe to call once; later calls return empty.</summary>
        public byte[] Finish()
        {
            if (_finished)
            {
                return [];
            }

            _finished = true;
            _stopping = true;
            _event.Set();
            if (_worker is not null && !_worker.Join(1500))
            {
                // The driver is stuck; leak the native buffers rather than free memory it may still write to.
                _log.Error("wavein_worker_stuck", LogKind.Talk, LogOutcome.TimedOut);
                return _pcm.ToArray();
            }

            if (_hwi != 0)
            {
                Check(WinMm.waveInStop(_hwi), "wavein_stop");
                Check(WinMm.waveInReset(_hwi), "wavein_reset"); // returns every queued buffer as done
                Drain(requeue: false);
            }

            ReleaseDevice();
            _event.Dispose();
            return _pcm.ToArray();
        }

        private void OpenDevice()
        {
            if (WinMm.waveInGetNumDevs() == 0)
            {
                _log.Error("mic_none", LogKind.Talk);
                return;
            }

            var format = new WAVEFORMATEX
            {
                WFormatTag = Win32.WAVE_FORMAT_PCM,
                NChannels = 1,
                NSamplesPerSec = SampleRate,
                NAvgBytesPerSec = SampleRate * 2,
                NBlockAlign = 2,
                WBitsPerSample = 16,
                CbSize = 0,
            };

            uint rc = WinMm.waveInOpen(out _hwi, Win32.WAVE_MAPPER, in format, _event.SafeWaitHandle.DangerousGetHandle(), 0, Win32.CALLBACK_EVENT);
            if (rc != Win32.MMSYSERR_NOERROR)
            {
                // 4 = MMSYSERR_ALLOCATED (in use), 6 = MMSYSERR_NODRIVER, 32 = WAVERR_BADFORMAT; privacy blocks show up here too.
                _log.Error("wavein_open_" + rc, LogKind.Talk);
                _hwi = 0;
                return;
            }

            _headers = (WAVEHDR*)NativeMemory.AllocZeroed((nuint)(BufferCount * sizeof(WAVEHDR)));
            _data = (byte*)NativeMemory.AllocZeroed((nuint)(BufferCount * BytesPerBuffer));
            for (int i = 0; i < BufferCount; i++)
            {
                WAVEHDR* h = &_headers[i];
                h->LpData = (nint)(_data + (i * BytesPerBuffer));
                h->DwBufferLength = BytesPerBuffer;
                if (!Check(WinMm.waveInPrepareHeader(_hwi, h, (uint)sizeof(WAVEHDR)), "wavein_prepare")
                    || !Check(WinMm.waveInAddBuffer(_hwi, h, (uint)sizeof(WAVEHDR)), "wavein_add"))
                {
                    ReleaseDevice();
                    return;
                }
            }

            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Basira mic", Priority = ThreadPriority.AboveNormal };
            _worker.Start();
            if (!Check(WinMm.waveInStart(_hwi), "wavein_start"))
            {
                _stopping = true;
                _event.Set();
                _worker.Join(1500);
                _worker = null;
                ReleaseDevice();
            }
        }

        private void WorkerLoop()
        {
            try
            {
                while (!_stopping)
                {
                    _event.WaitOne(200);
                    if (_stopping)
                    {
                        break;
                    }

                    Drain(requeue: true);
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("wavein_worker", e);
            }
        }

        /// <summary>Copies finished buffers in order; re-queues them while recording.</summary>
        private void Drain(bool requeue)
        {
            if (_headers is null)
            {
                return;
            }

            for (int n = 0; n < BufferCount; n++)
            {
                WAVEHDR* h = &_headers[_next];
                uint flags = Volatile.Read(ref h->DwFlags);
                if ((flags & Win32.WHDR_DONE) == 0)
                {
                    return;
                }

                int recorded = (int)Math.Min(h->DwBytesRecorded, (uint)BytesPerBuffer);
                if (recorded > 0 && _pcm.Length + recorded <= MaxBytes)
                {
                    _pcm.Write(new ReadOnlySpan<byte>((void*)h->LpData, recorded));
                }

                h->DwBytesRecorded = 0;
                Volatile.Write(ref h->DwFlags, flags & ~Win32.WHDR_DONE);
                if (requeue && !_stopping)
                {
                    Check(WinMm.waveInAddBuffer(_hwi, h, (uint)sizeof(WAVEHDR)), "wavein_requeue");
                }

                _next = (_next + 1) % BufferCount;
            }
        }

        private void ReleaseDevice()
        {
            if (_hwi != 0)
            {
                if (_headers is not null)
                {
                    WinMm.waveInReset(_hwi);
                    for (int i = 0; i < BufferCount; i++)
                    {
                        WinMm.waveInUnprepareHeader(_hwi, &_headers[i], (uint)sizeof(WAVEHDR));
                    }
                }

                Check(WinMm.waveInClose(_hwi), "wavein_close");
                _hwi = 0;
            }

            if (_headers is not null)
            {
                NativeMemory.Free(_headers);
                _headers = null;
            }

            if (_data is not null)
            {
                NativeMemory.Free(_data);
                _data = null;
            }
        }

        private bool Check(uint rc, string what)
        {
            if (rc == Win32.MMSYSERR_NOERROR)
            {
                return true;
            }

            _log.Error(what + "_" + rc, LogKind.Talk);
            return false;
        }
    }
}
