using Mubsir.Core.Agent;
using Mubsir.Core.Text;

namespace Mubsir.App.Orchestration;

internal static class RequestModeClassifier
{
    private static readonly string[] ArabicActionStems = Fold(
    [
        "افتح", "افتحي", "شغل", "اضغط", "دوس", "اكتب", "ابعت", "ابعث", "ارسل", "ابحث", "دور", "روح", "اذهب", "انتقل",
        "انقل", "احذف", "امسح", "اغلق", "سكر", "قفل", "حمل", "نزل", "انسخ", "الصق", "سمي", "غير", "حدث", "رد", "العب",
        "اختار", "اختر", "ادخل", "سجل", "ثبت", "اطلب", "ادفع", "حول", "ذكرني", "احفظ", "تذكر", "اتصل", "كلم", "عدل",
        "ضيف", "اضف", "شيل", "اقفل", "وقف", "كبر", "صغر", "اعمل", "جهز", "ابدا", "رتب", "نظف", "فرغ", "اظهر",
    ]);

    private static readonly HashSet<string> EnglishActionWords = new(StringComparer.Ordinal)
    {
        "open", "play", "click", "press", "type", "send", "search", "go", "navigate", "move", "delete", "remove", "close",
        "download", "copy", "paste", "rename", "change", "update", "reply", "select", "choose", "switch", "login", "log",
        "sign", "install", "buy", "pay", "order", "remember", "call", "write", "fill", "start", "scroll", "find", "save",
        "upload", "pause", "skip", "turn", "set", "create", "make", "add", "mute", "unmute", "restart", "empty", "clean",
    };

    private static readonly string[] ArabicQuestionWords = Fold(
    [
        "ماذا", "ما", "مادا", "ايه", "ايش", "شو", "شنو", "لماذا", "ليش", "ليه", "هل", "كم", "مين", "من", "فين", "اين",
        "وين", "متى", "امتى", "كيف", "ازاي", "كيفاش", "اشرح", "لخص", "صف", "وصف", "ملخص", "عن", "يتحدث", "بيتكلم", "يتكلم",
        "معنى", "يعني",
    ]);

    private static readonly HashSet<string> EnglishQuestionWords = new(StringComparer.Ordinal)
    {
        "what", "what's", "whats", "who", "who's", "why", "how", "when", "where", "which", "is", "are", "does", "do",
        "did", "can", "could", "explain", "summarize", "summarise", "describe", "tell", "meaning", "mean",
    };

    public static BrainMode Classify(string? request)
    {
        if (string.IsNullOrWhiteSpace(request))
        {
            return BrainMode.Agent;
        }

        bool questionMark = request.Contains('?', StringComparison.Ordinal) || request.Contains('؟', StringComparison.Ordinal);
        string[] words = ArabicFolding.Fold(request).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return BrainMode.Agent;
        }

        if (words.Any(IsActionWord) || RequestDomains.Extract(request).Count > 0)
        {
            return BrainMode.Agent;
        }

        bool question = questionMark || words.Take(3).Any(IsQuestionWord);
        return question ? BrainMode.QuickAnswer : BrainMode.Agent;
    }

    private static bool IsActionWord(string word)
    {
        if (EnglishActionWords.Contains(word))
        {
            return true;
        }

        string w = StripConjunction(word);
        foreach (string stem in ArabicActionStems)
        {
            if (stem.Length <= 2 ? w == stem : w.StartsWith(stem, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuestionWord(string word)
    {
        if (EnglishQuestionWords.Contains(word))
        {
            return true;
        }

        string w = StripConjunction(word);
        return ArabicQuestionWords.Contains(w, StringComparer.Ordinal);
    }

    private static string StripConjunction(string word) =>
        word.Length > 3 && word[0] is 'و' or 'ف' ? word[1..] : word;

    private static string[] Fold(string[] words) => words.Select(ArabicFolding.Fold).Where(w => w.Length > 0).Distinct().ToArray();
}
