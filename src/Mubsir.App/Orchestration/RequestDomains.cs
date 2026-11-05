using System.Text.RegularExpressions;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal static partial class RequestDomains
{
    private static readonly (string[] Names, string Domain)[] Aliases =
    [
        (["جوجل", "غوغل", "قوقل", "جوجول", "google"], "google.com"),
        (["يوتيوب", "يوتوب", "اليوتيوب", "youtube"], "youtube.com"),
        (["واتساب", "واتس اب", "واتس", "الواتس", "الواتساب", "whatsapp"], "web.whatsapp.com"),
        (["فيسبوك", "فيس بوك", "الفيس", "الفيسبوك", "facebook"], "facebook.com"),
        (["جيميل", "جي ميل", "الجيميل", "gmail"], "mail.google.com"),
        (["ويكيبيديا", "ويكيبيديه", "wikipedia"], "wikipedia.org"),
    ];

    private static readonly (string[] Names, string Domain)[] FoldedAliases =
        Aliases.Select(a => (a.Names.Select(ArabicFolding.Fold).ToArray(), a.Domain)).ToArray();

    public static IReadOnlySet<string> Extract(string? text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        foreach (Match m in DomainToken().Matches(text))
        {
            string host = m.Value.Trim('.').ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
            {
                host = host[4..];
            }

            if (host.Contains('.', StringComparison.Ordinal) && !LooksLikeNumber(host))
            {
                result.Add(host);
            }
        }

        string folded = " " + ArabicFolding.Fold(text) + " ";
        foreach (var (names, domain) in FoldedAliases)
        {
            foreach (string name in names)
            {
                if (name.Length > 0 && ContainsWord(folded, name))
                {
                    result.Add(domain);
                    break;
                }
            }
        }

        return result;
    }

    private static bool ContainsWord(string folded, string name)
    {
        int at = 0;
        while ((at = folded.IndexOf(name, at, StringComparison.Ordinal)) >= 0)
        {
            int end = at + name.Length;
            bool rightOk = end >= folded.Length || folded[end] == ' ';
            char before = at > 0 ? folded[at - 1] : ' ';
            bool leftOk = before == ' '
                || (IsArabic(name[0]) && before is 'و' or 'ب' or 'ل' or 'ف' && (at < 2 || folded[at - 2] == ' '));
            if (leftOk && rightOk)
            {
                return true;
            }

            at = end;
        }

        return false;
    }

    private static bool IsArabic(char c) => c is >= '؀' and <= 'ۿ';

    private static bool LooksLikeNumber(string host) => host.All(c => char.IsAsciiDigit(c) || c == '.');

    [GeneratedRegex(@"(?<![\w@./-])(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,24}(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainToken();
}
