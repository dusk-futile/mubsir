using Basira.Core.Text;

namespace Basira.Core.Router;

/// <summary>
/// Conservative local command matching. Only short, clear commands match; anything doubtful
/// returns null and goes to the AI. Matching runs on <see cref="ArabicFolding.Fold"/> output, so
/// spelling variants (أ/ا, ة/ه, ى/ي, tashkeel, punctuation, case) don't matter.
/// </summary>
public static class CommandGrammar
{
    public const double ExactConfidence = 1.0;
    public const double FillerConfidence = 0.9;
    public const double OpenAppConfidence = 0.95;
    public const double OpenAppFillerConfidence = 0.85;
    public const double AdaptedVerbConfidence = 0.8;

    private const int MaxUtteranceChars = 120;
    private const int MaxWords = 10;
    private const int MaxAppWords = 3;

    private static readonly Dictionary<string, LocalCommand> Phrases = BuildPhrases();
    private static readonly string[][] Leading = FoldPhrases(CommandPhrases.LeadingFillers);
    private static readonly string[][] Trailing = FoldPhrases(CommandPhrases.TrailingFillers);
    private static readonly HashSet<string> OpenVerbs = FoldWords(CommandPhrases.OpenVerbs);
    private static readonly HashSet<string> OpenVerbParticles = FoldWords(CommandPhrases.OpenVerbParticles);
    private static readonly HashSet<string> AppNameNoise = FoldWords(CommandPhrases.AppNameNoise);
    private static readonly HashSet<string> StopWords = FoldWords(CommandPhrases.ArgumentStopWords);
    private static readonly HashSet<string> SecondVerbs = FoldWords(CommandPhrases.ArgumentVerbs);
    private static readonly HashSet<string> NonAppFirstWords = FoldWords(CommandPhrases.NonAppFirstWords);
    private static readonly HashSet<string> WawAppWords = FoldWords(CommandPhrases.WawAppWords);
    private static readonly HashSet<string> FillerWords = BuildFillerWords();
    private static readonly string[] Names = Enum.GetNames<LocalCommand>();
    private static readonly Dictionary<string, LocalCommand> NameLookup = BuildNameLookup();

    /// <summary>Enum names of all local commands, e.g. for the speech-to-text prompt that returns <c>local_command</c>.</summary>
    public static IReadOnlyList<string> KnownLocalCommandNames => Names;

    /// <summary>Matches an utterance (transcript or typed request) to a local command, or null when unsure.</summary>
    public static CommandMatch? Match(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance) || utterance.Length > MaxUtteranceChars)
        {
            return null;
        }

        string folded = ArabicFolding.Fold(utterance);
        if (folded.Length == 0)
        {
            return null;
        }

        string[] all = folded.Split(' ');
        if (all.Length > MaxWords)
        {
            return null;
        }

        // Exact phrase first, then peel fillers one at a time ("اقرأ الشاشة من فضلك يا بصيرة").
        ReadOnlySpan<string> words = all;
        int offset = 0;
        bool stripped = false;
        while (true)
        {
            if (Phrases.TryGetValue(Join(words), out LocalCommand command))
            {
                return new CommandMatch(command, null, stripped ? FillerConfidence : ExactConfidence);
            }

            if (!TryStripOneFiller(ref words, ref offset))
            {
                break;
            }

            stripped = true;
        }

        if (words.IsEmpty)
        {
            return null;
        }

        if (TryMatchOpenApp(words, out int start, out int length))
        {
            return OpenApp(utterance, all, words.Slice(start, length), offset + start, stripped ? OpenAppFillerConfidence : OpenAppConfidence);
        }

        return MatchAdaptedVerb(utterance, all, words, offset);
    }

    /// <summary>Parses a command name such as the speech-to-text model's <c>local_command</c> (case-insensitive).</summary>
    public static bool TryParseName(string? name, out LocalCommand command)
    {
        command = default;
        return name is not null && NameLookup.TryGetValue(name.Trim(), out command);
    }

    /// <summary>
    /// "ممكن تقرا الشاشة" → "اقرا الشاشة"; "تعيد" → "عيد": a second-person verb after a polite
    /// opener is tried as the imperative. Only exact table phrases and the open-app pattern qualify.
    /// </summary>
    private static CommandMatch? MatchAdaptedVerb(string utterance, string[] all, ReadOnlySpan<string> words, int offset)
    {
        string first = words[0];
        if (first.Length < 3 || first[0] != 'ت')
        {
            return null;
        }

        string[] variant = words.ToArray();
        foreach (string candidate in (ReadOnlySpan<string>)["ا" + first[1..], first[1..]])
        {
            variant[0] = candidate;
            if (Phrases.TryGetValue(Join(variant), out LocalCommand command))
            {
                return new CommandMatch(command, null, AdaptedVerbConfidence);
            }

            if (TryMatchOpenApp(variant, out int start, out int length))
            {
                return OpenApp(utterance, all, variant.AsSpan(start, length), offset + start, AdaptedVerbConfidence);
            }
        }

        return null;
    }

    private static CommandMatch OpenApp(string utterance, string[] all, ReadOnlySpan<string> app, int firstWord, double confidence)
    {
        string folded = Join(app);
        return new CommandMatch(LocalCommand.OpenApp, folded, confidence)
        {
            ArgumentText = OriginalWords(utterance, all.Length, firstWord, app.Length) ?? folded,
        };
    }

    /// <summary>
    /// "افتح X" / "شغل X" / "open X" where X is a 1–3 word app name and nothing else.
    /// On success, <paramref name="start"/> and <paramref name="length"/> locate X in <paramref name="words"/>.
    /// </summary>
    private static bool TryMatchOpenApp(ReadOnlySpan<string> words, out int start, out int length)
    {
        start = 0;
        length = 0;
        if (words.Length < 2 || !OpenVerbs.Contains(words[0]))
        {
            return false;
        }

        int from = 1;
        int to = words.Length; // exclusive
        if (to - from > 1 && OpenVerbParticles.Contains(words[from]))
        {
            from++;
        }

        while (to - from > 1 && AppNameNoise.Contains(words[from]))
        {
            from++;
        }

        while (to - from > 1 && AppNameNoise.Contains(words[to - 1]))
        {
            to--;
        }

        ReadOnlySpan<string> arg = words[from..to];
        if (arg.Length is 0 or > MaxAppWords || NonAppFirstWords.Contains(arg[0]))
        {
            return false;
        }

        for (int i = 0; i < arg.Length; i++)
        {
            string w = arg[i];
            if (StopWords.Contains(w) || SecondVerbs.Contains(w) || FillerWords.Contains(w) || AppNameNoise.Contains(w))
            {
                return false;
            }

            // "و" glued to a later word is a conjunction ("جوجل وابحث"), except in app names ("مايكروسوفت وورد").
            if (i > 0 && w.Length >= 2 && w[0] == 'و' && !WawAppWords.Contains(w))
            {
                return false;
            }

            if (!w.Any(char.IsLetter))
            {
                return false;
            }
        }

        start = from;
        length = arg.Length;
        return true;
    }

    /// <summary>
    /// The original spelling of folded words [<paramref name="first"/>, first + <paramref name="count"/>),
    /// with edge punctuation trimmed; null when the words don't line up with whole original tokens.
    /// </summary>
    private static string? OriginalWords(string utterance, int foldedWordCount, int first, int count)
    {
        string[] tokens = utterance.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int word = 0;
        int firstToken = -1;
        int lastToken = -1;
        for (int t = 0; t < tokens.Length; t++)
        {
            string folded = ArabicFolding.Fold(tokens[t]);
            if (folded.Length == 0)
            {
                continue;
            }

            int n = folded.Split(' ').Length;
            int end = word + n; // exclusive
            if (word < first + count && end > first)
            {
                if (word < first || end > first + count)
                {
                    return null; // a token only partly inside the argument
                }

                firstToken = firstToken < 0 ? t : firstToken;
                lastToken = t;
            }

            word = end;
        }

        if (word != foldedWordCount || firstToken < 0)
        {
            return null;
        }

        ReadOnlySpan<char> text = string.Join(' ', tokens[firstToken..(lastToken + 1)]);
        while (text.Length > 0 && IsEdgeMark(text[0]))
        {
            text = text[1..];
        }

        while (text.Length > 0 && IsEdgeMark(text[^1]))
        {
            text = text[..^1];
        }

        return text.IsEmpty ? null : text.ToString();
    }

    private static bool IsEdgeMark(char c) => char.IsPunctuation(c) || char.IsSymbol(c) || c == '\u0640';

    private static bool TryStripOneFiller(ref ReadOnlySpan<string> words, ref int offset)
    {
        foreach (string[] filler in Leading)
        {
            if (words.StartsWith(filler))
            {
                words = words[filler.Length..];
                offset += filler.Length;
                return true;
            }
        }

        foreach (string[] filler in Trailing)
        {
            if (words.EndsWith(filler))
            {
                words = words[..^filler.Length];
                return true;
            }
        }

        return false;
    }

    private static string Join(ReadOnlySpan<string> words) => string.Join(' ', words);

    private static Dictionary<string, LocalCommand> BuildPhrases()
    {
        var map = new Dictionary<string, LocalCommand>(StringComparer.Ordinal);
        foreach ((LocalCommand command, string[] phrases) in CommandPhrases.Table)
        {
            foreach (string phrase in phrases)
            {
                map.TryAdd(ArabicFolding.Fold(phrase), command); // conflicts are caught by a unit test
            }
        }

        return map;
    }

    /// <summary>Longest first, so "can you please" wins over "can you".</summary>
    private static string[][] FoldPhrases(string[] phrases) => phrases
        .Select(p => ArabicFolding.Fold(p).Split(' '))
        .OrderByDescending(p => p.Length)
        .ToArray();

    private static HashSet<string> FoldWords(string[] words) =>
        new(words.Select(ArabicFolding.Fold), StringComparer.Ordinal);

    private static HashSet<string> BuildFillerWords()
    {
        // Single-word fillers only; multi-word ones contain common words like "you".
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] f in Leading.Concat(Trailing))
        {
            if (f.Length == 1)
            {
                set.Add(f[0]);
            }
        }

        return set;
    }

    private static Dictionary<string, LocalCommand> BuildNameLookup()
    {
        var map = new Dictionary<string, LocalCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (LocalCommand c in Enum.GetValues<LocalCommand>())
        {
            map[c.ToString()] = c;
        }

        return map;
    }
}
