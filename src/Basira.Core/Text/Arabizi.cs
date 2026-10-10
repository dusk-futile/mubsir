using System.Text;

namespace Basira.Core.Text;

/// <summary>
/// Detects and transliterates Arabizi (Arabic chat written in Latin letters and digits, e.g. "7abibi", "3ala")
/// so the Arabic voice can read it. Results are approximate; common words come from a dictionary.
/// </summary>
public static class Arabizi
{
    /// <summary>Common chat words that are not English words.</summary>
    private static readonly Dictionary<string, string> Strong = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enta"] = "انت", ["inta"] = "انت", ["enti"] = "انتي", ["inti"] = "انتي", ["ento"] = "انتو", ["entu"] = "انتو",
        ["e7na"] = "احنا", ["i7na"] = "احنا", ["homa"] = "هما",
        ["ezayak"] = "ازيك", ["ezayek"] = "ازيك", ["ezzayak"] = "ازيك", ["ezay"] = "ازاي", ["ezzay"] = "ازاي",
        ["yalla"] = "يلا", ["yallah"] = "يلا",
        ["inshallah"] = "إن شاء الله", ["insha2allah"] = "إن شاء الله", ["inshaallah"] = "إن شاء الله",
        ["inshalla"] = "إن شاء الله", ["nshallah"] = "إن شاء الله", ["inchallah"] = "إن شاء الله",
        ["mashallah"] = "ما شاء الله", ["masha2allah"] = "ما شاء الله", ["mashaallah"] = "ما شاء الله",
        ["alhamdulillah"] = "الحمد لله", ["el7amdulillah"] = "الحمد لله", ["al7amdulillah"] = "الحمد لله",
        ["el7amdolellah"] = "الحمد لله", ["hamdella"] = "الحمد لله", ["7amdella"] = "الحمد لله",
        ["habibi"] = "حبيبي", ["7abibi"] = "حبيبي", ["habibti"] = "حبيبتي", ["7abibti"] = "حبيبتي",
        ["shukran"] = "شكرا", ["shokran"] = "شكرا", ["choukran"] = "شكرا",
        ["keda"] = "كده", ["kda"] = "كده", ["kida"] = "كده", ["leh"] = "ليه", ["leih"] = "ليه",
        ["aywa"] = "أيوه", ["aiwa"] = "أيوه", ["aywah"] = "أيوه",
        ["la2"] = "لأ", ["la2a"] = "لأ", ["tamam"] = "تمام", ["wallah"] = "والله", ["wallahi"] = "والله",
        ["masa2"] = "مساء", ["kolo"] = "كله", ["kollo"] = "كله", ["kaman"] = "كمان", ["kman"] = "كمان",
        ["msh"] = "مش", ["mish"] = "مش", ["3arabi"] = "عربي", ["3ala"] = "على", ["3al"] = "عال",
        ["ma3a"] = "مع", ["ma3"] = "مع", ["ma3ak"] = "معاك", ["ma3aki"] = "معاكي",
        ["mabrook"] = "مبروك", ["mabrouk"] = "مبروك", ["marhaba"] = "مرحبا", ["mar7aba"] = "مرحبا",
        ["ahlan"] = "أهلا", ["2ahlan"] = "أهلا", ["kifak"] = "كيفك", ["kifik"] = "كيفك", ["keefak"] = "كيفك",
        ["3amel"] = "عامل", ["3amla"] = "عاملة", ["kwayes"] = "كويس", ["kwayyes"] = "كويس", ["kwayesa"] = "كويسة",
        ["7elw"] = "حلو", ["7elwa"] = "حلوة", ["shwaya"] = "شوية", ["shwayya"] = "شوية",
        ["ba3d"] = "بعد", ["ba3den"] = "بعدين", ["ba3dein"] = "بعدين",
        ["delwa2ty"] = "دلوقتي", ["delwa2ti"] = "دلوقتي", ["bokra"] = "بكرة", ["bukra"] = "بكرة",
        ["ennaharda"] = "النهارده", ["enaharda"] = "النهارده", ["emta"] = "إمتى", ["imta"] = "إمتى",
        ["fein"] = "فين", ["feen"] = "فين", ["meen"] = "مين", ["mien"] = "مين",
        ["ya3ni"] = "يعني", ["yaani"] = "يعني", ["akeed"] = "أكيد", ["akid"] = "أكيد",
        ["tayeb"] = "طيب", ["tayyeb"] = "طيب", ["tayb"] = "طيب",
        ["5alas"] = "خلاص", ["5alaas"] = "خلاص", ["khalas"] = "خلاص", ["5las"] = "خلاص",
        ["sa7"] = "صح", ["3ashan"] = "عشان", ["3alashan"] = "علشان", ["3shan"] = "عشان",
        ["ma3lesh"] = "معلش", ["ma3lish"] = "معلش", ["ma3lsh"] = "معلش",
        ["3afwan"] = "عفوا", ["afwan"] = "عفوا", ["salamtak"] = "سلامتك", ["salama"] = "سلامة",
        ["ramadan"] = "رمضان", ["mubarak"] = "مبارك", ["3eid"] = "عيد", ["zaman"] = "زمان", ["insan"] = "إنسان",
        ["jiddan"] = "جدا", ["gedan"] = "جدا", ["aslan"] = "أصلا", ["abadan"] = "أبدا", ["tab3an"] = "طبعا",
        ["7aga"] = "حاجة", ["7aja"] = "حاجة", ["3ayez"] = "عايز", ["3ayz"] = "عايز", ["3awez"] = "عاوز",
        ["wa7ed"] = "واحد", ["wa7da"] = "واحدة", ["ana"] = "أنا", ["eh"] = "إيه", ["eih"] = "إيه",
        ["bas"] = "بس", ["mesh"] = "مش", ["salam"] = "سلام", ["sabah"] = "صباح", ["ya"] = "يا", ["w"] = "و",
        ["3aref"] = "عارف", ["3arfa"] = "عارفة", ["ba2a"] = "بقى", ["ba2et"] = "بقيت", ["2awi"] = "أوي", ["awi"] = "أوي",
        ["bina"] = "بينا", ["beit"] = "بيت", ["bait"] = "بيت", ["basha"] = "باشا", ["fi"] = "في", ["fih"] = "فيه",
        ["mafeesh"] = "مفيش", ["mafish"] = "مفيش", ["kont"] = "كنت", ["ahu"] = "أهو", ["bardo"] = "برضه",
        ["lessa"] = "لسه", ["zay"] = "زي", ["rabena"] = "ربنا", ["a7san"] = "أحسن", ["ba7ebak"] = "بحبك",
        ["ba7ebek"] = "بحبك", ["w7ashtini"] = "وحشتني", ["wa7ashtini"] = "وحشتني", ["sadi2i"] = "صديقي",
        ["ezayko"] = "ازيكو", ["3amlin"] = "عاملين", ["el"] = "ال", ["il"] = "ال", ["wenta"] = "وانت",
        ["tab"] = "طب", ["ah"] = "آه", ["la"] = "لا", ["mama"] = "ماما", ["baba"] = "بابا", ["allah"] = "الله",
    };

    /// <summary>
    /// Dictionary words that are also English words or names ("ana", "mesh", "bas"); these are
    /// transliterated by <see cref="TransliterateArabiziWords"/> only when the text has other Arabizi.
    /// </summary>
    private static readonly HashSet<string> Ambiguous = new(StringComparer.OrdinalIgnoreCase)
    {
        "ana", "eh", "bas", "mesh", "ya", "w", "sabah", "salama", "insan", "zaman", "ramadan", "mubarak", "fein",
        "fi", "el", "il", "tab", "ah", "la", "mama", "baba", "allah", "zay", "bait",
    };

    /// <summary>Digit-bearing tokens that are English or technical, not Arabizi.</summary>
    private static readonly HashSet<string> NotArabizi = new(StringComparer.OrdinalIgnoreCase)
    {
        "h2o", "co2", "no2", "so2", "b2b", "b2c", "c2c", "p2p", "mp3", "2fa", "2day", "2nite", "2night",
        "2morrow", "2moro", "l8r", "gr8", "w8", "m8", "h8", "sk8", "str8", "any1", "some1", "ne1", "w3c",
    };

    /// <summary>Suffixes after a single digit that make an ordinal or unit ("2nd", "5pm", "3d").</summary>
    private static readonly HashSet<string> UnitSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "st", "nd", "rd", "th", "am", "pm", "x", "d", "g", "k", "s", "gb", "mb", "kb", "tb", "hz", "px", "cm",
        "mm", "km", "kg", "ml", "min", "mins", "h", "hr", "hrs", "m", "ft", "in", "lb", "lbs", "yo", "yr", "yrs",
        "sec", "secs", "ms", "v", "w", "mp", "l", "p",
    };

    private static readonly Dictionary<char, string> Consonants = new()
    {
        ['b'] = "ب", ['c'] = "ك", ['d'] = "د", ['f'] = "ف", ['g'] = "ج", ['h'] = "ه", ['j'] = "ج",
        ['k'] = "ك", ['l'] = "ل", ['m'] = "م", ['n'] = "ن", ['p'] = "ب", ['q'] = "ق", ['r'] = "ر",
        ['s'] = "س", ['t'] = "ت", ['v'] = "ف", ['w'] = "و", ['x'] = "كس", ['y'] = "ي", ['z'] = "ز",
    };

    private static readonly Dictionary<string, string> Digraphs = new()
    {
        ["sh"] = "ش", ["ch"] = "ش", ["kh"] = "خ", ["gh"] = "غ", ["th"] = "ث", ["dh"] = "ذ",
    };

    /// <summary>
    /// True for a Latin token with Arabizi digits (2 3 5 6 7 8 9) next to letters ("7abibi", "3ala", "la2"),
    /// or a common Arabizi word ("yalla", "inshallah"). Ordinals, units and technical names (2nd, 5pm, MP3) are excluded.
    /// </summary>
    public static bool LooksLikeArabizi(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var (start, end) = CoreRange(word);
        if (end <= start)
        {
            return false;
        }

        ReadOnlySpan<char> core = word.AsSpan(start, end - start);
        foreach (char c in core)
        {
            if (!(ScriptInfo.IsAsciiLetter(c) || char.IsAsciiDigit(c) || IsApostrophe(c)))
            {
                return false;
            }
        }

        string lower = core.ToString().ToLowerInvariant();
        if (Strong.ContainsKey(lower))
        {
            return true;
        }

        if (NotArabizi.Contains(lower))
        {
            return false;
        }

        int letters = 0, upper = 0, digitCount = 0;
        bool hasDigit = false, hasVowel = false, digitInside = false;
        for (int i = 0; i < core.Length; i++)
        {
            char c = core[i];
            if (ScriptInfo.IsAsciiLetter(c))
            {
                letters++;
                upper += char.IsAsciiLetterUpper(c) ? 1 : 0;
                hasVowel |= IsVowel(char.ToLowerInvariant(c));
                continue;
            }

            if (!char.IsAsciiDigit(c))
            {
                continue;
            }

            bool digitBefore = i > 0 && char.IsAsciiDigit(core[i - 1]);
            bool digitAfter = i + 1 < core.Length && char.IsAsciiDigit(core[i + 1]);
            if (digitBefore || digitAfter || c is '0' or '1' or '4')
            {
                return false; // 19, 2024, b4, 4u: numbers and English text-speak
            }

            hasDigit = true;
            digitCount++;
            bool letterBefore = i > 0 && ScriptInfo.IsAsciiLetter(core[i - 1]);
            bool letterAfter = i + 1 < core.Length && (ScriptInfo.IsAsciiLetter(core[i + 1]) || IsApostrophe(core[i + 1]));
            digitInside |= letterBefore && letterAfter;
        }

        // "9a7", "7a2": a single vowel between two Arabizi digits.
        digitInside |= letters == 1 && hasVowel && digitCount == 2 && core.Length == 3;
        if (!hasDigit || upper == letters || (letters < 2 && !digitInside))
        {
            return false; // no Arabizi digit, an acronym such as MP3 / H2O, or "2x"
        }

        if (char.IsAsciiDigit(lower[0]) && UnitSuffixes.Contains(lower[1..]))
        {
            return false; // 2nd, 5pm, 3d, 8gb
        }

        char last = lower[^1];
        if (char.IsAsciiDigit(last) && lower.Length >= 2 && !IsVowel(lower[^2]) && !digitInside && !char.IsAsciiDigit(lower[0]))
        {
            return false; // win7, ios9: a name followed by a version number
        }

        return hasVowel || digitInside;
    }

    /// <summary>Transliterates the whole text as Arabizi, leaving URLs, e-mails and non-Latin text alone.</summary>
    public static string ToArabic(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Transliterate(text, all: true);
    }

    /// <summary>
    /// Transliterates only tokens that look like Arabizi; real English words ("hello", "file", "computer") stay.
    /// Dictionary words that are also English ("ana", "mesh") change only when the text has other Arabizi.
    /// </summary>
    public static string TransliterateArabiziWords(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Transliterate(text, all: false);
    }

    private static string Transliterate(string text, bool all)
    {
        var tokens = Tokenize(text);
        var convert = new bool[tokens.Count];
        bool context = all; // in word mode, true once a token is clearly Arabizi rather than also English
        for (int k = 0; k < tokens.Count; k++)
        {
            var t = tokens[k];
            if (t.IsSpace)
            {
                continue;
            }

            if (all)
            {
                convert[k] = IsLatinWordToken(t.Text);
            }
            else if (LooksLikeArabizi(t.Text))
            {
                convert[k] = true;
                context |= !Ambiguous.Contains(CoreOf(t.Text));
            }
        }

        if (!context)
        {
            return text; // nothing, or only words such as "ana" or "bas" that are also English
        }

        var sb = new StringBuilder(text.Length);
        for (int k = 0; k < tokens.Count; k++)
        {
            var token = tokens[k];
            if (!convert[k])
            {
                sb.Append(token.Text);
                continue;
            }

            // "el beit" → "البيت", "w enta" → "وانت": the article and "and" join the next word.
            if (JoiningPrefix(token.Text) is { } prefix && k + 2 < tokens.Count && tokens[k + 1].IsSpace && convert[k + 2])
            {
                sb.Append(prefix);
                k++; // skip the space; the next word is appended without it
                continue;
            }

            sb.Append(ConvertToken(token.Text));
        }

        return sb.ToString();
    }

    private static string? JoiningPrefix(string token) => token.ToLowerInvariant() switch
    {
        "el" or "al" or "il" => "ال",
        "w" or "wa" => "و",
        _ => null,
    };

    private readonly record struct Token(string Text, bool IsSpace);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < text.Length)
        {
            bool space = char.IsWhiteSpace(text[i]);
            int start = i;
            while (i < text.Length && char.IsWhiteSpace(text[i]) == space)
            {
                i++;
            }

            tokens.Add(new Token(text[start..i], space));
        }

        return tokens;
    }

    private static bool IsApostrophe(char c) => c is '\'' or '’';

    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';

    /// <summary>The word inside a token: from its first to its last letter or digit, plus a trailing 3'/7' apostrophe.</summary>
    private static (int Start, int End) CoreRange(string token)
    {
        int start = 0;
        while (start < token.Length && !char.IsLetterOrDigit(token[start]))
        {
            start++;
        }

        int end = token.Length;
        while (end > start && !char.IsLetterOrDigit(token[end - 1]))
        {
            end--;
        }

        if (end < token.Length && end > start && IsApostrophe(token[end]) && token[end - 1] is '3' or '6' or '7' or '9')
        {
            end++;
        }

        return (start, end);
    }

    private static string CoreOf(string token)
    {
        var (start, end) = CoreRange(token);
        return end > start ? token[start..end] : "";
    }

    /// <summary>A token whose core is only ASCII letters, digits, apostrophes and hyphens, with at least one letter.</summary>
    private static bool IsLatinWordToken(string token)
    {
        string core = CoreOf(token);
        bool letter = false;
        foreach (char c in core)
        {
            if (ScriptInfo.IsAsciiLetter(c))
            {
                letter = true;
            }
            else if (!(char.IsAsciiDigit(c) || IsApostrophe(c) || c == '-'))
            {
                return false;
            }
        }

        return letter;
    }

    private static string ConvertToken(string token)
    {
        var (start, end) = CoreRange(token);
        var sb = new StringBuilder(token.Length + 4);
        sb.Append(token, 0, start);
        string core = token[start..end];
        string[] parts = core.Split('-');
        for (int p = 0; p < parts.Length; p++)
        {
            string part = parts[p].ToLowerInvariant();
            if (part is "el" or "al" or "il" && p < parts.Length - 1)
            {
                sb.Append("ال"); // "el-bet" → "البت"
                continue;
            }

            if (p > 0 && parts[p - 1].ToLowerInvariant() is not ("el" or "al" or "il"))
            {
                sb.Append(' ');
            }

            sb.Append(TransliterateWord(part));
        }

        foreach (char c in token.AsSpan(end))
        {
            sb.Append(c switch { '?' => '؟', ',' => '،', ';' => '؛', _ => c });
        }

        return sb.ToString();
    }

    private static string TransliterateWord(string w)
    {
        if (w.Length == 0)
        {
            return w;
        }

        if (Strong.TryGetValue(w, out string? known))
        {
            return known;
        }

        var sb = new StringBuilder(w.Length);
        char previous = '\0'; // previous source letter, to collapse doubled consonants
        int n = w.Length;
        int i = 0;
        while (i < n)
        {
            char c = w[i];
            bool first = sb.Length == 0;

            if (c == '2' && first && i + 1 < n && IsVowel(w[i + 1]))
            {
                // Word-initial hamza carries its vowel: "2ana" → أنا, "2ism" → إسم.
                sb.Append(w[i + 1] is 'e' or 'i' ? 'إ' : 'أ');
                previous = c;
                i += 2;
                continue;
            }

            if (char.IsAsciiDigit(c))
            {
                bool apostrophe = i + 1 < n && IsApostrophe(w[i + 1]);
                sb.Append(c switch
                {
                    '2' => first ? "أ" : HamzaSeat(w, i),
                    '3' => apostrophe ? "غ" : "ع",
                    '5' => "خ",
                    '6' => apostrophe ? "ظ" : "ط",
                    '7' => apostrophe ? "خ" : "ح",
                    '8' => "ق",
                    '9' => apostrophe ? "ض" : "ص",
                    _ => c.ToString(),
                });
                previous = c;
                i += apostrophe ? 2 : 1;
                continue;
            }

            if (!ScriptInfo.IsAsciiLetter(c))
            {
                i++;
                continue;
            }

            if (i + 1 < n && Digraphs.TryGetValue(w.Substring(i, 2), out string? digraph))
            {
                sb.Append(digraph);
                previous = c;
                i += 2;
                continue;
            }

            if (IsVowel(c))
            {
                i += AppendVowel(sb, w, i, first);
                previous = c;
                continue;
            }

            if (c == previous)
            {
                i++; // doubled consonant (shadda): "yalla" → يلا
                continue;
            }

            sb.Append(Consonants.TryGetValue(c, out string? consonant) ? consonant : c.ToString());
            previous = c;
            i++;
        }

        return sb.ToString();
    }

    /// <summary>Appends the Arabic for the vowel at <paramref name="i"/>; returns how many letters were used.</summary>
    private static int AppendVowel(StringBuilder sb, string w, int i, bool first)
    {
        int n = w.Length;
        char c = w[i];
        char next = i + 1 < n ? w[i + 1] : '\0';
        string pair = string.Concat(c, next);

        if (first)
        {
            if (pair == "aa")
            {
                sb.Append('آ');
                return 2;
            }

            sb.Append(c is 'e' or 'i' ? 'ا' : 'أ');
            if (next == c || pair is "ou")
            {
                sb.Append(c is 'e' or 'i' ? 'ي' : 'و');
                return 2;
            }

            return 1;
        }

        string? longVowel = pair switch
        {
            "aa" => "ا",
            "ee" or "ii" => "ي",
            "oo" or "ou" or "uu" => "و",
            _ => null,
        };
        if (longVowel is not null)
        {
            sb.Append(longVowel);
            return 2;
        }

        if (i == n - 1)
        {
            sb.Append(c switch { 'a' => "ا", 'o' or 'u' => "و", _ => "ي" });
            return 1;
        }

        // Tanween: "shukran" → شكرا, "ahlan" → أهلا (final -an after two consonants).
        if (c == 'a' && i == n - 2 && next == 'n' && i >= 2 && IsConsonantLike(w[i - 1]) && IsConsonantLike(w[i - 2]))
        {
            sb.Append('ا');
            return 2;
        }

        if (c == 'i')
        {
            sb.Append('ي');
        }

        return 1; // other short vowels are not written
    }

    /// <summary>Medial hamza sits on ي, و or ا by the strongest neighbouring vowel (i over u over a); final hamza stands alone.</summary>
    private static string HamzaSeat(string w, int i)
    {
        if (i == w.Length - 1)
        {
            return "ء";
        }

        char before = w[i - 1], after = w[i + 1];
        if (before is 'i' or 'e' || after is 'i' or 'e')
        {
            return "ئ";
        }

        if (before is 'o' or 'u' || after is 'o' or 'u')
        {
            return "ؤ";
        }

        return before == 'a' || after == 'a' ? "أ" : "ء";
    }

    private static bool IsConsonantLike(char c) => char.IsAsciiDigit(c) || (ScriptInfo.IsAsciiLetter(c) && !IsVowel(c));
}
