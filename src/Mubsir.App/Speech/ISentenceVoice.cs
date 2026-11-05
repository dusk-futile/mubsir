using Mubsir.Core.Common;

namespace Mubsir.App.Speech;

/// <summary>A voice that says one sentence at a time. If it can't, the next voice along says it.</summary>
internal interface ISentenceVoice
{
    bool CanSpeak(bool isBook, Lang lang);

    /// <summary>True if the sentence was heard, or was stopped by the user. False if another voice should say it.</summary>
    Task<bool> SpeakAsync(string text, Lang lang, int rate, Action started, bool isBook);

    /// <summary>Starts preparing the next sentence so there's less of a pause.</summary>
    void GetReady(string text, Lang lang, int rate, bool isBook);

    void Stop();
}
