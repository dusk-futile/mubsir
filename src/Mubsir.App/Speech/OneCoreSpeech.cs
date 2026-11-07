using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;
using Windows.Media.SpeechSynthesis;

namespace Mubsir.App.Speech;

internal sealed class OneCoreSpeech : IDisposable
{
    private static readonly TimeSpan SynthesisTimeout = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly IEventLog _log;
    private SpeechSynthesizer? _arabic;
    private SpeechSynthesizer? _english;
    private bool _initialized;
    private bool _available;
    private SentencePlayer? _out;
    private CancellationTokenSource _stop = new();
    private double _rate = RateMapping.ToOneCore(50);

    public OneCoreSpeech(IEventLog log)
    {
        _log = log;
        _out = new SentencePlayer(log, "onecore");
    }

    public bool IsAvailable
    {
        get
        {
            EnsureInitialized();
            return _available;
        }
    }

    public bool HasArabicVoice { get; private set; }

    public int Rate
    {
        set
        {
            _rate = RateMapping.ToOneCore(value);
            lock (_gate)
            {
                TrySetRate(_arabic);
                TrySetRate(_english);
            }
        }
    }

    public async Task SpeakAsync(IReadOnlyList<TextRun> runs, CancellationToken ct)
    {
        if (runs.Count == 0 || !IsAvailable)
        {
            return;
        }

        CancellationToken stopToken;
        lock (_gate)
        {
            stopToken = _stop.Token;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, stopToken);
        CancellationToken token = linked.Token;
        try
        {
            // Every language piece of the sentence is made at once, then joined into one clip, so switching
            // between Arabic and English sounds like one reader with a word's pause, not two clips.
            SpeechSynthesisStream?[] streams = await Task.WhenAll(runs.Select(r => SynthesizeAsync(r, token))).ConfigureAwait(false);
            var pieces = new List<(byte[] Audio, string Type)>();
            try
            {
                foreach (SpeechSynthesisStream? stream in streams)
                {
                    if (stream is not null)
                    {
                        pieces.Add((await ReadAllAsync(stream, token).ConfigureAwait(false), stream.ContentType));
                    }
                }
            }
            finally
            {
                foreach (SpeechSynthesisStream? stream in streams)
                {
                    stream?.Dispose();
                }
            }

            TimeSpan limit = EstimatePlayTimeout(string.Concat(runs.Select(r => r.Text)));
            SpeechClip?[] clips = pieces.Select(p => SpeechAudio.ReadWav(p.Audio)).ToArray();
            if (clips.Length > 0 && clips.All(c => c is not null))
            {
                SpeechClip joined = SpeechAudio.Join(clips!);
                if (joined.Samples.Length > 0)
                {
                    await PlayAsync(WavFile.FromSamples(joined.Samples, joined.SampleRate), "audio/wav", limit, token).ConfigureAwait(false);
                }

                return;
            }

            // Not plain WAV on this PC: play the pieces one after another as Windows made them.
            foreach (var (audio, type) in pieces)
            {
                token.ThrowIfCancellationRequested();
                await PlayAsync(audio, type, limit, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("onecore_speak", e);
        }
    }

    public void Stop()
    {
        CancellationTokenSource old;
        lock (_gate)
        {
            old = _stop;
            _stop = new CancellationTokenSource();
        }

        try
        {
            old.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        old.Dispose();
        _out?.Stop();
    }

    public void Dispose()
    {
        Stop();
        _out?.Dispose();
        _out = null;
        lock (_gate)
        {
            _arabic?.Dispose();
            _english?.Dispose();
            _arabic = null;
            _english = null;
        }
    }

    private static TimeSpan EstimatePlayTimeout(string text) =>
        TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(Math.Min(text.Length, 4000) * 200);

    private void EnsureInitialized()
    {
        lock (_gate)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            try
            {
                IReadOnlyList<VoiceInformation> voices = SpeechSynthesizer.AllVoices;
                VoiceInformation? ar = VoicePicker.Pick(voices.Select(v => (v, v.Language)).ToList(), Lang.Arabic);
                VoiceInformation? en = VoicePicker.Pick(voices.Select(v => (v, v.Language)).ToList(), Lang.English);
                VoiceInformation? fallback = SpeechSynthesizer.DefaultVoice;
                HasArabicVoice = ar is not null;
                if (ar is null)
                {
                    _log.Error("onecore_no_arabic_voice", LogKind.Speech);
                }

                _arabic = Create(ar ?? fallback);
                _english = Create(en ?? fallback);
                _available = _arabic is not null || _english is not null;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.Failure("onecore_init", e);
                _available = false;
            }
        }
    }

    private SpeechSynthesizer? Create(VoiceInformation? voice)
    {
        try
        {
            var synth = new SpeechSynthesizer();
            if (voice is not null)
            {
                synth.Voice = voice;
            }

            synth.Options.AppendedSilence = SpeechAppendedSilence.Min;
            synth.Options.PunctuationSilence = SpeechPunctuationSilence.Min;
            TrySetRate(synth);
            return synth;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("onecore_create", e);
            return null;
        }
    }

    private void TrySetRate(SpeechSynthesizer? synth)
    {
        if (synth is null)
        {
            return;
        }

        try
        {
            synth.Options.SpeakingRate = _rate;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
        }
    }

    private async Task<SpeechSynthesisStream?> SynthesizeAsync(TextRun run, CancellationToken ct)
    {
        string text = run.SpellOut ? string.Join(' ', run.Text.Where(c => !char.IsWhiteSpace(c))) : run.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        SpeechSynthesizer? synth;
        lock (_gate)
        {
            synth = run.Lang == Lang.Arabic ? _arabic ?? _english : _english ?? _arabic;
        }

        if (synth is null)
        {
            return null;
        }

        try
        {
            return await synth.SynthesizeTextToStreamAsync(text).AsTask(ct).WaitAsync(SynthesisTimeout, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            _log.Error("onecore_synthesis_timeout", LogKind.Speech, LogOutcome.TimedOut);
            return null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("onecore_synthesis", e);
            return null;
        }
    }

    private async Task PlayAsync(byte[] audio, string contentType, TimeSpan limit, CancellationToken ct)
    {
        if (_out is { } player)
        {
            await player.PlayAsync(audio, contentType, () => { }, limit, ct).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadAllAsync(SpeechSynthesisStream stream, CancellationToken ct)
    {
        using var copy = new MemoryStream();
        await stream.AsStreamForRead().CopyToAsync(copy, ct).ConfigureAwait(false);
        return copy.ToArray();
    }
}

internal static class VoicePicker
{
    private static readonly string[] ArabicPreferred = ["ar-SA", "ar-EG"];
    private static readonly string[] EnglishPreferred = ["en-US", "en-GB"];

    public static T? Pick<T>(IReadOnlyList<(T Voice, string Language)> voices, Lang lang)
    where T : class
    {
        string prefix = lang.Tag();
        string[] preferred = lang == Lang.Arabic ? ArabicPreferred : EnglishPreferred;
        foreach (string region in preferred)
        {
            foreach (var (voice, language) in voices)
            {
                if (string.Equals(language, region, StringComparison.OrdinalIgnoreCase))
                {
                    return voice;
                }
            }
        }

        foreach (var (voice, language) in voices)
        {
            if (language is not null && language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return voice;
            }
        }

        return null;
    }
}

internal static class RateMapping
{
    public const double MinOneCore = 0.6;
    public const double MaxOneCore = 2.5;

    public const double Normal = 1.0;

    public static double ToOneCore(int rate)
    {
        int r = Math.Clamp(rate, 0, 100);
        return r <= 50
            ? MinOneCore + ((Normal - MinOneCore) * r / 50.0)
            : Normal + ((MaxOneCore - Normal) * (r - 50) / 50.0);
    }
}
