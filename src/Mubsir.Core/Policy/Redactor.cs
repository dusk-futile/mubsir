using System.Text;
using System.Text.RegularExpressions;

namespace Mubsir.Core.Policy;

public static partial class Redactor
{
    public const string CardMarker = "[card]";
    public const string IbanMarker = "[iban]";
    public const string CodeMarker = "[code]";
    public const string PhoneMarker = "[phone]";
    public const string EmailMarker = "[email]";
    public const string IdMarker = "[id]";

    internal const int CodeWindow = 40;

    private const int MaxDigitsConsidered = 19;

    private static readonly string[] ArabicCodeWords =
        ["رمز", "كود", "التحقق", "كلمه المرور", "كلمه السر", "الرقم السري", "رقم سري", "otp"];

    private static readonly string[] CurrencyWords =
    [
        "$", "€", "£", "¥", "egp", "usd", "sar", "aed", "lyd", "jod", "mad", "kwd", "qar", "omr", "bhd", "eur", "gbp",
        "le", "l.e", "pounds", "pound", "dollars", "dollar", "riyal", "riyals", "dirham", "dinar",
        "جنيه", "ج.م", "جم", "ريال", "ر.س", "درهم", "د.ا", "دينار", "د.ل", "دولار", "يورو",
    ];

    [GeneratedRegex(@"(?<![A-Za-z0-9._%+\-])[A-Za-z0-9][A-Za-z0-9._%+\-]*@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,24}(?![A-Za-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-Za-z]{2}[0-9]{2}(?:[ ]?[A-Za-z0-9]){11,30}", RegexOptions.CultureInvariant)]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9])\(?\+?[0-9]+(?:(?:[  ]?[\-‐-–()][  ]?|[  ])[0-9]+)*\+?", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRunRegex();

    [GeneratedRegex(@"\b(?:codes?|otp|pin|pins|passcode|verification|verify|one[\- ]?time|2fa|security\s+code|cvv2?|cvc2?|csc)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CodeWordRegex();

    [GeneratedRegex(@"\b(?:cvv2?|cvc2?|csc|security\s+code)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CvvWordRegex();

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        var spans = FindSpans(text);
        if (spans.Count == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        int pos = 0;
        foreach (var s in spans)
        {
            sb.Append(text, pos, s.Start - pos).Append(s.Label);
            pos = s.End;
        }

        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }

    internal static bool ContainsCardOrIban(string? text) =>
    !string.IsNullOrEmpty(text) && FindSpans(text).Any(s => s.Label is CardMarker or IbanMarker);

    internal static bool ContainsCard(string? text) =>
        !string.IsNullOrEmpty(text) && FindSpans(text).Any(s => s.Label == CardMarker);

    internal readonly record struct Span(int Start, int Length, string Label)
    {
        public int End => Start + Length;

        public bool Overlaps(int start, int end) => start < End && Start < end;
    }

    internal static List<Span> FindSpans(string text)
    {
        var norm = PolicyText.NormalizeDigits(text);
        var spans = new List<Span>();
        foreach (Match m in EmailRegex().Matches(norm))
        {
            TryAdd(spans, new Span(m.Index, m.Length, EmailMarker));
        }

        FindIbans(norm, spans);
        FindDigitRuns(norm, spans);
        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    private static bool TryAdd(List<Span> spans, Span span)
    {
        foreach (var s in spans)
        {
            if (s.Overlaps(span.Start, span.End))
            {
                return false;
            }
        }

        spans.Add(span);
        return true;
    }

    private static void FindIbans(string norm, List<Span> spans)
    {
        foreach (Match m in IbanRegex().Matches(norm))
        {
            var positions = new List<int>(34);
            for (int k = m.Index; k < m.Index + m.Length; k++)
            {
                if (char.IsAsciiLetterOrDigit(norm[k]))
                {
                    positions.Add(k);
                }
            }

            for (int count = Math.Min(positions.Count, 34); count >= 15; count--)
            {
                int end = positions[count - 1] + 1;
                if (end < norm.Length && char.IsAsciiLetterOrDigit(norm[end]))
                {
                    continue;
                }

                if (IsIbanChecksumValid(norm, positions, count))
                {
                    TryAdd(spans, new Span(m.Index, end - m.Index, IbanMarker));
                    break;
                }
            }
        }
    }

    internal static bool IsIbanChecksumValid(string text, List<int> positions, int count)
    {
        int remainder = 0;
        for (int n = 0; n < count; n++)
        {
            char c = char.ToUpperInvariant(text[positions[(n + 4) % count]]);
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                int v = c - 'A' + 10;
                remainder = (remainder * 100 + v) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }

    private static void FindDigitRuns(string norm, List<Span> spans)
    {
        foreach (Match m in DigitRunRegex().Matches(norm))
        {
            int start = m.Index;
            int end = m.Index + m.Length;
            if (IsPartOfLargerNumber(norm, start, end))
            {
                continue;
            }

            var groups = new List<(int Start, int End)>();
            bool leadingPlus = false;
            for (int p = start; p < end;)
            {
                if (char.IsAsciiDigit(norm[p]))
                {
                    int q = p;
                    while (q < end && char.IsAsciiDigit(norm[q]))
                    {
                        q++;
                    }

                    groups.Add((p, q));
                    p = q;
                }
                else
                {
                    if (norm[p] == '+' && groups.Count == 0)
                    {
                        leadingPlus = true;
                    }

                    p++;
                }
            }

            bool trailingPlus = !leadingPlus && norm[end - 1] == '+';
            ClassifyRun(norm, start, end, groups, leadingPlus, trailingPlus, spans);
        }
    }

    private static void ClassifyRun(
        string norm, int runStart, int runEnd, List<(int Start, int End)> groups, bool leadingPlus, bool trailingPlus, List<Span> spans)
    {
        int i = 0;
        while (i < groups.Count)
        {
            int last = i;
            int digits = 0;
            while (last < groups.Count && digits + Len(groups[last]) <= MaxDigitsConsidered + 2)
            {
                digits += Len(groups[last]);
                last++;
            }

            bool found = false;
            for (int j = Math.Max(i, last - 1); j >= i; j--)
            {
                bool plus = (i == 0 && leadingPlus) || (j == groups.Count - 1 && trailingPlus);
                int spanStart = i == 0 && leadingPlus ? runStart : groups[i].Start;
                int spanEnd = j == groups.Count - 1 && trailingPlus ? runEnd : groups[j].End;
                var label = Classify(norm, groups, i, j, plus, spanStart, spanEnd);
                if (label is not null && TryAdd(spans, new Span(spanStart, spanEnd - spanStart, label)))
                {
                    i = j + 1;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                i++;
            }
        }
    }

    private static int Len((int Start, int End) g) => g.End - g.Start;

    private static string? Classify(string norm, List<(int Start, int End)> groups, int i, int j, bool plus, int spanStart, int spanEnd)
    {
        var sb = new StringBuilder(24);
        for (int k = i; k <= j; k++)
        {
            sb.Append(norm, groups[k].Start, Len(groups[k]));
        }

        var d = sb.ToString();
        int n = d.Length;
        int count = j - i + 1;

        if (plus)
        {
            return n is >= 8 and <= 15 ? PhoneMarker : null;
        }

        if (n is >= 10 and <= 17 && d.StartsWith("00", StringComparison.Ordinal))
        {
            return PhoneMarker;
        }

        if (n == 14 && IsEgyptianNationalId(d))
        {
            return IdMarker;
        }

        if (n is >= 13 and <= 19 && d[0] != '0' && CardGroupsOk(norm, groups, i, j) && IsLuhnValid(d))
        {
            return CardMarker;
        }

        if (n is >= 9 and <= 11 && d[0] == '0' && d[1] != '0' && LocalPhoneGroupsOk(groups, i, j))
        {
            return PhoneMarker;
        }

        if (n is >= 3 and <= 8 && count <= 2 && !IsNextToCurrencyOrUnit(norm, spanStart, spanEnd))
        {
            var near = NearCodeWord(norm, spanStart, spanEnd);
            if ((near == CodeContext.Cvv && n <= 4) || (near != CodeContext.None && n >= 4))
            {
                return CodeMarker;
            }
        }

        return null;
    }

    private static bool CardGroupsOk(string norm, List<(int Start, int End)> groups, int i, int j)
    {
        if (i == j)
        {
            return true;
        }

        var lengths = new List<int>(j - i + 1);
        bool allYears = true;
        for (int k = i; k <= j; k++)
        {
            var g = groups[k];
            lengths.Add(Len(g));

            allYears &= Len(g) == 4
                && ((norm[g.Start] == '1' && norm[g.Start + 1] == '9') || (norm[g.Start] == '2' && norm[g.Start + 1] == '0'));
        }

        if (allYears)
        {
            return false;
        }

        bool fours = lengths.Take(lengths.Count - 1).All(l => l == 4) && lengths[^1] is >= 1 and <= 4;
        bool amex = lengths.Count == 3 && lengths[0] == 4 && lengths[1] == 6 && lengths[2] is 4 or 5;
        return fours || amex;
    }

    private static bool LocalPhoneGroupsOk(List<(int Start, int End)> groups, int i, int j)
    {
        if (Len(groups[i]) < 2)
        {
            return false;
        }

        for (int k = i + 1; k <= j; k++)
        {
            if (Len(groups[k]) < 3)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEgyptianNationalId(string d)
    {
        if (d[0] is not ('2' or '3'))
        {
            return false;
        }

        int month = (d[3] - '0') * 10 + (d[4] - '0');
        int day = (d[5] - '0') * 10 + (d[6] - '0');
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }

    internal static bool IsLuhnValid(ReadOnlySpan<char> digits)
    {
        if (digits.Length < 2)
        {
            return false;
        }

        int sum = 0;
        bool dbl = false;
        for (int k = digits.Length - 1; k >= 0; k--)
        {
            int v = digits[k] - '0';
            if (v is < 0 or > 9)
            {
                return false;
            }

            if (dbl)
            {
                v *= 2;
                if (v > 9)
                {
                    v -= 9;
                }
            }

            sum += v;
            dbl = !dbl;
        }

        return sum % 10 == 0;
    }

    private static bool IsPartOfLargerNumber(string s, int start, int end)
    {
        static bool IsJoiner(char c) => c is '.' or ',' or ':' or '/' or '٫' or '٬';
        if (start >= 2 && IsJoiner(s[start - 1]) && char.IsAsciiDigit(s[start - 2]))
        {
            return true;
        }

        return end + 1 < s.Length && IsJoiner(s[end]) && char.IsAsciiDigit(s[end + 1]);
    }

    private static bool IsNextToCurrencyOrUnit(string s, int start, int end)
    {
        var before = PolicyText.Fold(s[Math.Max(0, start - 8)..start]).TrimEnd();
        var after = PolicyText.Fold(s[end..Math.Min(s.Length, end + 8)]).TrimStart();
        if (after.StartsWith('%'))
        {
            return true;
        }

        foreach (var w in CurrencyWords)
        {
            if (before.EndsWith(w, StringComparison.Ordinal) && BoundaryBefore(before, before.Length - w.Length, w))
            {
                return true;
            }

            if (after.StartsWith(w, StringComparison.Ordinal) && BoundaryAfter(after, w.Length, w))
            {
                return true;
            }
        }

        return false;

        static bool BoundaryBefore(string t, int at, string w) =>
            !char.IsLetter(w[0]) || at == 0 || !char.IsLetter(t[at - 1]);

        static bool BoundaryAfter(string t, int at, string w) =>
            !char.IsLetter(w[^1]) || at >= t.Length || !char.IsLetter(t[at]);
    }

    private enum CodeContext
    {
        None,
        Code,
        Cvv,
    }

    private static CodeContext NearCodeWord(string s, int start, int end)
    {
        int ws = Math.Max(0, start - CodeWindow);
        int we = Math.Min(s.Length, end + CodeWindow);
        var window = s[ws..start] + " " + s[end..we];
        if (CvvWordRegex().IsMatch(window))
        {
            return CodeContext.Cvv;
        }

        if (CodeWordRegex().IsMatch(window))
        {
            return CodeContext.Code;
        }

        var folded = PolicyText.Fold(window);
        foreach (var w in ArabicCodeWords)
        {
            if (folded.Contains(w, StringComparison.Ordinal))
            {
                return CodeContext.Code;
            }
        }

        return CodeContext.None;
    }
}
