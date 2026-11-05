using Mubsir.Core.Router;
using Mubsir.Core.Text;

namespace Mubsir.Core.Reading;

public enum BookCommandKind
{
    NextPage,
    PreviousPage,
    RepeatPage,
    GoToPage,
    WhereInBook,
    Continue,
    Mark,
    Note,
    ReadNotes,
    SleepTimer,
    CancelTimer,
    ReadingJournal,
    FindBook,
    Pick,
}

public sealed record BookCommand(BookCommandKind Kind, int? Number = null, string? Text = null);

/// <summary>Understands what a reader says about a book, in Arabic or English, on this PC with no AI.</summary>
public static class BookCommands
{
    private const int MaxChars = 200;

    private static readonly Dictionary<string, BookCommandKind> Exact = BuildExact();
    private static readonly string[][] Leading = Folded(CommandPhrases.LeadingFillers);
    private static readonly string[][] Trailing = Folded(CommandPhrases.TrailingFillers);
    private static readonly string[][] PagePrefixes = Folded(["go to page", "open page", "jump to page", "turn to page", "page", "روح لصفحة", "روح لصفحه", "روح صفحة", "افتح صفحة", "ادخل على صفحة", "روح على صفحة", "صفحة", "الصفحة", "اذهب الى صفحة", "انتقل الى صفحة"]);
    private static readonly string[][] NotePrefixes = Folded(["take a note", "make a note", "add a note", "write a note", "note", "سجل ملاحظة", "اكتب ملاحظة", "خد ملاحظة", "ضيف ملاحظة", "ملاحظة"]);
    private static readonly string[][] TimerPrefixes = Folded(["set a sleep timer for", "sleep timer for", "sleep timer", "read for", "keep reading for", "stop after", "stop in", "stop reading after", "stop reading in", "اقرأ لمدة", "اقرا لمدة", "اقرالي لمدة", "اقرأ لي لمدة", "اقف بعد", "وقف بعد", "وقف القراءة بعد", "اقف القراءة بعد", "اقرأ", "اقرا", "اقرالي"]);
    private static readonly string[][] FindPrefixes = Folded(["search my books for", "find me the book", "find me a book", "find the book", "find a book", "find book", "give me the book", "give me a book", "i want the book", "i want a book", "do i have a book", "open the book", "دور لي على كتاب", "دورلي على كتاب", "دور على كتاب", "ابحث عن كتاب", "هات لي كتاب", "هاتلي كتاب", "هات كتاب", "عايز كتاب", "عاوز كتاب", "اريد كتاب", "فين كتاب", "افتح كتاب", "عندي كتاب"]);
    private static readonly string[][] FindJoiners = Folded(["called", "named", "about", "on", "for", "عن", "بتاع", "بتاعت", "اسمه", "في"]);
    private static readonly string[][] PickPrefixes = Folded(["number", "open number", "read number", "open", "read", "the", "رقم", "افتح رقم", "اقرأ رقم", "افتح"]);

    private static readonly Dictionary<string, int> NumberWords = BuildNumbers();
    private static readonly Dictionary<string, int> Ordinals = new(StringComparer.Ordinal)
    {
        ["first"] = 1, ["second"] = 2, ["third"] = 3, ["fourth"] = 4, ["fifth"] = 5,
        [ArabicFolding.Fold("الأول")] = 1, [ArabicFolding.Fold("الأولى")] = 1, [ArabicFolding.Fold("الثاني")] = 2, [ArabicFolding.Fold("التاني")] = 2,
        [ArabicFolding.Fold("الثانية")] = 2, [ArabicFolding.Fold("التانية")] = 2, [ArabicFolding.Fold("الثالث")] = 3, [ArabicFolding.Fold("التالت")] = 3,
        [ArabicFolding.Fold("الثالثة")] = 3, [ArabicFolding.Fold("التالتة")] = 3, [ArabicFolding.Fold("الرابع")] = 4, [ArabicFolding.Fold("الرابعة")] = 4,
        [ArabicFolding.Fold("الخامس")] = 5, [ArabicFolding.Fold("الخامسة")] = 5,
    };

    /// <summary>The command, or null if this isn't about a book. <paramref name="choosing"/> is true just after a
    /// numbered list was read out, when "2" on its own means "open the second one".</summary>
    public static BookCommand? Parse(string utterance, bool choosing = false)
    {
        if (string.IsNullOrWhiteSpace(utterance) || utterance.Length > MaxChars)
        {
            return null;
        }

        string[] raw = utterance.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var words = new List<(string Folded, int Raw)>();
        for (int i = 0; i < raw.Length; i++)
        {
            string folded = ArabicFolding.Fold(raw[i]);
            if (folded.Length > 0)
            {
                foreach (string part in folded.Split(' '))
                {
                    words.Add((part, i));
                }
            }
        }

        StripFillers(words);
        if (words.Count == 0)
        {
            return null;
        }

        string[] plain = words.Select(w => w.Folded).ToArray();
        if (choosing && ParsePick(plain) is { } pick)
        {
            return new BookCommand(BookCommandKind.Pick, pick);
        }

        if (Exact.TryGetValue(string.Join(' ', plain), out BookCommandKind kind))
        {
            return new BookCommand(kind);
        }

        if (AfterPrefix(plain, NotePrefixes) is { } noteAt && noteAt < plain.Length)
        {
            return new BookCommand(BookCommandKind.Note, Text: string.Join(' ', raw[words[noteAt].Raw..]).TrimStart(':', ' ', '،', ','));
        }

        if (AfterPrefix(plain, PagePrefixes) is { } pageAt && ParseNumber(plain[pageAt..]) is { } page)
        {
            return new BookCommand(BookCommandKind.GoToPage, page);
        }

        if (AfterPrefix(plain, TimerPrefixes) is { } timerAt && ParseMinutes(plain[timerAt..]) is { } minutes)
        {
            return new BookCommand(BookCommandKind.SleepTimer, minutes);
        }

        if (AfterPrefix(plain, FindPrefixes) is { } findAt)
        {
            int from = AfterPrefix(plain[findAt..], FindJoiners) is { } joiner ? findAt + joiner : findAt;
            if (from < plain.Length)
            {
                return new BookCommand(BookCommandKind.FindBook, Text: string.Join(' ', raw[words[from].Raw..]));
            }
        }

        return null;
    }

    /// <summary>Spoken minutes such as "10 minutes", "half an hour", "ربع ساعة" or "دقيقتين".</summary>
    public static int? ParseMinutes(string[] words)
    {
        if (words.Length == 0)
        {
            return null;
        }

        string all = string.Join(' ', words);
        if (Durations.TryGetValue(all, out int fixedMinutes))
        {
            return fixedMinutes;
        }

        string unit = words[^1];
        if (words.Length < 2 || ParseNumber(words[..^1]) is not { } n || n is < 1 or > 600)
        {
            return null;
        }

        if (MinuteUnits.Contains(unit))
        {
            return n;
        }

        return HourUnits.Contains(unit) && n <= 10 ? n * 60 : null;
    }

    private static readonly Dictionary<string, int> Durations = new(StringComparer.Ordinal)
    {
        ["half an hour"] = 30, ["an hour"] = 60, ["one hour"] = 60, ["a minute"] = 1, ["quarter of an hour"] = 15, ["a quarter of an hour"] = 15,
        [ArabicFolding.Fold("ربع ساعة")] = 15, [ArabicFolding.Fold("نص ساعة")] = 30, [ArabicFolding.Fold("نصف ساعة")] = 30,
        [ArabicFolding.Fold("ساعة")] = 60, [ArabicFolding.Fold("ساعتين")] = 120, [ArabicFolding.Fold("دقيقة")] = 1, [ArabicFolding.Fold("دقيقتين")] = 2,
    };

    private static readonly HashSet<string> MinuteUnits = new(new[] { "minute", "minutes", "min", "mins", "دقيقة", "دقايق", "دقائق" }.Select(ArabicFolding.Fold), StringComparer.Ordinal);
    private static readonly HashSet<string> HourUnits = new(new[] { "hour", "hours", "ساعة", "ساعات" }.Select(ArabicFolding.Fold), StringComparer.Ordinal);

    private static int? ParsePick(string[] words)
    {
        int from = AfterPrefix(words, PickPrefixes) ?? 0;
        string[] rest = words[from..];
        if (rest.Length == 1 && Ordinals.TryGetValue(rest[0], out int ordinal))
        {
            return ordinal;
        }

        return ParseNumber(rest);
    }

    private static int? ParseNumber(string[] words)
    {
        if (words.Length == 0)
        {
            return null;
        }

        if (words.Length == 1)
        {
            string w = words[0];
            if (w.All(char.IsAsciiDigit) || w.All(c => char.GetNumericValue(c) >= 0 && char.IsDigit(c)))
            {
                int value = 0;
                foreach (char c in w)
                {
                    value = (value * 10) + (int)char.GetNumericValue(c);
                    if (value > 100_000)
                    {
                        return null;
                    }
                }

                return value;
            }

            return NumberWords.TryGetValue(w, out int n) ? n : null;
        }

        // "thirty four", "اربعة وتلاتين": add up the parts.
        int total = 0;
        foreach (string word in words)
        {
            string w = word.StartsWith('و') && word.Length > 1 ? word[1..] : word;
            if (w is "and" or "و")
            {
                continue;
            }

            if (!NumberWords.TryGetValue(w, out int part) && !NumberWords.TryGetValue(word, out part))
            {
                return null;
            }

            total += part;
        }

        return total;
    }

    private static int? AfterPrefix(string[] words, string[][] prefixes)
    {
        foreach (string[] prefix in prefixes)
        {
            if (prefix.Length <= words.Length && words.AsSpan(0, prefix.Length).SequenceEqual(prefix))
            {
                return prefix.Length;
            }
        }

        return null;
    }

    private static void StripFillers(List<(string Folded, int Raw)> words)
    {
        bool changed = true;
        while (changed && words.Count > 0)
        {
            changed = false;
            foreach (string[] filler in Leading)
            {
                if (filler.Length < words.Count && words.Take(filler.Length).Select(w => w.Folded).SequenceEqual(filler))
                {
                    words.RemoveRange(0, filler.Length);
                    changed = true;
                    break;
                }
            }

            foreach (string[] filler in Trailing)
            {
                if (filler.Length < words.Count && words.Skip(words.Count - filler.Length).Select(w => w.Folded).SequenceEqual(filler))
                {
                    words.RemoveRange(words.Count - filler.Length, filler.Length);
                    changed = true;
                    break;
                }
            }
        }
    }

    private static string[][] Folded(string[] phrases) => phrases
        .Select(p => ArabicFolding.Fold(p).Split(' '))
        .OrderByDescending(p => p.Length)
        .ToArray();

    private static Dictionary<string, BookCommandKind> BuildExact()
    {
        var map = new Dictionary<string, BookCommandKind>(StringComparer.Ordinal);
        void Add(BookCommandKind kind, params string[] phrases)
        {
            foreach (string phrase in phrases)
            {
                map[ArabicFolding.Fold(phrase)] = kind;
            }
        }

        Add(BookCommandKind.NextPage, "next page", "the next page", "go to the next page", "turn the page", "skip this page", "الصفحة التالية", "الصفحة اللي بعدها", "الصفحة الجاية", "الصفحة اللي بعد كده", "الصفحة بعدها", "اللي بعدها", "صفحة كمان", "اقلب الصفحة", "روح للصفحة اللي بعدها");
        Add(BookCommandKind.PreviousPage, "previous page", "the previous page", "go back a page", "back a page", "last page please", "الصفحة السابقة", "الصفحة اللي قبلها", "الصفحة قبلها", "ارجع صفحة", "الصفحة اللي فاتت", "رجعني صفحة");
        Add(BookCommandKind.RepeatPage, "read this page again", "read the page again", "repeat this page", "repeat the page", "start of the page", "from the top of the page", "اقرأ الصفحة دي تاني", "اقرا الصفحة دي تاني", "عيد الصفحة", "اعد الصفحة", "من أول الصفحة");
        Add(BookCommandKind.WhereInBook, "what page am i on", "what page is this", "which page am i on", "where am i in the book", "how far am i", "how much is left", "في صفحة كام", "أنا في صفحة كام", "احنا في صفحة كام", "إحنا في صفحة كام", "وصلت لفين", "أنا وصلت لفين", "كام صفحة فاضلة", "فاضل كام صفحة", "في أي صفحة أنا");
        Add(BookCommandKind.Continue, "continue", "continue reading", "keep reading", "resume reading", "read on", "carry on", "go on", "continue the book", "continue my book", "read my book", "carry on reading", "كمل", "كمل القراءة", "كمل قراءة", "تابع القراءة", "واصل القراءة", "كمل القراية", "كمل قراية", "كمل اقرا", "كمل الكتاب", "كمل قراءة الكتاب", "استمر في القراءة", "تابع", "واصل");
        Add(BookCommandKind.Mark, "mark this", "mark this page", "mark here", "bookmark", "bookmark this", "bookmark this page", "add a bookmark", "علم هنا", "حط علامة", "حط علامة هنا", "علم الصفحة دي", "علم على الصفحة دي", "ضع علامة", "ضع علامة هنا");
        Add(BookCommandKind.ReadNotes, "read my notes", "read me my notes", "read the notes", "my notes", "read my bookmarks", "what are my notes", "اقرأ ملاحظاتي", "اقرا ملاحظاتي", "اقرالي ملاحظاتي", "اقرأ لي ملاحظاتي", "ملاحظاتي", "علاماتي");
        Add(BookCommandKind.CancelTimer, "cancel the timer", "cancel the sleep timer", "no timer", "stop the timer", "الغي المؤقت", "إلغي المؤقت", "الغاء المؤقت", "من غير مؤقت");
        Add(BookCommandKind.ReadingJournal, "what was i reading", "what have i been reading", "reading journal", "my reading journal", "where did i stop", "كنت بقرأ إيه", "كنت بقرا ايه", "كنت بقرأ ايه", "وقفت فين", "أنا وقفت فين", "كنت واقف فين");
        return map;
    }

    private static Dictionary<string, int> BuildNumbers()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(int n, params string[] names)
        {
            foreach (string name in names)
            {
                map[ArabicFolding.Fold(name)] = n;
            }
        }

        Add(1, "one", "a", "واحد", "واحدة"); Add(2, "two", "اتنين", "اثنين"); Add(3, "three", "تلاتة", "ثلاثة", "تلات", "ثلاث"); Add(4, "four", "اربعة", "أربعة", "اربع");
        Add(5, "five", "خمسة", "خمس"); Add(6, "six", "ستة", "ست"); Add(7, "seven", "سبعة", "سبع"); Add(8, "eight", "تمانية", "ثمانية", "تمن");
        Add(9, "nine", "تسعة", "تسع"); Add(10, "ten", "عشرة", "عشر"); Add(11, "eleven", "حداشر", "احد عشر"); Add(12, "twelve", "اتناشر");
        Add(13, "thirteen", "تلتاشر"); Add(14, "fourteen", "اربعتاشر"); Add(15, "fifteen", "خمستاشر"); Add(16, "sixteen", "ستاشر"); Add(17, "seventeen", "سبعتاشر");
        Add(18, "eighteen", "تمنتاشر"); Add(19, "nineteen", "تسعتاشر"); Add(20, "twenty", "عشرين"); Add(30, "thirty", "تلاتين", "ثلاثين"); Add(40, "forty", "اربعين", "أربعين");
        Add(50, "fifty", "خمسين"); Add(60, "sixty", "ستين"); Add(70, "seventy", "سبعين"); Add(80, "eighty", "تمانين", "ثمانين"); Add(90, "ninety", "تسعين");
        Add(100, "hundred", "مية", "مئة", "ميه"); Add(200, "ميتين");
        return map;
    }
}
