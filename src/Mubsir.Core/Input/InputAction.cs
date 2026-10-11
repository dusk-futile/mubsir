namespace Mubsir.Core.Input;

public enum LayerCommand
{
    ReadFromHere,
    PreviousSentence,
    NextSentence,
    PreviousParagraph,
    NextParagraph,
    Repeat,
    TypeRequest,
    StopTask,
    Help,
}

public abstract record InputAction;

public sealed record StopSpeech : InputAction;

public sealed record TalkPreroll : InputAction;

public sealed record TalkStarted : InputAction;

public sealed record TalkEnded : InputAction;

public sealed record TalkCancelled : InputAction;

public sealed record LayerOpened : InputAction;

public sealed record LayerClosed : InputAction;

public sealed record LayerKey(LayerCommand Command) : InputAction;

public sealed record TaskStop : InputAction;

public sealed record TaskPause : InputAction;

public sealed record TaskResume : InputAction;

public sealed record ConfirmAccepted : InputAction;

public sealed record ConfirmRejected : InputAction;

public sealed record ConfirmRepeat : InputAction;

public sealed record PausedNotice : InputAction;

public sealed record ResumeFromPaused : InputAction;

public sealed record DescribeKey(KeyEvent Key) : InputAction;

public sealed record KeyExplorerExited : InputAction;

public readonly record struct KeyDecision(bool Swallow, IReadOnlyList<InputAction> Actions);

