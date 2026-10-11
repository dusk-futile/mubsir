namespace Basira.Core.Text;

/// <summary>
/// Splits text into sentences (the unit of rewind, repeat and SSML marks) and paragraphs.
/// </summary>
/// <remarks>
/// Sentences end at . ! ? … ؟ and line breaks. The Arabic comma ، and semicolon ؛ do not end sentences.
/// Decimals (3.14), URLs, e-mail addresses, common abbreviations (Dr., e.g., etc.), initials (J. K.) and
/// list numbers ("1. ") do not split. Runs such as "?!" or "..." stay together.
/// </remarks>
public static class SentenceSplitter
{
    /// <summary>Longer sentences are broken at a comma or space so rewind stays useful in unpunctuated text.</summary>
    public const int MaxSentenceLength = 400;

    /// <summary>Abbreviations that never end a sentence when followed by more text.</summary>
    private static readonly HashSet<string> TitleAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "dr", "mr", "mrs", "ms", "prof", "st", "mt", "jr", "sr", "vs", "e.g", "i.e", "eg", "ie", "cf", "viz",
        "fig", "approx", "gen", "col", "capt", "lt", "sgt", "rev", "hon", "dept", "est",
        "د", "أ",
    };

    /// <summary>Abbreviations that end a sentence only when the next word starts with a capital letter or another script.</summary>
    private static readonly HashSet<string> EndCapableAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "etc", "inc", "ltd", "co", "corp", "no", "nos", "vol", "pp", "p", "ed", "eds", "al",
        "jan", "feb", "mar", "apr", "jun", "jul", "aug", "sep", "sept", "oct", "nov", "dec",
        "a.m", "p.m", "u.s", "u.k", "u.n",
    };

    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var pieces = new List<string>();
        int n = text.Length, start = 0, i = 0;
        while (i < n)
        {
            char c = text[i];
            if (ScriptInfo.IsLineBreak(c))
            {
                AddTrimmed(pieces, text, start, i);
                start = ++i;
                continue;
            }

            if (!IsTerminator(c))
            {
                i++;
                continue;
            }

            int runEnd = i;
            while (runEnd < n && IsTerminator(text[runEnd]))
            {
                runEnd++;
            }

            int afterClosers = runEnd;
            while (afterClosers < n && IsCloser(text[afterClosers]))
            {
                afterClosers++;
            }

            if (IsBoundary(text, start, i, runEnd, afterClosers))
            {
                AddTrimmed(pieces, text, start, afterClosers);
                start = afterClosers;
                i = afterClosers;
            }
            else
            {
                i = runEnd;
            }
        }

        AddTrimmed(pieces, text, start, n);

        var result = new List<string>(pieces.Count);
        foreach (string piece in pieces)
        {
            BreakLong(piece, result);
        }

        return result;
    }

    /// <summary>Splits on line breaks (single newlines and blank lines alike), trimming and dropping empty lines.</summary>
    public static IReadOnlyList<string> Paragraphs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<string>();
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || ScriptInfo.IsLineBreak(text[i]))
            {
                AddTrimmed(result, text, start, i);
                start = i + 1;
            }
        }

        return result;
    }

    private static bool IsTerminator(char c) =>
        c is '.' or '!' or '?' or '؟' or '…' or '۔' or '！' or '？' or '‼' or '⁇' or '⁈' or '⁉' or '。';

    private static bool IsStrongTerminator(char c) => c is not ('.' or '…');

    private static bool IsCloser(char c) => c is '"' or '\'' or '”' or '’' or '»' or '›' or ')' or ']' or '}';

    private static bool IsBoundary(string text, int sentenceStart, int runStart, int runEnd, int afterClosers)
    {
        int n = text.Length;
        bool atEnd = afterClosers >= n;
        char next = atEnd ? '\0' : text[afterClosers];
        bool spaceAfter = !atEnd && char.IsWhiteSpace(next);
        char prev = runStart > 0 ? text[runStart - 1] : '\0';

        bool strong = false, ellipsis = false;
        int dots = 0;
        for (int k = runStart; k < runEnd; k++)
        {
            char c = text[k];
            if (c == '.')
            {
                dots++;
            }
            else if (c == '…')
            {
                ellipsis = true;
            }
            else if (IsStrongTerminator(c))
            {
                strong = true;
            }
        }

        ellipsis |= dots >= 2;

        if (strong)
        {
            if (atEnd || spaceAfter)
            {
                return true;
            }

            // "page?id=3" is a URL; "كيف حالك؟أنا بخير" is two sentences typed without a space.
            return text.AsSpan(runStart, runEnd - runStart).Contains('؟')
                || ScriptInfo.IsArabicLetter(prev)
                || ScriptInfo.IsArabicLetter(next);
        }

        if (ellipsis)
        {
            if (atEnd)
            {
                return true;
            }

            if (!spaceAfter)
            {
                return false;
            }

            char following = NextNonSpace(text, afterClosers);
            return !char.IsLower(following);
        }

        // A single period.
        if (!atEnd && !spaceAfter)
        {
            // Decimals, URLs, e-mails and "e.g" stay together; Arabic sentences glued with "." do not.
            return ScriptInfo.IsArabicLetter(prev) && ScriptInfo.IsArabicLetter(next);
        }

        if (text.AsSpan(sentenceStart, runStart - sentenceStart).IsWhiteSpace())
        {
            return false;
        }

        if (atEnd)
        {
            return true;
        }

        string word = WordBefore(text, runStart);
        if (word.Length == 0)
        {
            return true;
        }

        char nextWordStart = NextNonSpace(text, afterClosers);

        if (word.Length <= 3 && IsAllDigits(word) && text.AsSpan(sentenceStart, runStart - sentenceStart).Trim().Length == word.Length)
        {
            return false; // list numbering: "1. Buy milk"
        }

        if (TitleAbbreviations.Contains(word))
        {
            return false;
        }

        if (word.Length == 1 && char.IsUpper(word[0]))
        {
            return false; // initials: "J. K. Rowling"
        }

        if (EndCapableAbbreviations.Contains(word) || IsDottedInitials(word))
        {
            return !(char.IsLower(nextWordStart) || char.IsDigit(nextWordStart));
        }

        return true;
    }

    /// <summary>The token right before a period: letters, digits and inner dots ("e.g", "U.S", "Dr").</summary>
    private static string WordBefore(string text, int periodIndex)
    {
        int i = periodIndex;
        while (i > 0 && (char.IsLetterOrDigit(text[i - 1]) || text[i - 1] == '.'))
        {
            i--;
        }

        return text[i..periodIndex].TrimStart('.');
    }

    private static bool IsDottedInitials(string word)
    {
        // "U.S", "a.m": single letters separated by dots.
        if (word.Length < 3)
        {
            return false;
        }

        for (int i = 0; i < word.Length; i++)
        {
            bool expectLetter = i % 2 == 0;
            if (expectLetter ? !char.IsLetter(word[i]) : word[i] != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllDigits(string s)
    {
        foreach (char c in s)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static char NextNonSpace(string text, int from)
    {
        for (int i = from; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i];
            }
        }

        return '\0';
    }

    private static void AddTrimmed(List<string> list, string text, int start, int end)
    {
        if (end <= start)
        {
            return;
        }

        ReadOnlySpan<char> piece = text.AsSpan(start, end - start).Trim();
        if (!piece.IsEmpty)
        {
            list.Add(piece.ToString());
        }
    }

    private static void BreakLong(string sentence, List<string> result)
    {
        string rest = sentence;
        while (rest.Length > MaxSentenceLength)
        {
            int cut = FindCut(rest);
            AddTrimmed(result, rest, 0, cut);
            rest = rest[cut..].TrimStart();
        }

        if (rest.Length > 0)
        {
            result.Add(rest);
        }
    }

    private static int FindCut(string s)
    {
        int min = MaxSentenceLength / 2;
        for (int i = MaxSentenceLength - 1; i >= min; i--)
        {
            if (s[i] is ',' or '،' or ';' or '؛' or ':' or '—' or '–')
            {
                return i + 1;
            }
        }

        for (int i = MaxSentenceLength - 1; i >= min; i--)
        {
            if (char.IsWhiteSpace(s[i]))
            {
                return i;
            }
        }

        int cut = MaxSentenceLength;
        return char.IsLowSurrogate(s[cut]) ? cut - 1 : cut;
    }
}
