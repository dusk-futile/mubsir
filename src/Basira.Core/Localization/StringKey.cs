namespace Basira.Core.Localization;

/// <summary>Every sentence Basira says on its own (not AI answers). Texts live in <see cref="Strings"/>.</summary>
public enum StringKey
{
    Ready,
    NoInternet,
    AiBusyRetrying,
    AiUnavailable,

    /// <summary>Arg 0: amount spent this month in USD.</summary>
    BudgetWarning80,
    BudgetReached,
    BudgetExtraAllowed,
    MicSilent,
    HeardNothing,
    NvdaStopped,
    NvdaRestarted,
    NvdaRestartHint,
    Paused,
    PausedPressEnter,
    Resumed,
    TaskStopped,
    TaskPaused,
    TaskStepLimit,
    TaskTimeLimit,
    AdminWindow,
    PartialRead,
    BankModeOn,
    BankModeOff,
    AiLabel,
    ConfirmSuffix,
    ConfirmCancelled,
    ConfirmTimedOut,
    SuggestedByPage,
    PasswordField,

    /// <summary>Arg 0: the one-time code, already spaced for reading digit by digit.</summary>
    OtpLocal,
    PrivateModeOn,
    PrivateModeOff,
    PrivateModeBlocked,
    ReportSaved,

    /// <summary>Arg 0: version.</summary>
    UpdateReady,
    UpdateRolledBack,
    CrashRecovered,
    UacDuringTask,
    LayerHelp,
    KeyExplorerIntro,
    KeyExplorerExit,
    KeyExplorerNoAction,
    Thinking,
    NotUnderstood,
    NoApiKey,
    LocalStillWorks,
    TypeRequestHint,
    NothingToRepeat,

    /// <summary>Arg 0: app name.</summary>
    OpeningApp,

    /// <summary>Arg 0: app name.</summary>
    AppNotFound,

    /// <summary>Arg 0: app name; arg 1: window title.</summary>
    WhereAmI,

    /// <summary>Arg 0: time, already formatted.</summary>
    TimeNow,

    /// <summary>Arg 0: date, already formatted.</summary>
    DateToday,

    /// <summary>Arg 0: percent.</summary>
    BatteryLevel,
    BatteryCharging,
    NoBattery,
    Muted,
    Unmuted,

    /// <summary>Arg 0: percent.</summary>
    VolumeLevel,
    SpeakingFaster,
    SpeakingSlower,
    SettingsMenu,

    /// <summary>Arg 0: spent this month in USD; arg 1: the monthly limit in USD.</summary>
    BudgetStatus,
    NothingToRead,
    Done,

    // Key explorer: what a key does in Basira (the app may say the key's own name first).
    KeyHelpTalkAndLayer,
    KeyHelpTalk,
    KeyHelpLayer,
    KeyHelpControl,
    KeyHelpSpace,
    KeyHelpLeftArrow,
    KeyHelpRightArrow,
    KeyHelpUpArrow,
    KeyHelpDownArrow,
    KeyHelpPeriod,
    KeyHelpEnter,
    KeyHelpF1,
    KeyHelpNvdaKey,
}
