using Mubsir.Core.Context;

namespace Mubsir.Core.Web;

public sealed record SearchResult(int Number, string Title, string? Site);

/// <summary>Picks the real results out of a search page's links, so they can be read as a short numbered list.</summary>
public static class SearchResults
{
    public const int MaxResults = 5;
    private const int MinTitle = 12;
    private const int MaxTitle = 110;

    private static readonly string[] AdWords = ["sponsored", "إعلان", "ممول", "دعائي"];

    private static readonly string[] PageWords =
    [
        "sign in", "تسجيل الدخول", "settings", "الإعدادات", "privacy", "الخصوصية", "terms", "الأحكام", "feedback", "skip to",
        "accessibility", "إمكانية الوصول", "next page", "الصفحة التالية", "learn more", "لمزيد من المعلومات", "more results", "نتائج أخرى",
        "people also", "related searches", "عمليات بحث", "cached", "مُخزَّنة", "translate this page", "ترجمة هذه الصفحة",
    ];

    public static IReadOnlyList<SearchResult> Pick(IEnumerable<UiNode> nodes, int max = MaxResults)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var found = new List<SearchResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UiNode node in nodes)
        {
            if (found.Count >= max)
            {
                break;
            }

            if (node.Role != "link" || node.IsOffscreen || Clean(node.Name) is not { Length: >= MinTitle } name)
            {
                continue;
            }

            string? site = SiteOf(node.Value);
            if (site is null || IsSearchEngine(site) || Has(name, AdWords) || Has(name, PageWords))
            {
                continue;
            }

            string title = name.Length > MaxTitle ? name[..MaxTitle].TrimEnd() : name;
            if (seen.Add(title))
            {
                found.Add(new SearchResult(found.Count + 1, title, site));
            }
        }

        return found;
    }

    /// <summary>What the AI gets back. It says this list, then the person picks a number.</summary>
    public static string Text(IReadOnlyList<SearchResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return string.Join('\n', results.Select(r => r.Site is null ? $"{r.Number}. {r.Title}" : $"{r.Number}. {r.Title} ({r.Site})"));
    }

    private static string? Clean(string? name) => name is null ? null : string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? SiteOf(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    private static bool IsSearchEngine(string? site) =>
        site is not null && (site.StartsWith("google.", StringComparison.OrdinalIgnoreCase)
            || site.Contains(".google.", StringComparison.OrdinalIgnoreCase)
            || site.EndsWith(".gstatic.com", StringComparison.OrdinalIgnoreCase));

    private static bool Has(string text, string[] words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
}
