using Basira.Core.Common;
using Basira.Core.Text;

namespace Basira.Core.Speech;

/// <summary>One sentence ready to speak.</summary>
/// <param name="HistoryIndex">The sentence's <see cref="SpeechItem.Index"/>; the SSML mark is "s{HistoryIndex}".</param>
/// <param name="Ssml">SSML for NVDA's speakSsml.</param>
/// <param name="PlainText">The spoken words without markup (for the OneCore fallback and logs in debug mode).</param>
/// <param name="Runs">The language runs the SSML was built from.</param>
public sealed record ComposedSentence(int HistoryIndex, string Ssml, string PlainText, IReadOnlyList<TextRun> Runs);

/// <summary>Records an utterance in the history and turns each sentence into SSML.</summary>
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

    /// <summary>The SSML mark name for a history index.</summary>
    public static string MarkName(int historyIndex) => "s" + historyIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Parses a mark name back into a history index.</summary>
    public static bool TryParseMark(string? markName, out int historyIndex)
    {
        historyIndex = 0;
        return markName is ['s', .. var digits]
            && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out historyIndex);
    }
}
