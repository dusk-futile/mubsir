using System.Text;

namespace Basira.Core.Policy;

/// <summary>
/// Light, self-contained text folding for policy matching: lowercase, ASCII digits, no Arabic diacritics or tatweel,
/// unified alef/yaa/taa-marbuta/hamza seats. Deliberately independent of the other builders' normalizers.
/// </summary>
internal static class PolicyText
{
    // Longest first; one-letter clitics need a longer remainder so short words are not mangled.
    private static readonly string[] LongPrefixes = ["وال", "بال", "فال", "كال", "لل", "ال"];
    private static readonly string[] ShortPrefixes = ["و", "ف", "ب", "ل"];

    public static char NormalizeDigit(char c) => c switch
    {
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        _ => c,
    };

    /// <summary>Maps Arabic-Indic digits to ASCII. The result has the same length, so indexes line up.</summary>
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

    /// <summary>Diacritics, tatweel, soft hyphen and invisible direction/joiner marks.</summary>
    public static bool IsIgnorable(char c) =>
        c is (>= 'ً' and <= 'ٟ') or 'ٰ' or 'ـ' or '­' or '﻿'
            or (>= '​' and <= '‏') or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');

    public static char FoldChar(char c) => c switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', // أ إ آ ٱ -> ا
        'ى' or 'ی' => 'ي',                         // ى ی -> ي
        'ة' => 'ه',                                     // ة -> ه
        'ؤ' => 'و',                                     // ؤ -> و
        'ئ' => 'ي',                                     // ئ -> ي
        'ک' => 'ك',                                     // ک -> ك
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

    /// <summary>Folded text with single spaces, for phrase and substring matching.</summary>
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

    /// <summary>Folded words. Splits on anything that is not a letter or digit, and on camelCase ("btnSend" -> btn, send).</summary>
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

    /// <summary>Only letters and digits, folded: "Sign-in options" -> "signinoptions".</summary>
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

    /// <summary>The token plus forms with a leading Arabic article or clitic removed ("الإرسال" -> "ارسال").</summary>
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
