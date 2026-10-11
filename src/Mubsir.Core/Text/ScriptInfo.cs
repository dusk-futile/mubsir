using System.Globalization;
using Mubsir.Core.Common;

namespace Mubsir.Core.Text;

internal static class ScriptInfo
{
    public static bool IsArabicScript(char c) =>
    c is (>= '\u0600' and <= '\u06FF')
        or (>= '\u0750' and <= '\u077F')
        or (>= '\u08A0' and <= '\u08FF')
        or (>= '\uFB50' and <= '\uFDFF')
        or (>= '\uFE70' and <= '\uFEFF');

    public static bool IsArabicLetter(char c) => IsArabicScript(c) && char.IsLetter(c);

    public static bool IsArabicStrong(char c)
    {
        if (!IsArabicScript(c) || char.IsDigit(c))
        {
            return false;
        }

        return char.GetUnicodeCategory(c) is UnicodeCategory.OtherLetter or UnicodeCategory.ModifierLetter
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    public static bool IsNonArabicLetter(char c) => char.IsLetter(c) && !IsArabicScript(c);

    public static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    public static bool IsCombiningMark(char c) =>
        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;

    public static bool IsLineBreak(char c) => c is '\n' or '\r' or '\u2028' or '\u2029' or '\u000B' or '\u000C' or '\u0085';

    public static char ToAsciiDigit(char c) => char.IsDigit(c) ? (char)('0' + (int)char.GetNumericValue(c)) : c;

    public static (int Arabic, int Other) CountLetters(ReadOnlySpan<char> text)
    {
        int arabic = 0, other = 0;
        foreach (char c in text)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            if (IsArabicScript(c))
            {
                arabic++;
            }
            else
            {
                other++;
            }
        }

        return (arabic, other);
    }

    public static Lang Dominant(int arabic, int other, Lang defaultLang) =>
    arabic > other ? Lang.Arabic : other > arabic ? Lang.English : defaultLang;
}
