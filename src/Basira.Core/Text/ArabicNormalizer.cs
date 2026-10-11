using System.Globalization;
using System.Text;

namespace Basira.Core.Text;

/// <summary>
/// Repairs Arabic text extracted from PDFs and other documents: presentation-form glyphs and
/// lines stored in visual (reversed) order.
/// </summary>
public static class ArabicNormalizer
{
    /// <summary>
    /// Very common words. Words whose reversal is also a common word (كل/لك, هل/له, مع/عم, ما/ام) are left out.
    /// </summary>
    private static readonly HashSet<string> FunctionWords = new(StringComparer.Ordinal)
    {
        "في", "من", "على", "إلى", "الى", "عن", "أن", "إن", "هذا", "هذه", "التي", "الذي", "الذين", "كان", "كانت",
        "لكن", "بعد", "قبل", "عند", "بين", "لن", "هو", "هي", "أو", "ثم", "حتى", "لا", "أنا", "نحن", "أنت", "منذ",
        "خلال", "ذلك", "تلك", "الله", "يكون", "لقد", "عليه", "عليكم", "أيضا", "جدا", "إذا", "يا", "كيف", "تم", "لم",
        "قد", "هناك", "هنا", "الآن", "اليوم", "غير", "بعض", "مثل", "حول", "نعم", "شكرا", "يجب", "يمكن", "أين", "متى",
        "لماذا", "ماذا", "عندما", "كما", "أكثر", "أي",
    };

    /// <summary>Hamza-alef letters: they often start an Arabic word and rarely end one.</summary>
    private const string TypicalFirst = "أإآ";

    /// <summary>Teh marbuta, alef maksura and hamza: they end words and never start them.</summary>
    private const string FinalOnly = "ةىء";

    private static readonly string[] ArticlePrefixes = ["ال", "وال", "بال", "فال", "كال", "لل"];

    private static readonly string[] PluralSuffixes = ["ات", "ون", "ين"];

    /// <summary>Applies NFKC to Arabic presentation forms only (U+FB50–FDFF, U+FE70–FEFF), including lam-alef ligatures.</summary>
    public static string NormalizePresentationForms(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int first = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (ArabicPresentationForms.Lookup(text[i]) is not null)
            {
                first = i;
                break;
            }
        }

        if (first < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 8);
        sb.Append(text, 0, first);
        for (int i = first; i < text.Length; i++)
        {
            string? mapped = ArabicPresentationForms.Lookup(text[i]);
            if (mapped is null)
            {
                sb.Append(text[i]);
            }
            else
            {
                sb.Append(mapped);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Heuristic: true when a line's Arabic words look stored in reverse character order, as text extracted from
    /// some PDFs is. Each word is scored as written and reversed (common words, the "ال" article, letters that
    /// typically start or end words, impossible starts such as ة and ى); the line counts as reversed when it has at
    /// least three Arabic words and the reversed reading wins clearly.
    /// </summary>
    public static bool LooksReversed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int words = 0, reversed = 0, normal = 0;
        foreach (string raw in ArabicWords(line))
        {
            string word = NormalizePresentationForms(raw);
            words++;
            int difference = Plausibility(Reverse(word)) - Plausibility(word);
            if (difference > 0)
            {
                reversed += difference;
            }
            else
            {
                normal -= difference;
            }
        }

        return words >= 3 && reversed >= 3 && 2 * reversed > 3 * normal;
    }

    /// <summary>
    /// Reverses a visually ordered line into logical order, keeping embedded left-to-right runs
    /// (Latin words, numbers, and the spaces and punctuation between them) in their original order.
    /// Brackets in the reversed parts are mirrored.
    /// </summary>
    /// <remarks>
    /// Call this before <see cref="NormalizePresentationForms"/>: a lam-alef ligature is one character here and
    /// keeps its letters in the right order, whereas its normalized "لا" would come out reversed.
    /// </remarks>
    public static string FixReversedLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length < 2)
        {
            return line;
        }

        var segments = new List<(string Text, bool Ltr)>();
        int i = 0;
        while (i < line.Length)
        {
            if (IsLtrChar(line[i]))
            {
                int end = i + 1;
                int lastLtr = i;
                while (end < line.Length && !ScriptInfo.IsArabicStrong(line[end]))
                {
                    if (IsLtrChar(line[end]))
                    {
                        lastLtr = end;
                    }

                    end++;
                }

                segments.Add((line[i..(lastLtr + 1)], true));
                i = lastLtr + 1;
            }
            else
            {
                int start = i;
                while (i < line.Length && !IsLtrChar(line[i]))
                {
                    i++;
                }

                segments.Add((line[start..i], false));
            }
        }

        var sb = new StringBuilder(line.Length);
        for (int s = segments.Count - 1; s >= 0; s--)
        {
            var (text, ltr) = segments[s];
            if (ltr)
            {
                sb.Append(text);
            }
            else
            {
                AppendReversed(sb, text);
            }
        }

        return sb.ToString();
    }

    private static bool IsLtrChar(char c) => char.IsDigit(c) || ScriptInfo.IsNonArabicLetter(c);

    /// <summary>Reverses by text element so a letter keeps its diacritics and surrogate pairs stay intact.</summary>
    private static void AppendReversed(StringBuilder sb, string text)
    {
        var elements = new List<string>();
        TextElementEnumerator e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            elements.Add(e.GetTextElement());
        }

        for (int k = elements.Count - 1; k >= 0; k--)
        {
            string element = elements[k];
            sb.Append(element.Length == 1 ? Mirror(element[0]) : element);
        }
    }

    private static char Mirror(char c) => c switch
    {
        '(' => ')',
        ')' => '(',
        '[' => ']',
        ']' => '[',
        '{' => '}',
        '}' => '{',
        '<' => '>',
        '>' => '<',
        '«' => '»',
        '»' => '«',
        _ => c,
    };

    /// <summary>Arabic words of a line with diacritics, tatweel and surrounding punctuation removed.</summary>
    private static IEnumerable<string> ArabicWords(string line)
    {
        var sb = new StringBuilder();
        foreach (char c in line)
        {
            if (ScriptInfo.IsArabicLetter(c) && c != 'ـ')
            {
                sb.Append(c);
            }
            else if (ScriptInfo.IsArabicStrong(c) || c == 'ـ')
            {
                continue; // diacritics and tatweel do not break words
            }
            else if (sb.Length > 0)
            {
                if (sb.Length >= 2)
                {
                    yield return sb.ToString();
                }

                sb.Clear();
            }
        }

        if (sb.Length >= 2)
        {
            yield return sb.ToString();
        }
    }

    /// <summary>How much a word (diacritics removed) looks like Arabic written in logical order.</summary>
    internal static int Plausibility(string w)
    {
        int score = 0;
        if (FunctionWords.Contains(w))
        {
            score += 3;
        }

        // Words ending in "لا" (قليلا، أهلا) are common, so a reversed-looking article is weaker evidence on short words.
        if (w.Length >= 5 && StartsWithAny(w, ArticlePrefixes))
        {
            score += 1;
        }

        if (w.Length >= 4 && EndsWithAny(w, PluralSuffixes))
        {
            score += 1;
        }

        char first = w[0], last = w[^1];
        if (FinalOnly.Contains(first))
        {
            score -= 3;
        }
        else if (TypicalFirst.Contains(first))
        {
            score += 1;
        }

        if (last is 'إ' or 'آ')
        {
            score -= 3;
        }
        else if (FinalOnly.Contains(last))
        {
            score += 1;
        }

        return score;
    }

    private static bool StartsWithAny(string w, string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (w.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndsWithAny(string w, string[] suffixes)
    {
        foreach (string suffix in suffixes)
        {
            if (w.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Reverse(string word)
    {
        char[] chars = word.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }
}
