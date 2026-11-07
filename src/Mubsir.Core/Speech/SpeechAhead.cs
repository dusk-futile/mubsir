namespace Mubsir.Core.Speech;

/// <summary>How far ahead of the listener speech is prepared.</summary>
public static class SpeechAhead
{
    /// <summary>Sentences made ready while one plays: enough to hide the pause before the next, and little
    /// wasted if the user stops.</summary>
    public const int Sentences = 2;
}
