using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

public sealed record SpeechOptions(bool TransliterateArabizi = true, bool SpeakEmoji = true, VerbalizeMode Mode = VerbalizeMode.Normal);

public static class SpeechTextPipeline
{
    private static readonly SpeechOptions Defaults = new();

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
