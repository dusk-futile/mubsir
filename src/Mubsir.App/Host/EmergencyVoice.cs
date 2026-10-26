using Mubsir.Core.Common;
using Mubsir.Core.Logging;
using Mubsir.Core.Text;

namespace Mubsir.App.Host;

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
        }
    }
}
