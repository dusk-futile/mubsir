using System.Text;
using Basira.Core.Context;

namespace Basira.App.Actions;

/// <summary>A piece of a document to hand to the model, and where reading continues.</summary>
internal sealed record DocumentChunk(string Text, int FirstPage, int LastPage, int TotalPages, int NextPage, int NextOffset, bool Truncated);

/// <summary>Pure paging of document text under a token budget (via Core <see cref="TokenEstimator"/>).</summary>
internal static class DocumentChunker
{
    public const int ReadTokens = 6_000;
    public const int QuestionTokens = 90_000;

    /// <summary>
    /// Consecutive pages from <paramref name="startPage"/> (1-based) and <paramref name="startOffset"/> until the
    /// budget is used; a single oversized page is cut and continues at <see cref="DocumentChunk.NextOffset"/>.
    /// </summary>
    public static DocumentChunk Read(IReadOnlyList<string> pages, int startPage, int startOffset, int maxTokens, IReadOnlyCollection<int>? unreadPages = null)
    {
        int total = pages.Count;
        if (total == 0)
        {
            return new DocumentChunk("", 0, 0, 0, 1, 0, false);
        }

        int page = Math.Clamp(startPage, 1, total);
        int offset = Math.Max(0, startOffset);
        var sb = new StringBuilder();
        int used = 0;
        int first = page;
        int last = page;
        int nextPage = page;
        int nextOffset = 0;
        while (page <= total)
        {
            var text = pages[page - 1] ?? "";
            if (offset >= text.Length)
            {
                offset = 0;
            }

            var rest = offset > 0 ? text[offset..] : text;
            string header = $"[Page {page} of {total}]";
            string body = rest.Length == 0 && unreadPages?.Contains(page) == true
                ? "(scanned page; its text could not be read yet)"
                : rest;
            int cost = TokenEstimator.Estimate(header) + TokenEstimator.Estimate(body) + 2;
            if (used > 0 && used + cost > maxTokens)
            {
                nextPage = page;
                nextOffset = offset;
                return new DocumentChunk(sb.ToString().TrimEnd(), first, last, total, nextPage, nextOffset, true);
            }

            if (cost > maxTokens)
            {
                // One page alone is over budget: cut it at a whitespace boundary near the budget.
                int chars = CharsForTokens(body, maxTokens - used - TokenEstimator.Estimate(header) - 2);
                sb.Append(header).Append('\n').Append(body.AsSpan(0, chars)).Append("\n\n");
                return new DocumentChunk(sb.ToString().TrimEnd(), first, page, total, page, offset + chars, true);
            }

            sb.Append(header).Append('\n').Append(body).Append("\n\n");
            used += cost;
            last = page;
            page++;
            offset = 0;
            nextPage = page;
        }

        return new DocumentChunk(sb.ToString().TrimEnd(), first, last, total, Math.Min(nextPage, total + 1), nextOffset, false);
    }

    /// <summary>Largest prefix (ending at whitespace when possible) whose estimate fits <paramref name="tokens"/>.</summary>
    internal static int CharsForTokens(string text, int tokens)
    {
        if (tokens <= 0 || text.Length == 0)
        {
            return Math.Min(text.Length, 1);
        }

        int lo = 1;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (TokenEstimator.Estimate(text[..mid]) <= tokens)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        int cut = lo;
        int space = text.LastIndexOfAny([' ', '\n', '\t'], Math.Max(0, cut - 1));
        if (space > cut / 2)
        {
            cut = space + 1;
        }

        return Math.Max(1, cut);
    }
}
