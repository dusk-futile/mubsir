using System.Text;

namespace Basira.Core.Input;

/// <summary>Commands of the double-tap command layer.</summary>
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

/// <summary>What the keyboard state machine asks the app to do.</summary>
public abstract record InputAction;

/// <summary>Empty Basira's speech queue and cancel NVDA speech.</summary>
public sealed record StopSpeech : InputAction;

/// <summary>Talk key went down: start capturing audio now so the first word is not clipped.</summary>
public sealed record TalkPreroll : InputAction;

/// <summary>Hold confirmed: play the listening earcon.</summary>
public sealed record TalkStarted : InputAction;

/// <summary>Talk key released after a confirmed hold: send the audio.</summary>
public sealed record TalkEnded : InputAction;

/// <summary>Discard the captured audio silently (no earcon).</summary>
public sealed record TalkCancelled : InputAction;

public sealed record LayerOpened : InputAction;

/// <summary>
/// The command layer closed. When it follows a <see cref="LayerKey"/> in the same decision the
/// layer closed because its command ran, so the app may skip the closing earcon.
/// </summary>
public sealed record LayerClosed : InputAction;

public sealed record LayerKey(LayerCommand Command) : InputAction;

public sealed record TaskStop : InputAction;

public sealed record TaskPause : InputAction;

public sealed record TaskResume : InputAction;

public sealed record ConfirmAccepted : InputAction;

public sealed record ConfirmRejected : InputAction;

public sealed record ConfirmRepeat : InputAction;

/// <summary>Basira is paused and the talk key was used: say "paused, press Enter to resume".</summary>
public sealed record PausedNotice : InputAction;

public sealed record ResumeFromPaused : InputAction;

/// <summary>Key explorer: say what this key does.</summary>
public sealed record DescribeKey(KeyEvent Key) : InputAction;

public sealed record KeyExplorerExited : InputAction;

/// <summary>Result for one key event: whether to hide it from apps and NVDA, and what to do.</summary>
public readonly record struct KeyDecision(bool Swallow, IReadOnlyList<InputAction> Actions)
{
    public override string ToString() => $"{(Swallow ? "swallow" : "pass")} {InputTrace.Format(Actions)}";
}

/// <summary>Compact text traces for debugging the state machine (dev pipe, logs in dev builds).</summary>
public static class InputTrace
{
    /// <summary>"[StopSpeech, LayerKey(Repeat)]".</summary>
    public static string Format(IReadOnlyList<InputAction>? actions)
    {
        if (actions is null || actions.Count == 0)
        {
            return "[]";
        }

        var sb = new StringBuilder("[");
        for (int i = 0; i < actions.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(Format(actions[i]));
        }

        return sb.Append(']').ToString();
    }

    public static string Format(InputAction action) => action switch
    {
        LayerKey k => $"LayerKey({k.Command})",
        DescribeKey d => $"DescribeKey({d.Key.Identity})",
        _ => action.GetType().Name,
    };

    /// <summary>"down sc=1D+ vk=A3 t=1000 -> pass [StopSpeech, TalkPreroll]".</summary>
    public static string Format(KeyEvent e, KeyDecision d) => $"{e} -> {d}";
}
