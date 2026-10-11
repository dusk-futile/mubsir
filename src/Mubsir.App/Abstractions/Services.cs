using Mubsir.Core.Agent;
using Mubsir.Core.Common;

namespace Mubsir.App.Abstractions;



public sealed record RecordedAudio(byte[] Wav, int DurationMs, int SpeechMs, double Peak);

public sealed record EarsHints(
    Lang PreferredLang,
    string? Dialect,
    string? AppName,
    string? FocusedName,
    IReadOnlyList<string> LocalCommandNames);

public sealed record EarsResult(string Transcript, string? LocalCommand, string? LocalArgument, UsageRecord? Usage);


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



public static class SecretNames
{
    public const string Anthropic = "anthropic";
    public const string Gemini = "gemini";
}

