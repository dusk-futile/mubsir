namespace Mubsir.Core.Context;

public static class TokenEstimator
{
    private const int Unit = 60;
    private const int LatinCost = 15;
    private const int ArabicCost = 24;
    private const int SymbolCost = 20;
    private const int OtherCost = 60;

    public static int Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        long total = 0;
        foreach (var c in text)
        {
            total += Cost(c);
        }

        return (int)((total + Unit - 1) / Unit);
    }

    internal static int PrefixLengthWithin(string text, int maxTokens)
    {
        if (maxTokens <= 0 || string.IsNullOrEmpty(text))
        {
            return 0;
        }

        long budget = (long)maxTokens * Unit;
        long total = 0;
        for (int i = 0; i < text.Length; i++)
        {
            total += Cost(text[i]);
            if (total > budget)
            {
                return i > 0 && char.IsHighSurrogate(text[i - 1]) ? i - 1 : i;
            }
        }

        return text.Length;
    }

    internal static int Cost(char c)
    {
        if (c < 0x80)
        {
            if (char.IsAsciiLetter(c) || char.IsWhiteSpace(c))
            {
                return LatinCost;
            }

            return SymbolCost;
        }

        if (IsArabicLetterOrMark(c))
        {
            return ArabicCost;
        }

        if (char.IsLowSurrogate(c))
        {
            return 0;
        }

        if (char.IsHighSurrogate(c))
        {
            return OtherCost;
        }

        if (char.IsDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c))
        {
            return SymbolCost;
        }

        if (char.IsWhiteSpace(c))
        {
            return LatinCost;
        }

        if (char.IsLetter(c))
        {
            return c < 0x0250 ? LatinCost : ArabicCost;
        }

        return OtherCost;
    }

    private static bool IsArabicLetterOrMark(char c) =>
        c switch
        {
            >= '٠' and <= '٩' => false,
            >= '۰' and <= '۹' => false,
            '،' or '؛' or '؟' or '٪' or '٫' or '٬' or '۔' => false,
            >= '؀' and <= 'ۿ' => true,
            >= 'ݐ' and <= 'ݿ' => true,
            >= 'ࢠ' and <= 'ࣿ' => true,
            >= 'ﭐ' and <= '﷿' => true,
            >= 'ﹰ' and <= '﻿' => true,
            _ => false,
        };
}
