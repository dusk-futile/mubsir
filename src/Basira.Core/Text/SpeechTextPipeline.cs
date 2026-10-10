using Basira.Core.Common;

namespace Basira.Core.Text;

/// <summary>Options for <see cref="SpeechTextPipeline.Prepare"/>.</summary>
/// <param name="TransliterateArabizi">Turn Arabizi chat ("7abibi") into Arabic before speaking (normal mode only).</param>
/// <param name="SpeakEmoji">Name emoji; when false they are removed.</param>
/// <param name="Mode">Normal reading, math reading, or spelling.</param>
public sealed record SpeechOptions(bool TransliterateArabizi = true, bool SpeakEmoji = true, VerbalizeMode Mode = VerbalizeMode.Normal);

/// <summary>Turns raw text into language runs ready for SSML.</summary>
public static class SpeechTextPipeline
{
    private static readonly SpeechOptions Defaults = new();

    /// <summary>
    /// Presentation forms → emoji names → Arabizi → symbols → language runs.
    /// </summary>
    /// <remarks>
    /// In normal mode, emoji names and symbol words use the text's own dominant language (Arabizi counted as Arabic),
    /// so an English sentence gets English names inside an Arabic session. Math and spell modes produce words in
    /// <paramref name="defaultLang"/> and skip Arabizi, so the exact characters are read; spell mode names each emoji itself.
    /// </remarks>
    public static IReadOnlyList<TextRun> Prepare(string text, Lang defaultLang, SpeechOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= Defaults;

        string s = ArabicNormalizer.NormalizePresentationForms(text);
        switch (options.Mode)
        {
            case VerbalizeMode.Math:
                s = options.SpeakEmoji ? EmojiNames.Replace(s, defaultLang, parenthesize: false) : EmojiNames.Remove(s);
                s = SymbolVerbalizer.Verbalize(s, defaultLang, VerbalizeMode.Math);
                break;

            case VerbalizeMode.Spell:
                if (!options.SpeakEmoji)
                {
                    s = EmojiNames.Remove(s);
                }

                s = SymbolVerbalizer.Verbalize(s, defaultLang, VerbalizeMode.Spell);
                break;

            default:
                s = options.SpeakEmoji
                    ? EmojiNames.Replace(s, ContextLanguage(s, defaultLang, options.TransliterateArabizi))
                    : EmojiNames.Remove(s);
                if (options.TransliterateArabizi)
                {
                    s = Arabizi.TransliterateArabiziWords(s);
                }

                s = SymbolVerbalizer.Verbalize(s, ScriptSegmenter.DominantLanguage(s, defaultLang), VerbalizeMode.Normal);
                break;
        }

        return ScriptSegmenter.Segment(s, defaultLang);
    }

    /// <summary>Dominant language by letters, counting Arabizi words as Arabic.</summary>
    internal static Lang ContextLanguage(string text, Lang defaultLang, bool arabizi)
    {
        if (!arabizi)
        {
            return ScriptSegmenter.DominantLanguage(text, defaultLang);
        }

        int arabic = 0, other = 0;
        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var (a, o) = ScriptInfo.CountLetters(token);
            if (o > 0 && Arabizi.LooksLikeArabizi(token))
            {
                arabic += a + o;
            }
            else
            {
                arabic += a;
                other += o;
            }
        }

        return ScriptInfo.Dominant(arabic, other, defaultLang);
    }
}
