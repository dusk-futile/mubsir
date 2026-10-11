using Basira.Core.Agent;
using Basira.Core.Common;
using Basira.Core.Config;
using Basira.Core.Context;
using Basira.Core.Speech;
using Basira.Core.Text;

namespace Basira.App.Abstractions;

// Seams between the two halves of the Windows layer:
//   host side  (input, speech, audio, storage, orchestration) implements ISpeechService, IAudioRecorder,
//              ISecretStore, ISettingsProvider, IConfirmer, INarrator, IUsageSink;
//   screen side (UI Automation, capture, OCR, brain, ears, actions) implements IScreenReader, IEars,
//              IBrain and IToolExecutor, registered from ScreenModule / BrainModule / ActionsModule.

/// <summary>Everything Basira says goes through here: one queue, NVDA first, OneCore otherwise.</summary>
public interface ISpeechService
{
    SpeechHistory History { get; }

    bool IsSpeaking { get; }

    /// <summary>Speech rate 0–100 (applies to the OneCore fallback; NVDA keeps its own rate).</summary>
    int Rate { get; set; }

    /// <summary>Queues text sentence by sentence. <paramref name="interrupt"/> empties the queue first.</summary>
    void Speak(string text, UtteranceKind kind, Lang lang, bool interrupt = false, VerbalizeMode mode = VerbalizeMode.Normal);

    /// <summary>Speaks and completes when the text has been spoken, stopped, or <paramref name="ct"/> fires.</summary>
    Task SpeakAsync(string text, UtteranceKind kind, Lang lang, CancellationToken ct, bool interrupt = false);

    /// <summary>Re-speaks items already in <see cref="History"/> (rewind, skip, say-all continuation).</summary>
    void SpeakItems(IReadOnlyList<SpeechItem> items, Lang lang, bool interrupt = true);

    /// <summary>Empties the queue and silences NVDA and OneCore.</summary>
    void Stop();

    void PlayEarcon(Earcon earcon);

    event Action<bool>? SpeakingChanged;
}

/// <summary>Microphone capture for push-to-talk: 16 kHz mono PCM16.</summary>
public interface IAudioRecorder : IDisposable
{
    bool IsRecording { get; }

    /// <summary>Starts capturing immediately (called on talk-key down, so the first word is kept).</summary>
    void Start();

    /// <summary>Stops and returns the clip as a WAV file plus simple level statistics.</summary>
    RecordedAudio Stop();

    /// <summary>Stops and discards the audio.</summary>
    void Cancel();
}

/// <param name="Wav">Complete RIFF WAV, 16 kHz mono PCM16.</param>
/// <param name="SpeechMs">Milliseconds above the local voice-activity threshold.</param>
/// <param name="Peak">Peak level 0–1. Near zero means a muted or blocked microphone.</param>
public sealed record RecordedAudio(byte[] Wav, int DurationMs, int SpeechMs, double Peak);

/// <summary>Hints that help speech-to-text with names on screen and the user's dialect.</summary>
public sealed record EarsHints(
    Lang PreferredLang,
    string? Dialect,
    string? AppName,
    string? FocusedName,
    IReadOnlyList<string> LocalCommandNames);

/// <param name="LocalCommand">A <c>Basira.Core.Router.LocalCommand</c> name when the request is a simple command, else null.</param>
public sealed record EarsResult(string Transcript, string? LocalCommand, string? LocalArgument, UsageRecord? Usage);

/// <summary>Speech-to-text for push-to-talk clips.</summary>
public interface IEars
{
    /// <exception cref="EarsException">Network, auth, quota or bad-request failures.</exception>
    Task<EarsResult> TranscribeAsync(byte[] wav, EarsHints hints, CancellationToken ct);
}

public sealed class EarsException(BrainErrorKind kind, string code) : Exception(code)
{
    public BrainErrorKind Kind { get; } = kind;

    public string Code { get; } = code;
}

public enum ReadScope
{
    Focus,
    Window,
    Document,
}

public sealed record ForegroundInfo(
    nint Hwnd,
    int ProcessId,
    string? ProcessName,
    string? WindowTitle,
    string? WindowClass,
    bool IsElevated,
    bool IsBrowser);

/// <summary>Reads the screen. All calls are time-budgeted and never block the caller for long.</summary>
public interface IScreenReader
{
    ForegroundInfo GetForeground();

    /// <summary>A pruned UI Automation snapshot plus the compact text the AI sees (ids map to live elements).</summary>
    Task<(ScreenContext Screen, BuiltContext Built)> CaptureContextAsync(ReadScope scope, CancellationToken ct);

    /// <summary>"Where am I": app, window and focused item as one short spoken line.</summary>
    Task<string?> DescribeFocusAsync(Lang lang, CancellationToken ct);

    /// <summary>Plain text to read aloud: visible text, or the whole document for <see cref="ReadScope.Document"/>.</summary>
    Task<string?> ReadTextAsync(ReadScope scope, CancellationToken ct);

    /// <summary>JPEG of the foreground window, downscaled to at most 1280 px on the long side.</summary>
    Task<byte[]?> CaptureForegroundJpegAsync(CancellationToken ct);

    /// <summary>Windows OCR (Arabic + English) of the foreground window, in logical reading order.</summary>
    Task<string?> OcrForegroundAsync(CancellationToken ct);
}

/// <summary>DPAPI-protected per-user secrets (API keys, relay token).</summary>
public interface ISecretStore
{
    string? Get(string name);

    void Set(string name, string? value);
}

public static class SecretNames
{
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
}

/// <summary>Current settings and profile, persisted under %LOCALAPPDATA%\Basira.</summary>
public interface ISettingsProvider
{
    BasiraSettings Settings { get; }

    UserProfile Profile { get; }

    void Update(Func<BasiraSettings, BasiraSettings> change);

    void UpdateProfile(Func<UserProfile, UserProfile> change);

    event Action? Changed;
}
