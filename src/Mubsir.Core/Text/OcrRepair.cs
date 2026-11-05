namespace Mubsir.Core.Text;

/// <summary>Puts the two OCR engines together, and fills letters a PDF lost with what OCR read on the same page.</summary>
public static class OcrRepair
{
    private const double SameBox = 0.5;

    public static bool HasLatin(IReadOnlyList<IReadOnlyList<OcrWordBox>> lines) => lines.Any(l => l.Any(IsLatin));

    /// <summary>
    /// The Arabic engine reads Arabic well and damages English ("paper." becomes ".aper"). Its English words, and
    /// any whole line that is mostly English, take the English engine's words from the same place on the page.
    /// </summary>
    public static string Merge(IReadOnlyList<IReadOnlyList<OcrWordBox>> arabicEngine, IReadOnlyList<OcrWordBox> latinEngine)
    {
        ArgumentNullException.ThrowIfNull(arabicEngine);
        ArgumentNullException.ThrowIfNull(latinEngine);
        var used = new HashSet<OcrWordBox>();
        var lines = new List<List<OcrWordBox>>();
        foreach (IReadOnlyList<OcrWordBox> line in arabicEngine)
        {
            bool mostlyLatin = line.Count(IsLatin) * 2 > line.Count;
            var merged = new List<OcrWordBox>(line.Count);
            foreach (OcrWordBox word in line)
            {
                OcrWordBox[] same = latinEngine.Where(l => Overlap(word, l) >= SameBox).ToArray();
                if ((!mostlyLatin && !IsLatin(word)) || same.Length == 0)
                {
                    merged.Add(word);
                    continue;
                }

                merged.AddRange(same.Where(used.Add));
            }

            if (merged.Count > 0)
            {
                lines.Add(merged);
            }
        }

        OcrWordBox[] missed = latinEngine.Where(l => IsLatin(l) && !used.Contains(l) && !lines.Any(line => line.Any(w => Overlap(w, l) >= SameBox))).ToArray();
        lines.AddRange(OcrLineOrder.GroupLines(missed).Select(l => l.ToList()));
        return string.Join('\n', lines.OrderBy(l => l.Min(w => w.Y)).Select(OcrLineOrder.ToLogicalLine).Where(t => t.Length > 0));
    }

    /// <summary>Replaces each word holding a lost letter with the word OCR read in the matching line.</summary>
    public static string FillLostLetters(string text, string? ocr)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains(PdfPageText.Unknown, StringComparison.Ordinal))
        {
            return text;
        }

        // OCR and the PDF break lines in different places, so this looks across the whole page.
        string[] page = (ocr ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(PdfPageText.Unknown, StringComparison.Ordinal))
            {
                lines[i] = FillLine(lines[i], page);
            }
        }

        return PdfPageText.WithoutUnknown(string.Join('\n', lines));
    }

    /// <summary>The number of letters to change to turn one text into another.</summary>
    public static int Distance(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            row[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }

    private static string FillLine(string line, string[] page)
    {
        string[] words = line.Split(' ');
        for (int w = 0; w < words.Length; w++)
        {
            int lost = words[w].Count(c => c == PdfPageText.Unknown);
            if (lost == 0)
            {
                continue;
            }

            string key = Letters(words[w]);
            string before = w > 0 ? Letters(words[w - 1]) : string.Empty;
            string after = w + 1 < words.Length ? Letters(words[w + 1]) : string.Empty;

            // A neighbouring word has to match too, because short words look alike.
            string? chosen = Enumerable.Range(0, page.Length)
                .Where(i => Letters(page[i]).Length >= key.Length + lost - 1 && Distance(key, Letters(page[i])) <= lost)
                .Where(i => (before.Length > 0 && i > 0 && Letters(page[i - 1]) == before) || (after.Length > 0 && i + 1 < page.Length && Letters(page[i + 1]) == after))
                .OrderBy(i => Distance(key, Letters(page[i])))
                .Select(i => page[i])
                .FirstOrDefault();
            if (chosen is not null)
            {
                words[w] = chosen;
            }
        }

        return string.Join(' ', words);
    }

    /// <summary>Hamza and tashkeel don't count: OCR often reads قرأ as قرا.</summary>
    private static string Letters(string text) => new(ArabicFolding.Fold(text).Where(char.IsLetterOrDigit).ToArray());

    private static bool IsLatin(OcrWordBox word)
    {
        var (arabic, other) = ScriptInfo.CountLetters(word.Text);
        return other > 0 && other >= arabic;
    }

    private static double Overlap(OcrWordBox a, OcrWordBox b)
    {
        double wide = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        double high = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        double smaller = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return wide <= 0 || high <= 0 || smaller <= 0 ? 0 : wide * high / smaller;
    }
}
