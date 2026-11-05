using System.Text;
using Mubsir.Core.Text;

namespace Mubsir.Core.Reading;

public sealed record Passage(int Page, string Text);

/// <summary>Finds the few passages of a book that a question is about, on this PC, so only those go to the AI.</summary>
public static class PassageFinder
{
    public const int MaxChars = 8000;
    private const int PassageChars = 900;
    private const int MaxPassages = 5;

    private static readonly HashSet<string> Stop = new(new[]
    {
        "the", "a", "an", "of", "in", "on", "to", "is", "are", "was", "were", "what", "who", "why", "how", "when", "where", "which", "does", "do", "did",
        "this", "that", "it", "and", "or", "for", "with", "about", "book", "page", "says", "say", "mean", "means", "tell", "me", "explain",
        "ايه", "إيه", "يعني", "ما", "ماذا", "هو", "هي", "في", "من", "على", "عن", "الى", "إلى", "هل", "مين", "ليه", "لماذا", "كيف", "ازاي", "الكتاب", "كتاب", "الصفحة", "صفحة", "بيتكلم", "بتتكلم", "اللي", "ده", "دي",
    }.Select(ArabicFolding.Fold), StringComparer.Ordinal);

    /// <summary>True if the book holds any word of the question. If not, or if the question mixes languages, it's
    /// worth asking the AI for keywords, because a question in Arabic can be about an English book.</summary>
    public static bool Matches(IReadOnlyList<string> pages, string question)
    {
        string[] terms = Terms(question).Distinct(StringComparer.Ordinal).ToArray();
        return terms.Length > 0 && pages.Any(page => Terms(page).Any(w => Array.IndexOf(terms, w) >= 0));
    }

    public static IReadOnlyList<Passage> Find(IReadOnlyList<string> pages, string question, int currentPage)
    {
        string[] terms = Terms(question).Distinct(StringComparer.Ordinal).ToArray();
        var chunks = new List<(Passage Passage, string[] Words)>();
        for (int i = 0; i < pages.Count; i++)
        {
            foreach (string chunk in Split(pages[i]))
            {
                chunks.Add((new Passage(i + 1, chunk), Terms(chunk).ToArray()));
            }
        }

        var scored = new List<(int Index, double Score)>();
        if (terms.Length > 0)
        {
            Dictionary<string, int> found = terms.ToDictionary(t => t, t => chunks.Count(c => Array.IndexOf(c.Words, t) >= 0), StringComparer.Ordinal);
            for (int i = 0; i < chunks.Count; i++)
            {
                double score = 0;
                foreach (string term in terms)
                {
                    int count = chunks[i].Words.Count(w => w == term);
                    if (count > 0)
                    {
                        score += (1 + Math.Log(count)) * Math.Log(1 + (chunks.Count / (double)found[term]));
                    }
                }

                if (score > 0)
                {
                    // A passage near where they're reading wins a tie.
                    scored.Add((i, score + (Math.Abs(chunks[i].Passage.Page - currentPage) <= 1 ? 0.01 : 0)));
                }
            }
        }

        IEnumerable<Passage> picked = scored.Count > 0
            ? scored.OrderByDescending(s => s.Score).Take(MaxPassages).Select(s => s.Index).Order().Select(i => chunks[i].Passage)
            : Fallback(chunks.Select(c => c.Passage).ToList(), currentPage);

        var result = new List<Passage>();
        int used = 0;
        foreach (Passage passage in picked)
        {
            if (used + passage.Text.Length > MaxChars)
            {
                break;
            }

            result.Add(passage);
            used += passage.Text.Length;
        }

        return result;
    }

    /// <summary>A question with nothing to search for, like "what's this about?": where they are, then the opening.</summary>
    private static IEnumerable<Passage> Fallback(List<Passage> all, int currentPage) =>
        all.Where(p => p.Page == currentPage).Concat(all.Where(p => p.Page != currentPage && p.Page <= 2)).Take(MaxPassages);

    private static IEnumerable<string> Split(string page)
    {
        var current = new StringBuilder();
        foreach (string paragraph in page.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.Length > 0 && current.Length + paragraph.Length > PassageChars)
            {
                yield return current.ToString();
                current.Clear();
            }

            string rest = paragraph;
            while (rest.Length > PassageChars)
            {
                int cut = rest.LastIndexOf(' ', PassageChars);
                cut = cut < PassageChars / 2 ? PassageChars : cut;
                yield return rest[..cut];
                rest = rest[cut..].TrimStart();
            }

            current.Append(current.Length > 0 ? " " : string.Empty).Append(rest);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static IEnumerable<string> Terms(string text)
    {
        foreach (string word in ArabicFolding.Fold(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Stop.Contains(word) || word.Length < 2)
            {
                continue;
            }

            yield return Root(word);
        }
    }

    /// <summary>A light trim, so "the allophones" and "allophone", or "الكتب" and "كتب", still meet.</summary>
    private static string Root(string word)
    {
        if (word.Length > 4 && word.StartsWith(ArabicFolding.Fold("ال"), StringComparison.Ordinal))
        {
            return word[2..];
        }

        if (word.Length > 4 && word.EndsWith("es", StringComparison.Ordinal))
        {
            return word[..^2];
        }

        return word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;
    }
}

/// <summary>Checks the AI's quotes against the book, so an invented quote never reaches the reader as fact.</summary>
public static class QuoteChecker
{
    /// <summary>Quotes are written in «these marks». Any that aren't in the passages are taken out.</summary>
    public static (string Answer, int Verified, int Removed) Check(string answer, IReadOnlyList<Passage> passages)
    {
        string source = Flat(string.Concat(passages.Select(p => p.Text)));
        var result = new StringBuilder();
        int verified = 0;
        int removed = 0;
        int at = 0;
        while (at < answer.Length)
        {
            int open = answer.IndexOf('«', at);
            int close = open < 0 ? -1 : answer.IndexOf('»', open);
            if (close < 0)
            {
                result.Append(answer, at, answer.Length - at);
                break;
            }

            result.Append(answer, at, open - at);
            string quote = answer[(open + 1)..close];
            if (Flat(quote).Length > 0 && source.Contains(Flat(quote), StringComparison.Ordinal))
            {
                verified++;
                result.Append(quote);
            }
            else
            {
                removed++;
            }

            at = close + 1;
        }

        return (result.ToString().Trim(), verified, removed);
    }

    private static string Flat(string text) => ArabicFolding.Fold(text).Replace(" ", string.Empty, StringComparison.Ordinal);
}
