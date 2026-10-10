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
        bool stripped = false;
        while (true)
        {
            if (Phrases.TryGetValue(Join(words), out LocalCommand command))
            {
                return new CommandMatch(command, null, stripped ? FillerConfidence : ExactConfidence);
            }

            if (!TryStripOneFiller(ref words))
            {
                break;
            }

            stripped = true;
        }

        if (words.IsEmpty)
        {
            return null;
        }

        if (MatchOpenApp(words) is string app)
        {
            return new CommandMatch(LocalCommand.OpenApp, app, stripped ? OpenAppFillerConfidence : OpenAppConfidence);
        }

        return MatchAdaptedVerb(words);
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
    private static CommandMatch? MatchAdaptedVerb(ReadOnlySpan<string> words)
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

            if (MatchOpenApp(variant) is string app)
            {
                return new CommandMatch(LocalCommand.OpenApp, app, AdaptedVerbConfidence);
            }
        }

        return null;
    }

    /// <summary>"افتح X" / "شغل X" / "open X" where X is a 1–3 word app name and nothing else.</summary>
    private static string? MatchOpenApp(ReadOnlySpan<string> words)
    {
        if (words.Length < 2 || !OpenVerbs.Contains(words[0]))
        {
            return null;
        }

        ReadOnlySpan<string> arg = words[1..];
        if (arg.Length > 1 && OpenVerbParticles.Contains(arg[0]))
        {
            arg = arg[1..];
        }

        while (arg.Length > 1 && AppNameNoise.Contains(arg[0]))
        {
            arg = arg[1..];
        }

        while (arg.Length > 1 && AppNameNoise.Contains(arg[^1]))
        {
            arg = arg[..^1];
        }

        if (arg.Length is 0 or > MaxAppWords || NonAppFirstWords.Contains(arg[0]))
        {
            return null;
        }

        for (int i = 0; i < arg.Length; i++)
        {
            string w = arg[i];
            if (StopWords.Contains(w) || SecondVerbs.Contains(w) || FillerWords.Contains(w) || AppNameNoise.Contains(w))
            {
                return null;
            }

            // "و" glued to a later word is a conjunction ("جوجل وابحث"), except in app names ("مايكروسوفت وورد").
            if (i > 0 && w.Length >= 2 && w[0] == 'و' && !WawAppWords.Contains(w))
            {
                return null;
            }

            if (!w.Any(char.IsLetter))
            {
                return null;
            }
        }

        return Join(arg);
    }

    private static bool TryStripOneFiller(ref ReadOnlySpan<string> words)
    {
        foreach (string[] filler in Leading)
        {
            if (words.StartsWith(filler))
            {
                words = words[filler.Length..];
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
