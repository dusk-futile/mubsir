using Mubsir.Core.Common;

namespace Mubsir.Core.Agent;

/// <summary>
/// Turns what Mubsir said as a step started into what it says once the step has finished:
/// "Opening Settings" becomes "Opened Settings", "أفتح الإعدادات" becomes "فتحت الإعدادات". Only the verbs
/// change, never a name in quotes. Anything it doesn't know becomes "Done." / "تم.".
/// </summary>
public static class StepWords
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Opening"] = "Opened",
        ["Clicking"] = "Clicked",
        ["Pressing"] = "Pressed",
        ["Typing"] = "Typed",
        ["Scrolling"] = "Scrolled",
        ["Going"] = "Went",
        ["Moving"] = "Moved",
        ["Switching"] = "Switched",
        ["Listing"] = "Listed",
        ["Making"] = "Made",
        ["Reading"] = "Read",
        ["Searching"] = "Searched",
        ["searching"] = "searched",
        ["Copying"] = "Copied",
        ["Renaming"] = "Renamed",
        ["Checking"] = "Checked",
        ["Setting"] = "Set",
        ["Looking"] = "Looked",
        ["Taking"] = "Took",
        ["Skipping"] = "Skipped",
    };

    private static readonly Dictionary<string, string> Arabic = new(StringComparer.Ordinal)
    {
        ["أفتح"] = "فتحت",
        ["أضغط"] = "ضغطت",
        ["أكتب"] = "كتبت",
        ["أذهب"] = "رحت",
        ["أصعد"] = "طلعت",
        ["أنزل"] = "نزلت",
        ["أمرر"] = "مررت",
        ["أنقل"] = "نقلت",
        ["أنتقل"] = "انتقلت",
        ["أعدّ"] = "عددت",
        ["أعمل"] = "عملت",
        ["أقرأ"] = "قرأت",
        ["ألتقط"] = "التقطت",
        ["أصفها"] = "وصفتها",
        ["أدور"] = "دورت",
        ["أعرض"] = "عرضت",
        ["أبحث"] = "بحثت",
        ["أنسخ"] = "نسخت",
        ["أغيّر"] = "غيّرت",
        ["أفحص"] = "فحصت",
        ["أضبط"] = "ضبطت",
    };

    public static string Finished(string? started, Lang lang)
    {
        bool ar = lang == Lang.Arabic;
        string done = AgentText.StepDone(lang);
        if (string.IsNullOrWhiteSpace(started))
        {
            return done;
        }

        // "Checking Windows Update; this can take a minute": the note about time no longer applies.
        string text = started.Trim();
        int note = text.IndexOfAny([';', '؛']);
        if (note < 0 && ar)
        {
            note = text.IndexOf("، قد", StringComparison.Ordinal);
        }

        if (note > 0)
        {
            text = text[..note].TrimEnd();
        }

        Dictionary<string, string> verbs = ar ? Arabic : English;
        string[] words = text.Split(' ');
        bool quoted = false;
        bool changed = false;
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            bool opensQuote = word.StartsWith('"') || word.StartsWith('«');
            if (!quoted && !opensQuote && (i == 0 || words[i - 1].EndsWith(',') || ar))
            {
                // Arabic joins a second verb with و: "أفتح جوجل وأبحث" becomes "فتحت جوجل وبحثت".
                string prefix = ar && word.Length > 1 && word[0] == 'و' && verbs.ContainsKey(word[1..]) ? "و" : string.Empty;
                if (verbs.TryGetValue(word[prefix.Length..], out string? past))
                {
                    words[i] = prefix + past;
                    changed = true;
                }
            }

            if (opensQuote)
            {
                quoted = true;
            }

            if (quoted && (word.EndsWith('"') || word.EndsWith('»')) && (word.Length > 1 || !opensQuote))
            {
                quoted = false;
            }
        }

        if (!changed)
        {
            return done;
        }

        string result = string.Join(' ', words).TrimEnd('.', '،', ',');
        return result + ".";
    }
}
