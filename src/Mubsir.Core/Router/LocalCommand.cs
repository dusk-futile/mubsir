namespace Mubsir.Core.Router;

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

public sealed record CommandMatch(LocalCommand Command, string? Argument, double Confidence)
{
    public string? ArgumentText { get; init; } = Argument;
}
