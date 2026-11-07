namespace Mubsir.Core.Motion;

/// <summary>What the notch is showing.</summary>
public enum CompanionState
{
    Idle,
    Peek,
    Listening,
    Thinking,
    Doing,
    Pointing,
    Speaking,
    Reading,
    Confirm,
    Paid,
    Undone,
}

/// <summary>State of one step in the Doing checklist. A step is ticked only after it has finished.</summary>
public enum StepState
{
    Waiting,
    Running,
    Done,
    Undoing,
    Undone,
}

public static class CompanionStates
{
    /// <summary>The name used in motion-tokens.json ("idle", "listening", ...).</summary>
    public static string Key(this CompanionState state) => state switch
    {
        CompanionState.Idle => "idle",
        CompanionState.Peek => "peek",
        CompanionState.Listening => "listening",
        CompanionState.Thinking => "thinking",
        CompanionState.Doing => "doing",
        CompanionState.Pointing => "pointing",
        CompanionState.Speaking => "speaking",
        CompanionState.Reading => "reading",
        CompanionState.Confirm => "confirm",
        CompanionState.Paid => "paid",
        CompanionState.Undone => "undone",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    public static bool TryParse(string key, out CompanionState state)
    {
        foreach (CompanionState s in Enum.GetValues<CompanionState>())
        {
            if (string.Equals(s.Key(), key, StringComparison.OrdinalIgnoreCase))
            {
                state = s;
                return true;
            }
        }

        state = CompanionState.Idle;
        return false;
    }
}
