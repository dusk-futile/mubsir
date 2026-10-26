using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

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

        bool[] isProtected = MarkUrlsAndEmails(text);
        bool[] acronym = FindAcronyms(text, cls, isProtected);
        bool[] protectedOrAcronym = new bool[n];
        for (int i = 0; i < n; i++)
        {
            protectedOrAcronym[i] = isProtected[i] || acronym[i];
        }

        Lang[] sentenceLang = SentenceDominance(text, cls, isProtected, defaultLang);
        Lang[] acronymContext = SentenceDominance(text, cls, protectedOrAcronym, defaultLang);

        var lang = new Lang[n];
        var assigned = new bool[n];
        var spell = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (isProtected[i])
            {
                lang[i] = Lang.English;
                assigned[i] = true;
            }
            else if (cls[i] == Cls.Arabic)
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

        for (int i = 0; i < n; i++)
        {
            spell[i] = acronym[i] && acronymContext[i] == Lang.Arabic;
        }

        AssignNumbers(text, cls, sentenceLang, lang, assigned);
        AssignNeutrals(lang, assigned, spell);
        return BuildRuns(text, lang, spell);
    }

    public static Lang DominantLanguage(string text, Lang defaultLang)
    {
        ArgumentNullException.ThrowIfNull(text);
        var (arabic, latin) = CountLetters(text, Classify(text), 0, text.Length, MarkUrlsAndEmails(text));
        return ScriptInfo.Dominant(arabic, latin, defaultLang);
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

    private static (int Arabic, int Latin) CountLetters(string text, Cls[] cls, int start, int end, bool[]? exclude = null)
    {
        int arabic = 0, latin = 0;
        for (int i = start; i < end; i++)
        {
            if (!char.IsLetter(text[i]) || (exclude is not null && exclude[i]))
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

    private static Lang[] SentenceDominance(string text, Cls[] cls, bool[]? exclude, Lang defaultLang)
    {
        int n = text.Length;
        var (arabicTotal, latinTotal) = CountLetters(text, cls, 0, n, exclude);
        Lang textDominant = ScriptInfo.Dominant(arabicTotal, latinTotal, defaultLang);
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

            var (arabic, latin) = CountLetters(text, cls, start, i + 1, exclude);
            Lang d = arabic + latin == 0 ? textDominant : ScriptInfo.Dominant(arabic, latin, defaultLang);
            Array.Fill(result, d, start, i + 1 - start);
            start = i + 1;
        }

        return result;
    }

    private static bool IsSentenceTerminator(char c) => c is '.' or '!' or '?' or '؟' or '…' or '۔' or '！' or '？';

    private static bool[] FindAcronyms(string text, Cls[] cls, bool[] isProtected)
    {
        int n = text.Length;
        var acronym = new bool[n];
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
            if (length is < 2 or > 5 || isProtected[start])
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
                Array.Fill(acronym, true, start, length);
            }
        }

        return acronym;
    }

    private static bool[] MarkUrlsAndEmails(string text)
    {
        int n = text.Length;
        var marks = new bool[n];
        int i = 0;
        while (i < n)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            int start = i;
            while (i < n && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            int s = start, e = i;
            while (s < e && text[s] is '(' or '[' or '{' or '<' or '"' or '\'' or '«' or '“')
            {
                s++;
            }

            while (e > s && text[e - 1] is '.' or ',' or ';' or ':' or '!' or '?' or '؟' or '،' or '؛' or ')' or ']' or '}'
                or '>' or '"' or '\'' or '»' or '”' or '…')
            {
                e--;
            }

            if (e - s >= 4 && IsUrlOrEmail(text.AsSpan(s, e - s)))
            {
                Array.Fill(marks, true, s, e - s);
            }
        }

        return marks;
    }

    private static bool IsUrlOrEmail(ReadOnlySpan<char> token)
    {
        foreach (char c in token)
        {
            if (ScriptInfo.IsArabicScript(c))
            {
                return false;
            }
        }

        if (token.Contains("://", StringComparison.Ordinal) || token.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int at = token.IndexOf('@');
        if (at <= 0 || at != token.LastIndexOf('@') || !char.IsLetterOrDigit(token[at - 1]))
        {
            return false;
        }

        ReadOnlySpan<char> domain = token[(at + 1)..];
        int dot = domain.IndexOf('.');
        return dot > 0 && dot < domain.Length - 1;
    }

    private static bool IsNumberSeparator(char c) => c is '.' or ',' or ':' or '/' or '-' or '٫' or '٬';

    private static void AssignNumbers(string text, Cls[] cls, Lang[] sentenceLang, Lang[] lang, bool[] assigned)
    {
        int n = text.Length;
        int i = 0;
        while (i < n)
        {
            if (cls[i] != Cls.Digit || assigned[i])
            {
                i++;
                continue;
            }

            int start = i++;
            while (i < n)
            {
                if (cls[i] == Cls.Digit && !assigned[i])
                {
                    i++;
                }
                else if (IsNumberSeparator(text[i]) && i + 1 < n && cls[i + 1] == Cls.Digit && !assigned[i + 1])
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
