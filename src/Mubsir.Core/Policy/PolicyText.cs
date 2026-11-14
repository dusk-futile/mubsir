using System.Text;

namespace Mubsir.Core.Policy;

internal static class PolicyText
{
    private static readonly string[] LongPrefixes = ["وال", "بال", "فال", "كال", "لل", "ال"];
    private static readonly string[] ShortPrefixes = ["و", "ف", "ب", "ل"];

    public static char NormalizeDigit(char c) => c switch
    {
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        _ => c,
    };

    public static string NormalizeDigits(string s)
    {
        char[]? buffer = null;
        for (int i = 0; i < s.Length; i++)
        {
            var d = NormalizeDigit(s[i]);
            if (d != s[i])
            {
                buffer ??= s.ToCharArray();
                buffer[i] = d;
            }
        }

        return buffer is null ? s : new string(buffer);
    }

    public static bool IsIgnorable(char c) =>
    c is (>= 'ً' and <= 'ٟ') or 'ٰ' or 'ـ' or '­' or '﻿'
        or (>= '​' and <= '‏') or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');

    public static char FoldChar(char c) => c switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
        'ى' or 'ی' => 'ي',
        'ة' => 'ه',
        'ؤ' => 'و',
        'ئ' => 'ي',
        'ک' => 'ك',
        _ => char.ToLowerInvariant(NormalizeDigit(c)),
    };

    public static string Nfkc(string s)
    {
        try
        {
            return s.IsNormalized(NormalizationForm.FormKC) ? s : s.Normalize(NormalizationForm.FormKC);
        }
        catch (Exception e) when (e is ArgumentException or PlatformNotSupportedException)
        {
            return s;
        }
    }

    public static string Fold(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        var t = Nfkc(s);
        var sb = new StringBuilder(t.Length);
        bool space = false;
        foreach (var c in t)
        {
            if (IsIgnorable(c))
            {
                continue;
            }

            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = sb.Length > 0;
                continue;
            }

            if (space)
            {
                sb.Append(' ');
                space = false;
            }

            sb.Append(FoldChar(c));
        }

        return sb.ToString();
    }

    public static List<string> Tokens(string? s)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(s))
        {
            return list;
        }

        var t = Nfkc(s);
        var current = new StringBuilder();
        char prev = '\0';
        foreach (var c in t)
        {
            if (IsIgnorable(c))
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                if (current.Length > 0 && char.IsUpper(c) && char.IsLower(prev))
                {
                    Flush();
                }

                current.Append(FoldChar(c));
                prev = c;
            }
            else
            {
                Flush();
                prev = '\0';
            }
        }

        Flush();
        return list;

        void Flush()
        {
            if (current.Length > 0)
            {
                list.Add(current.ToString());
                current.Clear();
            }
        }
    }

    public static string Compact(string? s)
    {
        var sb = new StringBuilder();
        foreach (var c in Fold(s))
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static IEnumerable<string> Variants(string token)
    {
        yield return token;
        foreach (var p in LongPrefixes)
        {
            if (token.Length - p.Length >= 2 && token.StartsWith(p, StringComparison.Ordinal))
            {
                yield return token[p.Length..];
            }
        }

        foreach (var p in ShortPrefixes)
        {
            if (token.Length - p.Length >= 3 && token.StartsWith(p, StringComparison.Ordinal))
            {
                yield return token[p.Length..];
            }
        }
    }

    public static bool ContainsSequence(IReadOnlyList<string> tokens, IReadOnlyList<string> phrase)
    {
        if (phrase.Count == 0 || phrase.Count > tokens.Count)
        {
            return false;
        }

        for (int i = 0; i + phrase.Count <= tokens.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < phrase.Count && match; j++)
            {
                match = Variants(tokens[i + j]).Contains(phrase[j], StringComparer.Ordinal);
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    public static string ToArabicDigits(string s)
    {
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '0' and <= '9')
            {
                chars[i] = (char)('٠' + (chars[i] - '0'));
            }
        }

        return new string(chars);
    }
}
