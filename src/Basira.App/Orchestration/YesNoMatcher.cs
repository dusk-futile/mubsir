using Basira.Core.Text;

namespace Basira.App.Orchestration;

internal enum SpokenAnswer
{
    Unclear,
    Yes,
    No,
}

/// <summary>Spoken answers to a confirmation: "نعم / أيوه / yes" or "لا / إلغاء / no" (pure, testable).</summary>
internal static class YesNoMatcher
{
    private static readonly string[] YesPhrases = Fold(
    [
        "نعم", "أيوه", "ايوه", "أيوة", "ايوا", "أيوا", "اه", "آه", "ايه", "إيه", "تمام", "موافق", "موافقه", "اكيد", "أكيد",
        "طبعا", "طبعًا", "يلا", "ماشي", "اوكي", "أوكي", "حاضر", "صح", "بلى", "اي", "إي", "yes", "yeah", "yep", "ok", "okay", "sure",
        "confirm", "go ahead", "do it",
    ]);

    private static readonly string[] NoPhrases = Fold(
    [
        "لا", "لأ", "لاء", "لا لا", "مش عايز", "مش عاوز", "ما بدي", "مابدي", "ما ابغى", "لا شكرا", "إلغاء", "الغاء", "الغي", "ألغي",
        "بلاش", "خلاص لا", "توقف", "وقف", "no", "nope", "cancel", "stop", "don't", "dont", "do not",
    ]);

    // Words that may surround the answer without changing it.
    private static readonly HashSet<string> Fillers = new(Fold(
    [
        "يا", "بصيره", "بصيرة", "basira", "please", "من", "فضلك", "لو", "سمحت", "شكرا", "thanks", "thank", "you", "يعني",
    ]), StringComparer.Ordinal);

    public static SpokenAnswer Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SpokenAnswer.Unclear;
        }

        string folded = ArabicFolding.Fold(text);
        string core = string.Join(' ', folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Fillers.Contains(w)));
        if (core.Length == 0)
        {
            return SpokenAnswer.Unclear;
        }

        // "No" phrases win when both appear ("لا مش موافق"): declining is the safe reading.
        bool no = NoPhrases.Any(p => core == p || StartsWithWords(core, p));
        bool yes = YesPhrases.Any(p => core == p || StartsWithWords(core, p));
        return no ? SpokenAnswer.No : yes ? SpokenAnswer.Yes : SpokenAnswer.Unclear;
    }

    private static bool StartsWithWords(string text, string phrase) =>
        text.StartsWith(phrase, StringComparison.Ordinal) && (text.Length == phrase.Length || text[phrase.Length] == ' ')
        && text.Split(' ').Length <= phrase.Split(' ').Length + 2;

    private static string[] Fold(string[] words) => words.Select(ArabicFolding.Fold).Where(w => w.Length > 0).Distinct().ToArray();
}
