using Basira.Core.Common;

namespace Basira.Core.Text;

/// <summary>
/// Splits mixed Arabic/English text into language runs so each part is spoken by the right voice.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Arabic-script letters and marks are Arabic; every other letter (Latin, accented) is English.</item>
/// <item>Neutral characters (spaces, punctuation, symbols) between two runs of the same language stay in it;
/// between different languages they join the preceding run (or the following one when the preceding run is spelled out).</item>
/// <item>Numbers follow the dominant language of their sentence by letter count (ties go to the default language).
/// A number glued to a word without a space (Win10, 2026م) follows that word instead.</item>
/// <item>Short all-caps Latin acronyms (2–5 letters) inside Arabic sentences become English runs with
/// <see cref="TextRun.SpellOut"/> set.</item>
/// <item>Run texts always concatenate back to the exact input.</item>
/// </list>
/// </remarks>
public static class ScriptSegmenter
{
    private enum Cls : byte
    {
        Neutral,
        Arabic,
        Latin,
        Digit,
    }

    public static IReadOnlyList<TextRun> Segment(string text, Lang defaultLang)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return [];
        }

        int n = text.Length;
        Cls[] cls = Classify(text);
        var (arabicTotal, latinTotal) = CountLetters(text, cls, 0, n);
        if (arabicTotal + latinTotal == 0)
        {
            return [new TextRun(text, defaultLang)];
        }

        Lang textDominant = ScriptInfo.Dominant(arabicTotal, latinTotal, defaultLang);
        Lang[] sentenceLang = SentenceDominance(text, cls, textDominant, defaultLang);

        var lang = new Lang[n];
        var assigned = new bool[n];
        var spell = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (cls[i] == Cls.Arabic)
            {
                lang[i] = Lang.Arabic;
                assigned[i] = true;
            }
            else if (cls[i] == Cls.Latin)
            {
                lang[i] = Lang.English;
                assigned[i] = true;
            }
        }

        MarkAcronyms(text, cls, sentenceLang, spell);
        AssignNumbers(text, cls, sentenceLang, lang, assigned);
        AssignNeutrals(lang, assigned, spell);
        return BuildRuns(text, lang, spell);
    }

    /// <summary>The language with more letters in <paramref name="text"/>; ties and letterless text give <paramref name="defaultLang"/>.</summary>
    public static Lang DominantLanguage(string text, Lang defaultLang)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (arabic, other) = ScriptInfo.CountLetters(text);
        return ScriptInfo.Dominant(arabic, other, defaultLang);
    }

    private static Cls[] Classify(string text)
    {
        var cls = new Cls[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsDigit(c))
            {
                cls[i] = Cls.Digit;
            }
            else if (ScriptInfo.IsArabicScript(c))
            {
                cls[i] = ScriptInfo.IsArabicStrong(c) ? Cls.Arabic : Cls.Neutral;
            }
            else if (char.IsLetter(c))
            {
                cls[i] = Cls.Latin;
            }
            else if (i > 0 && ScriptInfo.IsCombiningMark(c) && cls[i - 1] is Cls.Arabic or Cls.Latin)
            {
                cls[i] = cls[i - 1];
            }
            else
            {
                cls[i] = Cls.Neutral;
            }
        }

        return cls;
    }

    private static (int Arabic, int Latin) CountLetters(string text, Cls[] cls, int start, int end)
    {
        int arabic = 0, latin = 0;
        for (int i = start; i < end; i++)
        {
            if (!char.IsLetter(text[i]))
            {
                continue;
            }

            if (cls[i] == Cls.Arabic)
            {
                arabic++;
            }
            else if (cls[i] == Cls.Latin)
            {
                latin++;
            }
        }

        return (arabic, latin);
    }

    /// <summary>Dominant language per character, computed sentence by sentence.</summary>
    private static Lang[] SentenceDominance(string text, Cls[] cls, Lang textDominant, Lang defaultLang)
    {
        int n = text.Length;
        var result = new Lang[n];
        int start = 0;
        for (int i = 0; i < n; i++)
        {
            bool end = i == n - 1
                || ScriptInfo.IsLineBreak(text[i])
                || (IsSentenceTerminator(text[i]) && char.IsWhiteSpace(text[i + 1]));
            if (!end)
            {
                continue;
            }

            var (arabic, latin) = CountLetters(text, cls, start, i + 1);
            Lang d = arabic + latin == 0 ? textDominant : ScriptInfo.Dominant(arabic, latin, defaultLang);
            Array.Fill(result, d, start, i + 1 - start);
            start = i + 1;
        }

        return result;
    }

    private static bool IsSentenceTerminator(char c) => c is '.' or '!' or '?' or '؟' or '…' or '۔' or '！' or '？';

    private static void MarkAcronyms(string text, Cls[] cls, Lang[] sentenceLang, bool[] spell)
    {
        int n = text.Length;
        int i = 0;
        while (i < n)
        {
            if (cls[i] != Cls.Latin)
            {
                i++;
                continue;
            }

            int start = i;
            while (i < n && cls[i] == Cls.Latin)
            {
                i++;
            }

            int length = i - start;
            if (length is < 2 or > 5 || sentenceLang[start] != Lang.Arabic)
            {
                continue;
            }

            bool allCaps = true;
            for (int k = start; k < i && allCaps; k++)
            {
                allCaps = text[k] is >= 'A' and <= 'Z';
            }

            if (allCaps)
            {
                Array.Fill(spell, true, start, length);
            }
        }
    }

    private static bool IsNumberSeparator(char c) => c is '.' or ',' or ':' or '/' or '-' or '٫' or '٬';

    private static void AssignNumbers(string text, Cls[] cls, Lang[] sentenceLang, Lang[] lang, bool[] assigned)
    {
        int n = text.Length;
        int i = 0;
        while (i < n)
        {
            if (cls[i] != Cls.Digit)
            {
                i++;
                continue;
            }

            int start = i++;
            while (i < n)
            {
                if (cls[i] == Cls.Digit)
                {
                    i++;
                }
                else if (IsNumberSeparator(text[i]) && i + 1 < n && cls[i + 1] == Cls.Digit)
                {
                    i += 2;
                }
                else
                {
                    break;
                }
            }

            int end = i;
            Lang l;
            if (start > 0 && cls[start - 1] is Cls.Arabic or Cls.Latin)
            {
                l = lang[start - 1];
            }
            else if (end < n && cls[end] is Cls.Arabic or Cls.Latin)
            {
                l = lang[end];
            }
            else
            {
                l = sentenceLang[start];
            }

            Array.Fill(lang, l, start, end - start);
            Array.Fill(assigned, true, start, end - start);
        }
    }

    private static void AssignNeutrals(Lang[] lang, bool[] assigned, bool[] spell)
    {
        int n = lang.Length;
        int i = 0;
        while (i < n)
        {
            if (assigned[i])
            {
                i++;
                continue;
            }

            int start = i;
            while (i < n && !assigned[i])
            {
                i++;
            }

            int end = i;
            bool hasLeft = start > 0, hasRight = end < n;
            Lang l;
            if (!hasLeft)
            {
                l = lang[end];
            }
            else if (!hasRight)
            {
                l = lang[start - 1];
            }
            else if (lang[start - 1] == lang[end] || spell[start - 1])
            {
                l = lang[end];
            }
            else
            {
                l = lang[start - 1];
            }

            Array.Fill(lang, l, start, end - start);
        }
    }

    private static List<TextRun> BuildRuns(string text, Lang[] lang, bool[] spell)
    {
        var runs = new List<TextRun>();
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && lang[i] == lang[start] && spell[i] == spell[start])
            {
                continue;
            }

            runs.Add(new TextRun(text[start..i], lang[start], spell[start]));
            start = i;
        }

        return runs;
    }
}
