using Basira.App.Speech;
using Basira.Core.Common;
using Basira.Core.Logging;
using Basira.Core.Text;

namespace Basira.App.Host;

/// <summary>
/// One-off speech outside the normal speech service (supervisor messages, "already running"): NVDA when it runs,
/// otherwise a OneCore voice. Bounded in time; never throws.
/// </summary>
internal static class EmergencyVoice
{
    public static void Speak(string text, Lang lang, IEventLog log, TimeSpan maxWait)
    {
        try
        {
            var nvda = new NvdaController();
            if (nvda.IsRunning() && nvda.SpeakText(text) == 0)
            {
                return;
            }

            using var oneCore = new OneCoreSpeech(log);
            if (!oneCore.IsAvailable)
            {
                return;
            }

            using var cts = new CancellationTokenSource(maxWait);
            Task speaking = oneCore.SpeakAsync(SpeechTextPipeline.Prepare(text, lang), cts.Token);
            speaking.Wait(maxWait);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Last-resort speech: nothing else to fall back to.
        }
    }
}
