using Mubsir.Core.Common;
using Mubsir.Core.Text;

namespace Mubsir.Core.Speech;

public sealed record ComposedSentence(int HistoryIndex, string Ssml, string PlainText, IReadOnlyList<TextRun> Runs);

public static class SpeechComposer
{
    public static IReadOnlyList<ComposedSentence> Compose(
        string text,
        Lang defaultLang,
        SpeechHistory history,
        UtteranceKind kind,
        SpeechOptions? options = null,
        LangTagStyle style = LangTagStyle.VoiceElement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(history);

        var (_, items) = history.AddUtteranceWithItems(text, kind);
        var result = new List<ComposedSentence>(items.Count);
        foreach (var item in items)
        {
            var runs = SpeechTextPipeline.Prepare(item.Text, defaultLang, options);
            string ssml = SsmlBuilder.Build(runs, MarkName(item.Index), style);
            result.Add(new ComposedSentence(item.Index, ssml, string.Concat(runs.Select(r => r.Text)), runs));
        }

        return result;
    }

    public static string MarkName(int historyIndex) => "s" + historyIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static bool TryParseMark(string? markName, out int historyIndex)
    {
        historyIndex = 0;
        return markName is ['s', .. var digits]
            && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out historyIndex);
    }
}
