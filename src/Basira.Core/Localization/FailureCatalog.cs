using Basira.Core.Common;

namespace Basira.Core.Localization;

/// <summary>Failure states the user can recover from without sighted help.</summary>
public enum FailureState
{
    Offline,
    ApiError,
    BudgetReached,
    MicSilent,
    Crashed,
    NvdaStopped,
    Paused,
    BadUpdate,
    NoApiKey,
}

/// <summary>What Basira plays and says for a failure, and an optional hint on how to recover.</summary>
public sealed record FailureInfo(Earcon Earcon, StringKey Message, StringKey? Recovery);

/// <summary>The spoken failure-state table.</summary>
public static class FailureCatalog
{
    public static FailureInfo Describe(FailureState s) => s switch
    {
        FailureState.Offline => new(Earcon.Error, StringKey.NoInternet, StringKey.LocalStillWorks),
        FailureState.ApiError => new(Earcon.Error, StringKey.AiBusyRetrying, StringKey.LocalStillWorks),
        FailureState.BudgetReached => new(Earcon.Error, StringKey.BudgetReached, StringKey.LocalStillWorks),
        FailureState.MicSilent => new(Earcon.Error, StringKey.MicSilent, StringKey.TypeRequestHint),
        FailureState.Crashed => new(Earcon.Ready, StringKey.CrashRecovered, null),
        FailureState.NvdaStopped => new(Earcon.Error, StringKey.NvdaStopped, StringKey.NvdaRestartHint),
        FailureState.Paused => new(Earcon.Paused, StringKey.PausedPressEnter, null),
        FailureState.BadUpdate => new(Earcon.Error, StringKey.UpdateRolledBack, null),
        FailureState.NoApiKey => new(Earcon.Error, StringKey.NoApiKey, null),
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown failure state."),
    };
}
