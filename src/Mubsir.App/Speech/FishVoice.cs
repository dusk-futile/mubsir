using Mubsir.Core.Speech;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Cost;
using Mubsir.Core.Logging;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Mubsir.App.Speech;

/// <summary>
/// Fish Audio's voice. It never leaves the user in silence: any failure returns false and the free Windows voice
/// speaks instead. After a failure it rests, and a key that's wrong or out of credit rests until it's changed.
/// </summary>
internal sealed class FishVoice(
    SecretStore secrets,
    SettingsProvider settings,
    IEventLog log,
    CostLedger? ledger = null,
    Action? onCostRecorded = null,
    FishVoiceCache? cache = null,
    HttpMessageHandler? handler = null) : IDisposable
{
    private const string Endpoint = "https://api.fish.audio/v1/tts";
    private const int MaxChars = 2000;

    /// <summary>What's been heard is kept, so hearing a page again is free. About 16 hours of listening.</summary>
    private const long BookCacheBytes = 500L * 1024 * 1024;
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FirstAudioTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan WholeAudioTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RestAfterFailure = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http = new(handler ?? new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan };
    private ReadySentence? _ready;
    private readonly FishVoiceCache _cache = cache ?? new FishVoiceCache(log);
    private readonly FishVoiceCache _bookCache = new(log, AppPaths.VoiceCacheDir + "-books", BookCacheBytes, BookCacheBytes * 9 / 10, MaxChars);
    private readonly object _gate = new();
    private CancellationTokenSource _stop = new();
    private MediaPlayer? _player;
    private TaskCompletionSource<bool>? _playing;
    private DateTime _restUntil = DateTime.MinValue;
    private string? _rejectedKey;

    /// <summary>Set once when the key stops working, so the user can be told why the voice changed.</summary>
    public event Action<int>? KeyRejected;

    public bool HasKey => !string.IsNullOrWhiteSpace(secrets.Get(SecretNames.Fish));

    /// <summary>Plays a voice's public sample. It needs no key and costs nothing. False means the user stopped it.</summary>
    public async Task<bool> PlaySampleAsync(string url, TimeSpan most, CancellationToken ct)
    {
        CancellationToken stopped;
        lock (_gate)
        {
            stopped = _stop.Token;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopped, ct);
            linked.CancelAfter(SampleTimeout);
            byte[] sample = await _http.GetByteArrayAsync(url, linked.Token).ConfigureAwait(false);
            await PlayAsync(new MemoryStream(sample), () => { }, most, stopped).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            log.Fail(LogKind.Speech, "fish_sample_" + MetaLog.Code(e));
        }

        return !stopped.IsCancellationRequested;
    }

    public bool CanSpeak(bool isBook, Lang lang = Lang.English)
    {
        MubsirSettings s = settings.Settings;
        bool bookOff = isBook && !(lang == Lang.Arabic ? s.ReadArabicBooksWithFish : s.ReadBooksWithFish);
        if (!s.UseFishVoice || s.PrivateMode || bookOff || DateTime.UtcNow < _restUntil)
        {
            return false;
        }

        string? key = secrets.Get(SecretNames.Fish);
        return key is not null && key != _rejectedKey;
    }

    /// <summary>True if the sentence was heard, or was stopped by the user. False if another voice should say it.</summary>
    public async Task<bool> SpeakAsync(string text, Lang lang, int rate, Action started, bool isBook = false)
    {
        string? key = secrets.Get(SecretNames.Fish);
        if (key is null || string.IsNullOrWhiteSpace(text) || text.Length > MaxChars)
        {
            return false;
        }

        CancellationToken stopped;
        lock (_gate)
        {
            stopped = _stop.Token;
        }

        double speed = Math.Clamp(0.5 + (rate / 100.0), 0.5, 2.0);
        string voice = VoiceFor(lang);
        FishVoiceCache store = isBook ? _bookCache : _cache;
        bool canCache = !settings.Settings.PrivateMode && text.Length <= store.Limit;

        if (canCache && store.TryGet(text, voice, speed, out byte[] cachedAudio))
        {
            try
            {
                var cachedStream = new MemoryStream(cachedAudio);
                return await PlayAsync(cachedStream, started, TimeSpan.FromSeconds(30) + TimeSpan.FromMilliseconds(text.Length * 250L), stopped).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.Fail(LogKind.Speech, "fish_cached_play_" + MetaLog.Code(e));
                // Fall back to fetching if playing from cache failed
            }
        }

        try
        {
            byte[]? audio = await TakeReadyAsync(text, lang, isBook, stopped).ConfigureAwait(false)
                ?? await FetchAsync(key, text, lang, rate, voice, speed, store, canCache, stopped).ConfigureAwait(false);
            return audio is not null
                && await PlayAsync(new MemoryStream(audio), started, TimeSpan.FromSeconds(30) + TimeSpan.FromMilliseconds(text.Length * 250L), stopped).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            log.Fail(LogKind.Speech, "fish_" + MetaLog.Code(e));
            _restUntil = DateTime.UtcNow + RestAfterFailure;
            return false;
        }
    }

    /// <summary>
    /// Gets the next sentence's audio while this one plays, so there's less of a pause. Only one is ever ready, so
    /// stopping costs at most that sentence, and it's saved, so hearing it later is free.
    /// </summary>
    public void GetReady(string text, Lang lang, int rate, bool isBook)
    {
        string? key = secrets.Get(SecretNames.Fish);
        if (key is null || string.IsNullOrWhiteSpace(text) || text.Length > MaxChars || !CanSpeak(isBook, lang))
        {
            return;
        }

        double speed = Math.Clamp(0.5 + (rate / 100.0), 0.5, 2.0);
        string voice = VoiceFor(lang);
        FishVoiceCache store = isBook ? _bookCache : _cache;
        bool canCache = text.Length <= store.Limit;
        if (canCache && store.Has(text, voice, speed))
        {
            return;
        }

        CancellationToken stopped;
        lock (_gate)
        {
            if (_ready is not null && _ready.Text == text && _ready.IsBook == isBook)
            {
                return;
            }

            stopped = _stop.Token;
            _ready = new ReadySentence(text, lang, isBook, FetchQuietlyAsync(key, text, lang, rate, voice, speed, store, canCache, stopped));
        }
    }

    private async Task<byte[]?> FetchQuietlyAsync(string key, string text, Lang lang, int rate, string voice, double speed, FishVoiceCache store, bool canCache, CancellationToken stopped)
    {
        try
        {
            return await FetchAsync(key, text, lang, rate, voice, speed, store, canCache, stopped).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        {
            return null;
        }
    }

    private async Task<byte[]?> TakeReadyAsync(string text, Lang lang, bool isBook, CancellationToken stopped)
    {
        ReadySentence? ready;
        lock (_gate)
        {
            ready = _ready;
            _ready = null;
        }

        return ready is not null && ready.Text == text && ready.Lang == lang && ready.IsBook == isBook
            ? await ready.Audio.WaitAsync(stopped).ConfigureAwait(false)
            : null;
    }

    private async Task<byte[]?> FetchAsync(string key, string text, Lang lang, int rate, string voice, double speed, FishVoiceCache store, bool canCache, CancellationToken stopped)
    {
        using var firstAudio = CancellationTokenSource.CreateLinkedTokenSource(stopped);
        firstAudio.CancelAfter(FirstAudioTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.TryAddWithoutValidation("model", "s2.1-pro");
        request.Content = new StringContent(Body(text, lang, rate, voice), System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, firstAudio.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Rest(key, response.StatusCode);
            return null;
        }

        firstAudio.CancelAfter(WholeAudioTimeout);
        using var audio = new MemoryStream();
        await response.Content.CopyToAsync(audio, firstAudio.Token).ConfigureAwait(false);
        if (audio.Length == 0)
        {
            Rest(key, HttpStatusCode.NoContent);
            return null;
        }

        // Count costs for text actually sent over the network
        ledger?.AddFishVoice(System.Text.Encoding.UTF8.GetByteCount(text));
        onCostRecorded?.Invoke();
        byte[] bytes = audio.ToArray();
        if (canCache)
        {
            store.Put(text, voice, speed, bytes);
        }

        return bytes;
    }

    private string VoiceFor(Lang lang) =>
        (lang == Lang.Arabic ? settings.Settings.FishVoiceIdArabic : settings.Settings.FishVoiceId) ?? VoiceCatalog.Default(lang == Lang.Arabic ? Lang.Arabic : Lang.English).Id;

    private sealed record ReadySentence(string Text, Lang Lang, bool IsBook, Task<byte[]?> Audio);

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
            old = _stop;
            _stop = new CancellationTokenSource();
            _ready = null;
        }

        // Not disposed: a sentence starting right now may still be linking to its token.
        old.Cancel();
        playing?.TrySetResult(true);
        try
        {
            player?.Pause();
            player?.Dispose();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Speech, "fish_stop_" + MetaLog.Code(e));
        }
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }

    private static string Body(string text, Lang lang, int rate, string? voice)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("text", text);
            json.WriteString("format", "mp3");
            json.WriteString("latency", "balanced");
            if (!string.IsNullOrWhiteSpace(voice))
            {
                json.WriteString("reference_id", voice);
            }

            json.WriteStartObject("prosody");
            json.WriteNumber("speed", Math.Clamp(0.5 + (rate / 100.0), 0.5, 2.0));
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void Rest(string key, HttpStatusCode status)
    {
        int code = (int)status;
        log.Fail(LogKind.Speech, "fish_http_" + code);
        if (code is 401 or 402 or 403)
        {
            bool first = _rejectedKey != key;
            _rejectedKey = key;
            if (first)
            {
                KeyRejected?.Invoke(code);
            }
        }
        else
        {
            _restUntil = DateTime.UtcNow + RestAfterFailure;
        }
    }

    private async Task<bool> PlayAsync(MemoryStream audio, Action started, TimeSpan limit, CancellationToken stopped)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var player = new MediaPlayer { AutoPlay = false, AudioCategory = MediaPlayerAudioCategory.Speech };
        using (audio)
        using (var stream = audio.AsRandomAccessStream())
        using (var source = MediaSource.CreateFromStream(stream, "audio/mpeg"))
        {
            player.MediaEnded += (_, _) => done.TrySetResult(true);
            player.MediaFailed += (_, args) =>
            {
                log.Fail(LogKind.Speech, "fish_play_" + args.Error);
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

                // Under the lock, so Stop can't dispose the player between these lines.
                player.Source = source;
                started();
                player.Play();
            }

            Task finished = await Task.WhenAny(done.Task, Task.Delay(limit)).ConfigureAwait(false);
            bool heard = finished == done.Task && await done.Task.ConfigureAwait(false);
            lock (_gate)
            {
                // Stop already took the player and disposed it, and the user meant it.
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
}
