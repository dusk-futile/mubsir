namespace Mubsir.Core.Input;

public sealed record InputSettings
{
    public static InputSettings Default { get; } = new();

    public KeyIdentity TalkKey { get; init; } = KeyIdentity.RightCtrl;

    public KeyIdentity LayerKey { get; init; } = KeyIdentity.RightCtrl;

    public int HoldThresholdMs { get; init; } = 300;

    public int DoubleTapWindowMs { get; init; } = 400;

    public int TapMaxMs { get; init; } = 300;

    public int LayerTimeoutMs { get; init; } = 3000;

    public int ConfirmIgnoreMs { get; init; } = 400;

    public int PausedNoticeWindowMs { get; init; } = 3000;

    public int MaxTalkMs { get; init; } = 120_000;

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
