using System.Globalization;
using System.Text;

namespace Mubsir.Core.Text;

public static class ArabicFolding
{
    public static string Fold(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (HasCompatibilityForms(text))
        {
            text = NormalizeCompatibility(text);
        }

        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int cp = rune.Value;
            if (IsDropped(rune, cp))
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rune) || IsSeparator(rune))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            int mapped = Map(cp);
            if (mapped != cp)
            {
                sb.Append((char)mapped);
            }
            else if (rune.IsBmp)
            {
                sb.Append((char)Rune.ToLowerInvariant(rune).Value);
            }
            else
            {
                sb.Append(Rune.ToLowerInvariant(rune).ToString());
            }
        }

        return sb.ToString();
    }

    private static int Map(int cp) => cp switch
    {
        0x0622 or 0x0623 or 0x0625 or 0x0671 or 0x0672 or 0x0673 => 0x0627,
        0x0629 => 0x0647,
        0x0649 => 0x064A,
        0x0624 => 0x0648,
        0x0626 => 0x064A,
        0x06CC => 0x064A,
        0x06A9 => 0x0643,
        >= 0x0660 and <= 0x0669 => '0' + (cp - 0x0660),
        >= 0x06F0 and <= 0x06F9 => '0' + (cp - 0x06F0),
        _ => cp,
    };

    private static bool IsDropped(Rune rune, int cp)
    {
        if (cp == 0x0640)
        {
            return true;
        }

        if (cp is '\'' or '`' or 0x00B4 or 0x02BC or 0x2018 or 0x2019)
        {
            return true;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);

        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            || (category == UnicodeCategory.Control && !Rune.IsWhiteSpace(rune));
    }

    private static bool IsSeparator(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation
            or UnicodeCategory.MathSymbol
            or UnicodeCategory.CurrencySymbol
            or UnicodeCategory.ModifierSymbol
            or UnicodeCategory.OtherSymbol;
    }

    private static bool HasCompatibilityForms(string text)
    {
        foreach (char c in text)
        {
            if (c is (>= 'ﭐ' and <= '﷿') or (>= 'ﹰ' and <= '﻿') or (>= '！' and <= '～'))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCompatibility(string text)
    {
        try
        {
            return text.Normalize(NormalizationForm.FormKC);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException)
        {
            return text;
        }
    }
}
