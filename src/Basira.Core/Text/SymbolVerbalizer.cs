using System.Buffers;
using System.Globalization;
using System.Text;
using Basira.Core.Common;
using static Basira.Core.Text.SymbolTables;

namespace Basira.Core.Text;

/// <summary>How <see cref="SymbolVerbalizer"/> turns symbols into words.</summary>
public enum VerbalizeMode
{
    /// <summary>Only symbols a voice would skip or misread (%, &amp;, @, #, ², ½, °C).</summary>
    Normal,

    /// <summary>Every math token is read ("read it as math").</summary>
    Math,

    /// <summary>Character by character ("spell that").</summary>
    Spell,
}

/// <summary>Turns symbols into words a voice can read, in Arabic or English.</summary>
public static class SymbolVerbalizer
{
    private static readonly SearchValues<char> NormalTriggers =
        SearchValues.Create("@#&%٪²³½¼¾⅓⅔⅕⅛°℃℉");

    public static string Verbalize(string text, Lang lang, VerbalizeMode mode)
    {
        ArgumentNullException.ThrowIfNull(text);
        return mode switch
        {
            VerbalizeMode.Math => VerbalizeMath(text, lang),
            VerbalizeMode.Spell => Spell(text, lang),
            _ => VerbalizeNormal(text, lang),
        };
    }

    // ---------------------------------------------------------------- Normal

    private static string VerbalizeNormal(string text, Lang lang)
    {
        if (text.AsSpan().IndexOfAny(NormalTriggers) < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 16);
        int n = text.Length;
        int i = 0;
        while (i < n)
        {
            char c = text[i];
            if (IsTokenStart(text, i) && TryAppendProtectedToken(text, i, lang, sb, out int tokenEnd))
            {
                i = tokenEnd;
                continue;
            }

            switch (c)
            {
                case '@' when IsTokenStart(text, i) && i + 1 < n && IsWordChar(text[i + 1]):
                    AppendWords(sb, At.For(lang), text, i + 1);
                    i++;
                    continue;

                case '#' when IsTokenStart(text, i) && i + 1 < n && char.IsLetter(text[i + 1]):
                    AppendWords(sb, Hashtag.For(lang), text, i + 1);
                    i++;
                    continue;

                case '#' when IsTokenStart(text, i) && i + 1 < n && char.IsDigit(text[i + 1]):
                    AppendWords(sb, NumberSign.For(lang), text, i + 1);
                    i++;
                    continue;

                case '&' when IsStandaloneOrBetweenLetters(text, i):
                    AppendWords(sb, And.For(lang), text, i + 1);
                    i++;
                    continue;

                case '%' or '٪' when PrecededByDigit(text, i):
                    AppendWords(sb, Percent.For(lang), text, i + 1);
                    i++;
                    continue;

                case '%' or '٪' when IsTokenStart(text, i) && i + 1 < n && char.IsDigit(text[i + 1]):
                {
                    // "%50" (percent typed before the number) reads as "50 percent".
                    int j = i + 1;
                    while (j < n && (char.IsDigit(text[j]) || (text[j] is '.' or '٫' or ',' && j + 1 < n && char.IsDigit(text[j + 1]))))
                    {
                        j++;
                    }

                    sb.Append(text, i + 1, j - i - 1);
                    AppendWords(sb, Percent.For(lang), text, j);
                    i = j;
                    continue;
                }

                case '²' or '³' when i > 0 && char.IsLetterOrDigit(text[i - 1]):
                    AppendWords(sb, (c == '²' ? Squared : Cubed).For(lang), text, i + 1);
                    i++;
                    continue;

                case '°':
                {
                    int j = i + 1;
                    if (j < n && text[j] == ' ')
                    {
                        j++;
                    }

                    bool one = IsOne(text, i);
                    if (j < n && text[j] is 'C' or 'F' && (j + 1 >= n || !char.IsLetter(text[j + 1])))
                    {
                        Bilingual words = text[j] == 'C' ? (one ? CelsiusOne : Celsius) : (one ? FahrenheitOne : Fahrenheit);
                        AppendWords(sb, words.For(lang), text, j + 1);
                        i = j + 1;
                        continue;
                    }

                    if (PrecededByDigit(text, i))
                    {
                        AppendWords(sb, (one ? Degree : Degrees).For(lang), text, i + 1);
                        i++;
                        continue;
                    }

                    break;
                }

                case '℃' or '℉':
                {
                    bool one = IsOne(text, i);
                    Bilingual words = c == '℃' ? (one ? CelsiusOne : Celsius) : (one ? FahrenheitOne : Fahrenheit);
                    AppendWords(sb, words.For(lang), text, i + 1);
                    i++;
                    continue;
                }

                default:
                    if (Fractions.TryGetValue(c, out var fraction))
                    {
                        Bilingual words = PrecededByDigit(text, i) ? fraction.AfterNumber : fraction.Alone;
                        AppendWords(sb, words.For(lang), text, i + 1);
                        i++;
                        continue;
                    }

                    break;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsTokenStart(string text, int i) =>
        i == 0 || char.IsWhiteSpace(text[i - 1]) || text[i - 1] is '(' or '[' or '"' or '\'' or '«';

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int TokenEnd(string text, int start)
    {
        int end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        return end;
    }

    /// <summary>URLs are left exactly as written; e-mail addresses get "at" for the @.</summary>
    private static bool TryAppendProtectedToken(string text, int start, Lang lang, StringBuilder sb, out int end)
    {
        end = TokenEnd(text, start);
        ReadOnlySpan<char> token = text.AsSpan(start, end - start);
        if (token.Contains("://", StringComparison.Ordinal) || token.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(token);
            return true;
        }

        int at = token.IndexOf('@');
        if (at > 0 && at < token.Length - 1 && token[(at + 1)..].IndexOf('@') < 0 && IsWordChar(token[at - 1]))
        {
            ReadOnlySpan<char> domain = token[(at + 1)..].TrimEnd(".,;:!?)]}\"'؟،؛");
            int dot = domain.IndexOf('.');
            if (dot > 0 && dot < domain.Length - 1)
            {
                sb.Append(token[..at]).Append(' ').Append(At.For(lang)).Append(' ').Append(token[(at + 1)..]);
                return true;
            }
        }

        return false;
    }

    private static bool IsStandaloneOrBetweenLetters(string text, int i)
    {
        int start = i, end = i + 1;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        end = TokenEnd(text, end);
        ReadOnlySpan<char> token = text.AsSpan(start, end - start);
        if (token.Length == 1)
        {
            return true;
        }

        if (token.Contains("&&", StringComparison.Ordinal) || token.IndexOfAny('=', '/', ';') >= 0)
        {
            return false; // query strings, code, HTML entities
        }

        return i > 0 && i + 1 < text.Length && char.IsLetter(text[i - 1]) && char.IsLetter(text[i + 1]);
    }

    private static bool PrecededByDigit(string text, int i) =>
        (i > 0 && char.IsDigit(text[i - 1])) || (i > 1 && text[i - 1] == ' ' && char.IsDigit(text[i - 2]));

    /// <summary>True when the number right before <paramref name="i"/> is exactly 1 (for "1 degree").</summary>
    private static bool IsOne(string text, int i)
    {
        int end = i;
        if (end > 0 && text[end - 1] == ' ')
        {
            end--;
        }

        int start = end;
        while (start > 0 && char.IsDigit(text[start - 1]))
        {
            start--;
        }

        return end - start == 1 && ScriptInfo.ToAsciiDigit(text[start]) == '1'
            && (start == 0 || !(char.IsDigit(text[start - 1]) || text[start - 1] is '.' or ',' or '٫'));
    }

    private static bool IsOpener(char c) => c is '(' or '[' or '{' or '"' or '«' or '\'';

    private static bool IsTrailingPunctuation(char c) =>
        c is '.' or ',' or ';' or ':' or '!' or '?' or '؟' or '،' or '؛' or ')' or ']' or '}' or '"' or '\'' or '»' or '…';

    /// <summary>Appends inserted words with single spaces around them, without doubling existing spaces.</summary>
    private static void AppendWords(StringBuilder sb, string words, string text, int nextIndex)
    {
        if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]) && !IsOpener(sb[^1]))
        {
            sb.Append(' ');
        }

        sb.Append(words);
        if (nextIndex < text.Length && !char.IsWhiteSpace(text[nextIndex]) && !IsTrailingPunctuation(text[nextIndex]))
        {
            sb.Append(' ');
        }
    }

    // ---------------------------------------------------------------- Math

    private static string VerbalizeMath(string text, Lang lang)
    {
        var tokens = new List<string>();
        new MathReader(text, lang, tokens).Read();
        return string.Join(' ', tokens);
    }

    private sealed class MathReader(string s, Lang lang, List<string> output)
    {
        private int _i;
        private bool _operand;
        private bool _lastWasNumber;

        public void Read()
        {
            while (_i < s.Length)
            {
                ReadOne();
            }
        }

        private void ReadOne()
        {
            char c = s[_i];
            if (char.IsWhiteSpace(c))
            {
                _i++;
                return;
            }

            if (Superscripts.ContainsKey(c))
            {
                ReadScript(Superscripts, superscript: true);
                return;
            }

            if (Subscripts.ContainsKey(c))
            {
                ReadScript(Subscripts, superscript: false);
                return;
            }

            if (char.IsDigit(c))
            {
                ReadNumber();
                return;
            }

            if (GreekLetters.TryGetValue(c, out var greek))
            {
                Emit(greek.For(lang), operand: true);
                _i++;
                return;
            }

            if (ScriptInfo.IsArabicStrong(c))
            {
                int start = _i;
                while (_i < s.Length && ScriptInfo.IsArabicStrong(s[_i]))
                {
                    _i++;
                }

                Emit(s[start.._i], operand: true);
                return;
            }

            if (char.IsLetter(c))
            {
                ReadLatinWord();
                return;
            }

            if (Fractions.TryGetValue(c, out var fraction))
            {
                Emit((_lastWasNumber ? fraction.AfterNumber : fraction.Alone).For(lang), operand: true);
                _i++;
                return;
            }

            if (TryReadSpecial(c))
            {
                return;
            }

            for (int len = Math.Min(MaxMathSymbolLength, s.Length - _i); len >= 1; len--)
            {
                if (MathSymbols.TryGetValue(s.Substring(_i, len), out var symbol))
                {
                    Emit(symbol.Words.For(lang), symbol.OperandAfter);
                    _i += len;
                    return;
                }
            }

            if (char.IsHighSurrogate(c) && _i + 1 < s.Length)
            {
                Emit(s.Substring(_i, 2), operand: true);
                _i += 2;
                return;
            }

            Emit(c.ToString(), operand: true);
            _i++;
        }

        private bool TryReadSpecial(char c)
        {
            switch (c)
            {
                case '-' or '−' or '–' or '‐' or '﹣' or '－' when !(c == '-' && Peek(1) == '>'):
                    Emit((_operand ? Minus : Negative).For(lang), operand: false);
                    _i++;
                    return true;

                case '^':
                    _i++;
                    ReadExponent();
                    return true;

                case '*' when Peek(1) == '*':
                    _i += 2;
                    ReadExponent();
                    return true;

                case '_':
                    _i++;
                    ReadSubscriptGroup();
                    return true;

                case '\'' or '′' or '’' when _operand:
                {
                    int count = 0;
                    while (_i < s.Length && s[_i] is '\'' or '′' or '’')
                    {
                        count++;
                        _i++;
                    }

                    Emit((count == 1 ? Prime : count == 2 ? DoublePrime : TriplePrime).For(lang), operand: true);
                    return true;
                }

                case '″':
                    Emit(DoublePrime.For(lang), operand: true);
                    _i++;
                    return true;

                case '‴':
                    Emit(TriplePrime.For(lang), operand: true);
                    _i++;
                    return true;

                case '!' when Peek(1) != '=':
                    if (_operand)
                    {
                        Emit(Factorial.For(lang), operand: true);
                    }

                    _i++;
                    return true;

                case '|':
                    Emit(VerticalBar.For(lang), _operand);
                    _i++;
                    return true;

                case ':' when Peek(1) != '=':
                    if (_lastWasNumber && char.IsDigit(NextNonSpace(_i + 1)))
                    {
                        Emit(Ratio.For(lang), operand: false);
                    }

                    _i++;
                    _operand = false;
                    return true;

                case '.' or ';' or '؛' or '?' or '؟' or '\'' or '"' or '’':
                    _i++;
                    return true;

                default:
                    return false;
            }
        }

        private char Peek(int offset) => _i + offset < s.Length ? s[_i + offset] : '\0';

        private char NextNonSpace(int from)
        {
            while (from < s.Length && char.IsWhiteSpace(s[from]))
            {
                from++;
            }

            return from < s.Length ? s[from] : '\0';
        }

        private void Emit(string token, bool operand)
        {
            output.Add(token);
            _operand = operand;
            _lastWasNumber = false;
        }

        private void ReadNumber()
        {
            var whole = new StringBuilder();
            StringBuilder? fraction = null;
            while (_i < s.Length)
            {
                char c = s[_i];
                if (char.IsDigit(c))
                {
                    (fraction ?? whole).Append(ScriptInfo.ToAsciiDigit(c));
                    _i++;
                }
                else if (fraction is null && c is '.' or '٫' && char.IsDigit(Peek(1)))
                {
                    fraction = new StringBuilder();
                    _i++;
                }
                else if (fraction is null && c is ',' or '٬' && IsThousandsGroup(_i))
                {
                    _i++;
                }
                else
                {
                    break;
                }
            }

            output.Add(whole.ToString());
            if (fraction is not null)
            {
                output.Add(DecimalPoint.For(lang));
                // English reads decimals digit by digit ("3 point 1 4"); Arabic reads them as a number ("3 فاصلة 14").
                output.Add(lang == Lang.English ? string.Join(' ', fraction.ToString().ToCharArray()) : fraction.ToString());
            }

            _operand = true;
            _lastWasNumber = true;
        }

        private bool IsThousandsGroup(int comma)
        {
            for (int k = 1; k <= 3; k++)
            {
                if (comma + k >= s.Length || !char.IsDigit(s[comma + k]))
                {
                    return false;
                }
            }

            return comma + 4 >= s.Length || !char.IsDigit(s[comma + 4]);
        }

        private void ReadLatinWord()
        {
            int start = _i;
            while (_i < s.Length && char.IsLetter(s[_i]) && !ScriptInfo.IsArabicScript(s[_i])
                && !GreekLetters.ContainsKey(s[_i]) && !Superscripts.ContainsKey(s[_i]) && !Subscripts.ContainsKey(s[_i]))
            {
                _i++;
            }

            string word = s[start.._i];
            if (MathFunctions.TryGetValue(word, out var function))
            {
                Emit(function.For(lang), operand: false);
                return;
            }

            if (word == "x" && _lastWasNumber && char.IsDigit(NextNonSpace(_i)))
            {
                Emit(Times.For(lang), operand: false); // 3x4, 1920 x 1080
                return;
            }

            if (word.Length <= 2)
            {
                foreach (char letter in word)
                {
                    Emit(letter.ToString(), operand: true); // xy is x times y
                }

                return;
            }

            Emit(word, operand: true);
        }

        private void ReadScript(IReadOnlyDictionary<char, char> map, bool superscript)
        {
            var plain = new StringBuilder();
            while (_i < s.Length && map.TryGetValue(s[_i], out char mapped))
            {
                plain.Append(mapped);
                _i++;
            }

            if (superscript)
            {
                EmitPower(plain.ToString());
            }
            else
            {
                EmitSubscript(plain.ToString());
            }
        }

        private void ReadExponent()
        {
            while (_i < s.Length && s[_i] == ' ')
            {
                _i++;
            }

            if (_i < s.Length && s[_i] == '{')
            {
                EmitPower(ReadBraceGroup());
                return;
            }

            if (_i < s.Length && char.IsDigit(s[_i]))
            {
                int start = _i;
                while (_i < s.Length && char.IsDigit(s[_i]))
                {
                    _i++;
                }

                bool decimalFollows = _i + 1 < s.Length && s[_i] is '.' or '٫' && char.IsDigit(s[_i + 1]);
                if (!decimalFollows)
                {
                    EmitPower(s[start.._i]);
                    return;
                }

                _i = start;
            }

            Emit(Power.For(lang), operand: false);
        }

        private void ReadSubscriptGroup()
        {
            if (_i < s.Length && s[_i] == '{')
            {
                EmitSubscript(ReadBraceGroup());
                return;
            }

            int start = _i;
            if (_i < s.Length && char.IsDigit(s[_i]))
            {
                while (_i < s.Length && char.IsDigit(s[_i]))
                {
                    _i++;
                }
            }
            else if (_i < s.Length && char.IsLetter(s[_i]))
            {
                _i++;
            }

            EmitSubscript(s[start.._i]);
        }

        private string ReadBraceGroup()
        {
            int depth = 0, start = _i + 1;
            while (_i < s.Length)
            {
                if (s[_i] == '{')
                {
                    depth++;
                }
                else if (s[_i] == '}' && --depth == 0)
                {
                    _i++;
                    return s[start..(_i - 1)];
                }

                _i++;
            }

            return s[start..];
        }

        private List<string> Tokens(string inner)
        {
            var tokens = new List<string>();
            new MathReader(inner, lang, tokens).Read();
            return tokens;
        }

        private void EmitPower(string exponent)
        {
            string trimmed = exponent.Trim();
            if (trimmed == "2" || trimmed == "3")
            {
                Emit((trimmed == "2" ? Squared : Cubed).For(lang), operand: true);
                return;
            }

            output.Add(Power.For(lang));
            output.AddRange(Tokens(exponent));
            _operand = true;
            _lastWasNumber = false;
        }

        private void EmitSubscript(string inner)
        {
            List<string> tokens = Tokens(inner);
            if (tokens.Count == 0)
            {
                return;
            }

            if (lang == Lang.English)
            {
                output.Add(Subscript.En);
                output.AddRange(tokens);
            }
            else
            {
                // "x₁" → "x واحد سفلي".
                if (tokens.Count == 1 && tokens[0].Length == 1 && tokens[0][0] is >= '0' and <= '9')
                {
                    tokens[0] = DigitNames[tokens[0][0] - '0'].Ar;
                }

                output.AddRange(tokens);
                output.Add(Subscript.Ar);
            }

            _operand = true;
            _lastWasNumber = false;
        }
    }

    // ---------------------------------------------------------------- Spell

    private static string Spell(string text, Lang lang)
    {
        var items = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            if (EmojiNames.TryReadCluster(text, i, out var cluster))
            {
                if (cluster.Name is { } name)
                {
                    items.Add(name.For(lang));
                }
                else if (cluster.Keep is { } keep)
                {
                    items.Add(SpellChar(keep[0], lang) ?? keep);
                }

                i += cluster.Length;
                continue;
            }

            if (char.IsSurrogate(text[i]))
            {
                if (Rune.TryGetRuneAt(text, i, out Rune rune))
                {
                    items.Add(rune.ToString());
                    i += rune.Utf16SequenceLength;
                }
                else
                {
                    i++;
                }

                continue;
            }

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            string? spoken = SpellChar(text[i], lang);
            if (spoken is not null)
            {
                items.Add(spoken);
            }

            i++;
        }

        return string.Join(", ", items);
    }

    /// <summary>The spoken name of one character, or null for invisible formatting characters.</summary>
    private static string? SpellChar(char c, Lang lang)
    {
        if (ArabicLetterNames.TryGetValue(c, out var letter))
        {
            return letter.For(lang);
        }

        if (TashkeelNames.TryGetValue(c, out var mark))
        {
            return mark.For(lang);
        }

        if (char.IsDigit(c))
        {
            return DigitNames[ScriptInfo.ToAsciiDigit(c) - '0'].For(lang);
        }

        if (SpellSymbols.TryGetValue(ScriptInfo.IsLineBreak(c) ? '\n' : c, out var symbol))
        {
            return symbol.For(lang);
        }

        if (char.IsWhiteSpace(c))
        {
            return SpellSymbols[' '].For(lang);
        }

        if (char.GetUnicodeCategory(c) == UnicodeCategory.Format)
        {
            return null;
        }

        if (char.IsLetter(c) && char.IsUpper(c))
        {
            return lang == Lang.Arabic ? $"{c} {Capital.Ar}" : $"{Capital.En} {c}";
        }

        if (GreekLetters.TryGetValue(c, out var greek))
        {
            return greek.For(lang);
        }

        return c.ToString();
    }
}
