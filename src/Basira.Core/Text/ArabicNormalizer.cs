using System.Globalization;
using System.Text;

namespace Basira.Core.Text;

/// <summary>
/// Repairs Arabic text extracted from PDFs and other documents: presentation-form glyphs and
/// lines stored in visual (reversed) order.
/// </summary>
public static class ArabicNormalizer
{
    /// <summary>Very common words; their reversed spellings are not Arabic words (palindromes and look-alikes are left out).</summary>
    private static readonly string[] FunctionWords =
    [
        "في", "من", "على", "إلى", "الى", "عن", "أن", "إن", "هذا", "التي", "الذي", "كان", "كانت", "لكن", "بعد",
        "قبل", "عند", "بين", "لن", "هو", "هي", "أو", "ثم", "حتى", "لا", "أنا", "منذ", "خلال", "ذلك", "تلك",
        "الله", "يكون", "لقد", "عليه", "أيضا", "جدا", "هذه", "إذا",
    ];

    private static readonly HashSet<string> NormalWords = new(FunctionWords, StringComparer.Ordinal);

    private static readonly HashSet<string> ReversedWords = BuildReversed();

    private static readonly string[] ArticlePrefixes = ["ال", "وال", "بال", "فال", "كال", "لل"];

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
    /// Heuristic: true when a line's Arabic words look stored in reverse character order
    /// (words ending in "لا" from a reversed "ال", words starting with ة or ى, reversed common words).
    /// Needs at least three Arabic words and clearly more reversed than normal evidence.
    /// </summary>
    public static bool LooksReversed(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        int words = 0, reversed = 0, normal = 0;
        foreach (string word in ArabicWords(line))
        {
            words++;
            var (r, n) = Score(word);
            reversed += r;
            normal += n;
        }

        return words >= 3 && reversed >= 2 && reversed > 2 * normal;
    }

    /// <summary>
    /// Reverses a visually ordered line into logical order, keeping embedded left-to-right runs
    /// (Latin words, numbers, and the spaces and punctuation between them) in their original order.
    /// Brackets in the reversed parts are mirrored.
    /// </summary>
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

    /// <summary>Evidence that a word is reversed or normal; strong signs count 2, weak signs 1.</summary>
    private static (int Reversed, int Normal) Score(string w)
    {
        int r = 0, n = 0;
        if (NormalWords.Contains(w))
        {
            n += 2;
        }

        if (ReversedWords.Contains(w))
        {
            r += 2;
        }

        if (w.Length >= 3)
        {
            foreach (string prefix in ArticlePrefixes)
            {
                if (w.StartsWith(prefix, StringComparison.Ordinal))
                {
                    n += 2;
                    break;
                }
            }

            if (w.EndsWith("لا", StringComparison.Ordinal))
            {
                r += 1;
            }
        }

        if (w[0] is 'ة' or 'ى' or 'ء')
        {
            r += 2;
        }

        if (w[^1] is 'إ')
        {
            r += 2;
        }

        if (w[^1] is 'ة' or 'ى')
        {
            n += 2;
        }

        if (w[0] is 'أ' or 'إ')
        {
            n += 1;
        }

        if (w[^1] is 'أ')
        {
            r += 1;
        }

        return (r, n);
    }

    private static HashSet<string> BuildReversed()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string word in FunctionWords)
        {
            char[] chars = word.ToCharArray();
            Array.Reverse(chars);
            string reversed = new(chars);
            if (reversed != word)
            {
                set.Add(reversed);
            }
        }

        return set;
    }
}
