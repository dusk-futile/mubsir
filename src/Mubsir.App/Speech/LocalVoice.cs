using System.Diagnostics;
using System.Security.Cryptography;
using Mubsir.App.Abstractions;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Mubsir.Core.Speech;
using SherpaOnnx;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Mubsir.App.Speech;

/// <summary>
/// The free English voice, Ryan from Piper, which runs on this PC. It's downloaded once in the background, and the
/// Windows voice speaks until it's ready. It's only held in memory while it's being used, and if this PC turns out to
/// be too slow for it, the Windows voice takes over.
/// </summary>
internal sealed class LocalVoice(IEventLog log, string? folder = null, HttpClient? http = null) : ISentenceVoice, IDisposable
{
    public const string ModelName = "en_US-ryan-high";
    private const string Download = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/vits-piper-en_US-ryan-high.tar.bz2";
    private const string DownloadSha256 = "6a71edf4d308b9cb2eaeadc8d1f3c6bf96120ecb7fe52c29a2b6e139c59760ed";
    private const double NormalSpeed = 0.9;
    private const int Threads = 2;
    private const int TooSlowInARow = 3;
    private const int WorthTiming = 40;
    private static readonly TimeSpan KeepLoaded = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan DownloadLimit = TimeSpan.FromMinutes(30);

    private readonly string _root = folder ?? Path.Combine(AppPaths.DataDir, "voices");
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    private readonly object _gate = new();
    private readonly object _engineGate = new();
    private OfflineTts? _engine;
    private Timer? _unload;
    private long _lastUsed;
    private CancellationTokenSource _stop = new();
    private MediaPlayer? _player;
    private TaskCompletionSource<bool>? _playing;
    private (string Text, int Rate, Task<byte[]?> Audio)? _ready;
    private int _slowInARow;
    private volatile bool _tooSlow;
    private volatile bool _installed;
    private bool _disposed;

    /// <summary>Fires once when the voice has been downloaded this run.</summary>
    public event Action? Installed;

    /// <summary>Fires once if this PC can't make speech as fast as it plays.</summary>
    public event Action? TooSlow;

    private string ModelFolder => Path.Combine(_root, "vits-piper-" + ModelName);

    private string ModelFile => Path.Combine(ModelFolder, ModelName + ".onnx");

    public bool IsInstalled => _installed || File.Exists(ModelFile);

    public bool CanSpeak(bool isBook, Lang lang) => lang == Lang.English && !_tooSlow && IsInstalled;

    /// <summary>Downloads the voice if it isn't here. It returns false on any failure, and it's tried again next start.</summary>
    public async Task<bool> InstallAsync(CancellationToken ct)
    {
        if (File.Exists(ModelFile))
        {
            _installed = true;
            return true;
        }

        string temp = Path.Combine(_root, ModelName + ".download");
        try
        {
            Directory.CreateDirectory(_root);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(DownloadLimit);
            using (HttpResponseMessage response = await _http.GetAsync(Download, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(temp);
                await response.Content.CopyToAsync(file, limit.Token).ConfigureAwait(false);
            }

            string sha;
            await using (FileStream read = File.OpenRead(temp))
            {
                sha = Convert.ToHexStringLower(await SHA256.HashDataAsync(read, limit.Token).ConfigureAwait(false));
            }

            if (sha != DownloadSha256)
            {
                log.Fail(LogKind.Speech, "local_voice_bad_download");
                return false;
            }

            using Process tar = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "tar.exe"))
            {
                ArgumentList = { "-xf", temp, "-C", _root },
                CreateNoWindow = true,
                UseShellExecute = false,
            }) ?? throw new IOException("tar did not start");
            await tar.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            if (tar.ExitCode != 0 || !File.Exists(ModelFile))
            {
                log.Fail(LogKind.Speech, "local_voice_unpack_" + tar.ExitCode);
                return false;
            }

            _installed = true;
            log.Ok(LogKind.Speech, "local_voice_installed");
            Installed?.Invoke();
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            log.Fail(LogKind.Speech, "local_voice_download_" + MetaLog.Code(e));
            return false;
        }
        finally
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public async Task<bool> SpeakAsync(string text, Lang lang, int rate, Action started, bool isBook)
    {
        if (string.IsNullOrWhiteSpace(text) || !CanSpeak(isBook, lang))
        {
            return false;
        }

        CancellationToken stopped;
        (string Text, int Rate, Task<byte[]?> Audio)? ready;
        lock (_gate)
        {
            stopped = _stop.Token;
            ready = _ready;
            _ready = null;
        }

        try
        {
            byte[]? audio = ready is { } r && r.Text == text && r.Rate == rate
                ? await r.Audio.WaitAsync(stopped).ConfigureAwait(false)
                : await Task.Run(() => Make(text, rate), stopped).WaitAsync(stopped).ConfigureAwait(false);
            return audio is not null && await PlayAsync(audio, started, stopped, text.Length).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested)
        {
            return true;
        }
    }

    public void GetReady(string text, Lang lang, int rate, bool isBook)
    {
        if (string.IsNullOrWhiteSpace(text) || !CanSpeak(isBook, lang))
        {
            return;
        }

        lock (_gate)
        {
            if (_ready is not { } r || r.Text != text || r.Rate != rate)
            {
                _ready = (text, rate, Task.Run(() => Make(text, rate)));
            }
        }
    }

    /// <summary>Makes one sentence as a real sound file. Public for the Windows check.</summary>
    public byte[]? Make(string text, int rate)
    {
        var clock = Stopwatch.StartNew();
        byte[] wav;
        int samples;
        int sampleRate;
        try
        {
            lock (_engineGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _engine ??= Load();
                Interlocked.Exchange(ref _lastUsed, Environment.TickCount64);

                // The same 50 that means normal for every other voice. Ryan sounds best a little slower than his own normal.
                float speed = (float)Math.Clamp(NormalSpeed * (0.5 + (rate / 100.0)), 0.4, 2.0);
                OfflineTtsGeneratedAudio audio = _engine.Generate(text, speed, 0);
                samples = audio.Samples.Length;
                sampleRate = audio.SampleRate;
                if (samples == 0 || sampleRate <= 0)
                {
                    log.Fail(LogKind.Speech, "local_voice_empty");
                    return null;
                }

                wav = WavFile.FromSamples(audio.Samples, sampleRate);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException and not ObjectDisposedException)
        {
            log.Fail(LogKind.Speech, "local_voice_" + MetaLog.Code(e));
            return null;
        }

        Timed(text.Length, (double)samples / sampleRate, clock.Elapsed.TotalSeconds);
        return wav;
    }

    public void Stop()
    {
        MediaPlayer? player;
        TaskCompletionSource<bool>? playing;
        CancellationTokenSource old;
        lock (_gate)
        {
            player = _player;
            playing = _playing;
            _player = null;
            _playing = null;
            _ready = null;
            old = _stop;
            _stop = new CancellationTokenSource();
        }

        old.Cancel();
        playing?.TrySetResult(true);
        try
        {
            player?.Pause();
            player?.Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Speech, "local_voice_stop_" + MetaLog.Code(e));
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_engineGate)
        {
            _disposed = true;
            _unload?.Dispose();
            _engine?.Dispose();
            _engine = null;
        }

        _http.Dispose();
    }

    private OfflineTts Load()
    {
        var config = new OfflineTtsConfig();
        config.Model.Vits.Model = ModelFile;
        config.Model.Vits.Tokens = Path.Combine(ModelFolder, "tokens.txt");
        config.Model.Vits.DataDir = Path.Combine(ModelFolder, "espeak-ng-data");
        config.Model.NumThreads = Threads;
        config.Model.Provider = "cpu";
        config.Model.Debug = 0;
        _unload ??= new Timer(_ => UnloadIfIdle(), null, KeepLoaded, KeepLoaded);
        return new OfflineTts(config);
    }

    private void UnloadIfIdle()
    {
        lock (_engineGate)
        {
            if (_engine is not null && Environment.TickCount64 - Interlocked.Read(ref _lastUsed) >= KeepLoaded.TotalMilliseconds)
            {
                _engine.Dispose();
                _engine = null;
            }
        }
    }

    private void Timed(int chars, double audioSeconds, double madeInSeconds)
    {
        if (chars < WorthTiming)
        {
            return;
        }

        bool slow = madeInSeconds > audioSeconds;
        int count = slow ? Interlocked.Increment(ref _slowInARow) : Interlocked.Exchange(ref _slowInARow, 0) * 0;
        if (count >= TooSlowInARow && !_tooSlow)
        {
            _tooSlow = true;
            log.Fail(LogKind.Speech, "local_voice_too_slow");
            TooSlow?.Invoke();
        }
    }

    private async Task<bool> PlayAsync(byte[] wav, Action started, CancellationToken stopped, int chars)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer { AutoPlay = false, AudioCategory = MediaPlayerAudioCategory.Speech };
        using var audio = new MemoryStream(wav);
        using var stream = audio.AsRandomAccessStream();
        using var source = MediaSource.CreateFromStream(stream, "audio/wav");
        player.MediaEnded += (_, _) => done.TrySetResult(true);
        player.MediaFailed += (_, args) =>
        {
            log.Fail(LogKind.Speech, "local_voice_play_" + args.Error);
            done.TrySetResult(false);
        };
        lock (_gate)
        {
            if (stopped.IsCancellationRequested)
            {
                player.Dispose();
                return true;
            }

            _player = player;
            _playing = done;
            player.Source = source;
            started();
            player.Play();
        }

        Task finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(30) + TimeSpan.FromMilliseconds(chars * 250L))).ConfigureAwait(false);
        bool heard = finished == done.Task && await done.Task.ConfigureAwait(false);
        lock (_gate)
        {
            if (!ReferenceEquals(_player, player))
            {
                return true;
            }

            _player = null;
            _playing = null;
        }

        player.Dispose();
        return heard;
    }
}
