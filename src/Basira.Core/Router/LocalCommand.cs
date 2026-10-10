namespace Basira.Core.Router;

/// <summary>Commands Basira runs locally, without the AI.</summary>
public enum LocalCommand
{
    ReadScreen,
    ReadFromHere,
    WhereAmI,
    ScrollDown,
    ScrollUp,
    PageDown,
    PageUp,
    GoToTop,
    GoToBottom,
    Stop,
    Repeat,
    PreviousSentence,
    NextSentence,
    SpeakFaster,
    SpeakSlower,
    OpenApp,
    GoBack,
    GoForward,
    NewTab,
    CloseTab,
    NewWindow,
    SwitchWindow,
    Pause,
    Resume,
    Help,
    WhatTime,
    WhatDate,
    BatteryStatus,
    VolumeUp,
    VolumeDown,
    Mute,
    PrivateModeOn,
    PrivateModeOff,
    OcrWindow,
    Settings,
    SaveReport,
    KeyExplorer,
}

/// <summary>
/// A local command recognized in an utterance. <paramref name="Argument"/> is set for
/// <see cref="LocalCommand.OpenApp"/> (the folded app name). <paramref name="Confidence"/> is 1.0
/// for an exact phrase and lower when polite fillers were stripped or the verb form was adapted.
/// </summary>
public sealed record CommandMatch(LocalCommand Command, string? Argument, double Confidence);
