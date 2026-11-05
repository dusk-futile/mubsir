namespace Mubsir.Core.Text;

/// <summary>Puts the two OCR engines together, and fills letters a PDF lost with what OCR read on the same page.</summary>
public static class OcrRepair
{
    private const double SameBox = 0.5;
    private const double SameLine = 0.5;

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

        string[] ocrLines = (ocr ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(PdfPageText.Unknown, StringComparison.Ordinal))
            {
                lines[i] = FillLine(lines[i], ocrLines);
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

    private static string FillLine(string line, string[] ocrLines)
    {
        string flat = Letters(line);
        string? match = null;
        double best = SameLine;
        foreach (string candidate in ocrLines)
        {
            string other = Letters(candidate);
            int longest = Math.Max(flat.Length, other.Length);
            double likeness = longest == 0 ? 0 : 1 - ((double)Distance(flat, other) / longest);
            if (likeness >= best)
            {
                best = likeness;
                match = candidate;
            }
        }

        if (match is null)
        {
            return line;
        }

        string[] options = match.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] words = line.Split(' ');
        for (int w = 0; w < words.Length; w++)
        {
            int lost = words[w].Count(c => c == PdfPageText.Unknown);
            if (lost == 0)
            {
                continue;
            }

            string key = Letters(words[w]);
            string? chosen = options
                .Where(o => Letters(o).Length >= key.Length + lost - 1 && Distance(key, Letters(o)) <= lost)
                .OrderBy(o => Distance(key, Letters(o)))
                .ThenBy(o => Math.Abs(((double)Array.IndexOf(options, o) / options.Length) - ((double)w / words.Length)))
                .FirstOrDefault();
            if (chosen is not null)
            {
                words[w] = chosen;
            }
        }

        return string.Join(' ', words);
    }

    private static string Letters(string text) => new(text.Where(char.IsLetterOrDigit).ToArray());

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
