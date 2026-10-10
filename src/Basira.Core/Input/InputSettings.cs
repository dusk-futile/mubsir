namespace Basira.Core.Input;

/// <summary>Keys and timings of the keyboard state machine.</summary>
public sealed record InputSettings
{
    public static InputSettings Default { get; } = new();

    /// <summary>Hold to talk. A modifier talk key is never swallowed; a non-modifier one (Apps, ScrollLock) is.</summary>
    public KeyIdentity TalkKey { get; init; } = KeyIdentity.RightCtrl;

    /// <summary>Double-tap to open the command layer.</summary>
    public KeyIdentity LayerKey { get; init; } = KeyIdentity.RightCtrl;

    /// <summary>Talk-key hold length that turns a press into talking.</summary>
    public int HoldThresholdMs { get; init; } = 300;

    /// <summary>Max gap between the first tap's key-up and the second tap's key-down.</summary>
    public int DoubleTapWindowMs { get; init; } = 400;

    /// <summary>Longest press that still counts as a tap for stopping an agent task (and for a separate layer key).</summary>
    public int TapMaxMs { get; init; } = 300;

    public int LayerTimeoutMs { get; init; } = 3000;

    /// <summary>Enter presses this soon after a confirmation prompt finishes speaking are ignored.</summary>
    public int ConfirmIgnoreMs { get; init; } = 400;

    /// <summary>After a "paused" notice, Enter within this window resumes Basira.</summary>
    public int PausedNoticeWindowMs { get; init; } = 3000;

    /// <summary>Safety cap: a talk longer than this ends by itself (protects against a lost key-up).</summary>
    public int MaxTalkMs { get; init; } = 120_000;

    /// <summary>Throws when a timing is negative or a key is missing.</summary>
    public void Validate()
    {
        if (TalkKey.IsEmpty)
        {
            throw new ArgumentException("Talk key is not set.", nameof(TalkKey));
        }

        if (LayerKey.IsEmpty)
        {
            throw new ArgumentException("Layer key is not set.", nameof(LayerKey));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(HoldThresholdMs);
        ArgumentOutOfRangeException.ThrowIfNegative(DoubleTapWindowMs);
        ArgumentOutOfRangeException.ThrowIfNegative(TapMaxMs);
        ArgumentOutOfRangeException.ThrowIfNegative(LayerTimeoutMs);
        ArgumentOutOfRangeException.ThrowIfNegative(ConfirmIgnoreMs);
        ArgumentOutOfRangeException.ThrowIfNegative(PausedNoticeWindowMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxTalkMs);
    }
}
