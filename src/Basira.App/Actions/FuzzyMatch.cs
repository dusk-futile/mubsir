using Basira.Core.Text;

namespace Basira.App.Actions;

/// <summary>Pure fuzzy matching of spoken names (Arabic or English) against app and window names, via Core <see cref="ArabicFolding"/>.</summary>
internal static class FuzzyMatch
{
    public const double Threshold = 0.6;

    private static readonly string[] ArabicArticles = ["ال"];

    /// <summary>Similarity 0–1 between a spoken query and a candidate name.</summary>
    public static double Score(string query, string candidate)
    {
        var q = Fold(query);
        var c = Fold(candidate);
        if (q.Length == 0 || c.Length == 0)
        {
            return 0;
        }

        if (q == c)
        {
            return 1;
        }

        var qt = Tokens(q);
        var ct = Tokens(c);

        // Every query word appears in the candidate ("word" in "microsoft word").
        if (qt.All(t => ct.Contains(t)))
        {
            return 0.9 * Math.Max(0.75, (double)qt.Count / ct.Count);
        }

        if (ct.All(t => qt.Contains(t)))
        {
            return 0.8;
        }

        if (c.Contains(q, StringComparison.Ordinal) && q.Length >= 3)
        {
            return 0.7;
        }

        // Spelling variants from speech recognition: edit distance over the whole string.
        int distance = Levenshtein(q, c);
        double ratio = 1.0 - (double)distance / Math.Max(q.Length, c.Length);
        double overlap = (double)qt.Intersect(ct).Count() / Math.Max(1, qt.Union(ct).Count());
        return Math.Max(ratio * 0.95, overlap * 0.85);
    }

    /// <summary>The best candidate at or above <paramref name="threshold"/>, or null.</summary>
    public static (T Item, double Score)? Best<T>(string query, IEnumerable<T> items, Func<T, IEnumerable<string>> names, double threshold = Threshold)
    {
        (T Item, double Score)? best = null;
        foreach (var item in items)
        {
            foreach (var name in names(item))
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                double s = Score(query, name);
                if (s >= threshold && (best is null || s > best.Value.Score))
                {
                    best = (item, s);
                }
            }
        }

        return best;
    }

    /// <summary>Folded text with a leading Arabic article dropped from each word ("المفكرة" ~ "مفكرة").</summary>
    internal static string Fold(string text)
    {
        var folded = ArabicFolding.Fold(text ?? "");
        if (folded.Length == 0)
        {
            return folded;
        }

        var words = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            foreach (var article in ArabicArticles)
            {
                if (words[i].Length > article.Length + 2 && words[i].StartsWith(article, StringComparison.Ordinal))
                {
                    words[i] = words[i][article.Length..];
                }
            }
        }

        return string.Join(' ', words);
    }

    private static HashSet<string> Tokens(string folded) =>
        new(folded.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    internal static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }

            (prev, cur) = (cur, prev);
        }

        return prev[b.Length];
    }
}
