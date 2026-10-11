using System.Globalization;
using System.Text;

namespace Mubsir.Core.Text;

public static class ArabicNormalizer
{
    private static readonly HashSet<string> FunctionWords = new(StringComparer.Ordinal)
    {
        "في", "من", "على", "إلى", "الى", "عن", "أن", "إن", "هذا", "هذه", "التي", "الذي", "الذين", "كان", "كانت",
        "لكن", "بعد", "قبل", "عند", "بين", "لن", "هو", "هي", "أو", "ثم", "حتى", "لا", "أنا", "نحن", "أنت", "منذ",
        "خلال", "ذلك", "تلك", "الله", "يكون", "لقد", "عليه", "عليكم", "أيضا", "جدا", "إذا", "يا", "كيف", "تم", "لم",
        "قد", "هناك", "هنا", "الآن", "اليوم", "غير", "بعض", "مثل", "حول", "نعم", "شكرا", "يجب", "يمكن", "أين", "متى",
        "لماذا", "ماذا", "عندما", "كما", "أكثر", "أي",
    };

    private const string TypicalFirst = "أإآ";

    private const string FinalOnly = "ةىء";

    private static readonly string[] ArticlePrefixes = ["ال", "وال", "بال", "فال", "كال", "لل"];

    private static readonly string[] PluralSuffixes = ["ات", "ون", "ين"];

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
                continue;
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

    internal static int Plausibility(string w)
    {
        int score = 0;
        if (FunctionWords.Contains(w))
        {
            score += 3;
        }

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
