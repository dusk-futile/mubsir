using System.Globalization;
using System.Text;

namespace Mubsir.Core.Text;

/// <summary>One glyph as a PDF draws it. <see cref="Value"/> can hold several letters, such as a lam-alef ligature.</summary>
public sealed record PdfGlyph(string Value, double X, double Y, double Width, double Height);

/// <summary>
/// Rebuilds a page's reading order from where its glyphs sit, one glyph at a time. A ligature keeps its letters
/// in order, however the PDF drew the line. A letter the PDF has no text for becomes <see cref="Unknown"/>.
/// </summary>
public static class PdfPageText
{
    public const char Unknown = '�';
    private const double TooBrokenShare = 0.1;

    private enum Run
    {
        Neutral,
        Rtl,
        Ltr,
    }

    /// <summary>The page's text, or null if its glyphs have no usable positions and it needs another way in.</summary>
    public static string? Build(IReadOnlyList<PdfGlyph> glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        if (glyphs.Count == 0)
        {
            return "";
        }

        int bases = 0, noWidth = 0;
        foreach (PdfGlyph g in glyphs)
        {
            if (!string.IsNullOrWhiteSpace(g.Value) && !IsMark(g))
            {
                bases++;
                if (g.Width <= 0)
                {
                    noWidth++;
                }
            }
        }

        if (noWidth * 2 > bases)
        {
            return null;
        }

        List<List<PdfGlyph>> lines = SplitLines(glyphs);
        if (bases > 50 && lines.Count * 2 > bases)
        {
            return null;
        }

        return ArabicNormalizer.NormalizePresentationForms(string.Join('\n', lines.Select(ToLogicalLine).Where(l => l.Length > 0)));
    }

    /// <summary>So little of the page has text that OCR has to read all of it.</summary>
    public static bool TooBroken(string text)
    {
        int letters = 0, unknown = 0;
        foreach (char c in text)
        {
            if (c == Unknown)
            {
                unknown++;
            }
            else if (char.IsLetter(c))
            {
                letters++;
            }
        }

        return unknown > (letters + unknown) * TooBrokenShare;
    }

    public static string WithoutUnknown(string text) => text.Replace(Unknown.ToString(), string.Empty, StringComparison.Ordinal);

    private static bool IsMark(PdfGlyph glyph) =>
        glyph.Value.Length > 0 && (glyph.Value.All(c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) || (IsNoText(glyph.Value) && glyph.Width <= 0));

    private static bool IsNoText(string value) => value.All(c => c is '\0' or Unknown);

    private static List<List<PdfGlyph>> SplitLines(IReadOnlyList<PdfGlyph> glyphs)
    {
        var lines = new List<List<PdfGlyph>>();
        List<PdfGlyph>? line = null;
        PdfGlyph? previous = null;
        foreach (PdfGlyph g in glyphs)
        {
            if (string.IsNullOrEmpty(g.Value))
            {
                continue;
            }

            bool solid = !string.IsNullOrWhiteSpace(g.Value) && !IsMark(g);
            if (line is null || (solid && previous is not null && Math.Abs(g.Y - previous.Y) > 0.6 * Math.Max(g.Height, previous.Height)))
            {
                line = [];
                lines.Add(line);
            }

            line.Add(g);
            if (solid)
            {
                previous = g;
            }
        }

        return lines;
    }

    private static string ToLogicalLine(List<PdfGlyph> line)
    {
        List<Unit> units = ToUnits(line);
        var words = new List<Unit>(units.Count);
        double typical = units.Where(u => !u.IsSpace && u.Width > 0).Select(u => u.Width).Order().ElementAtOrDefault(units.Count(u => !u.IsSpace && u.Width > 0) / 2);
        Unit? before = null;
        foreach (Unit unit in units.OrderBy(u => u.X))
        {
            if (before is not null && !before.IsSpace && !unit.IsSpace && unit.X - (before.X + before.Width) > typical / 2)
            {
                words.Add(new Unit(" ", before.X + before.Width, 0));
            }

            if (!(unit.IsSpace && before is { IsSpace: true }))
            {
                words.Add(unit);
            }

            before = unit;
        }

        return Order(words).Trim();
    }

    /// <summary>
    /// Each mark joins its letter. A line drawn left to right has its marks just before their letters, as the
    /// reversed Arabic it is, and a line drawn right to left has them just after.
    /// </summary>
    private static List<Unit> ToUnits(List<PdfGlyph> line)
    {
        double trend = 0;
        PdfGlyph? last = null;
        foreach (PdfGlyph g in line)
        {
            if (string.IsNullOrWhiteSpace(g.Value) || IsMark(g))
            {
                continue;
            }

            if (last is not null)
            {
                trend += Math.Sign(g.X - last.X);
            }

            last = g;
        }

        var units = new List<Unit>(line.Count);
        var waiting = new StringBuilder();
        foreach (PdfGlyph g in line)
        {
            if (IsMark(g))
            {
                string mark = IsNoText(g.Value) ? string.Empty : g.Value;
                if (trend < 0 && units.Count > 0 && !units[^1].IsSpace)
                {
                    units[^1].Text += mark;
                }
                else
                {
                    waiting.Append(mark);
                }

                continue;
            }

            string text = IsNoText(g.Value) ? Unknown.ToString() : g.Value;
            bool space = string.IsNullOrWhiteSpace(text);
            var unit = new Unit(space ? " " : text + waiting, g.X, g.Width);
            if (!space)
            {
                waiting.Clear();
            }

            units.Add(unit);
        }

        return units;
    }

    private static string Order(List<Unit> visual)
    {
        Run[] runs = visual.Select(u => Classify(u.Text)).ToArray();
        int rtl = runs.Count(r => r == Run.Rtl), ltr = runs.Count(r => r == Run.Ltr);
        Run lineBase = rtl > 0 && rtl >= ltr ? Run.Rtl : Run.Ltr;
        for (int i = 0; i < runs.Length; i++)
        {
            if (runs[i] != Run.Neutral)
            {
                continue;
            }

            int end = i;
            while (end < runs.Length && runs[end] == Run.Neutral)
            {
                end++;
            }

            Run left = i > 0 ? runs[i - 1] : lineBase;
            Run right = end < runs.Length ? runs[end] : lineBase;
            Array.Fill(runs, left == right ? left : lineBase, i, end - i);
            i = end - 1;
        }

        var groups = new List<(Run Kind, List<Unit> Units)>();
        for (int i = 0; i < visual.Count; i++)
        {
            if (groups.Count == 0 || groups[^1].Kind != runs[i])
            {
                groups.Add((runs[i], []));
            }

            groups[^1].Units.Add(visual[i]);
        }

        if (lineBase == Run.Rtl)
        {
            groups.Reverse();
        }

        var text = new StringBuilder();
        foreach ((Run kind, List<Unit> units) in groups)
        {
            if (kind == Run.Rtl)
            {
                for (int i = units.Count - 1; i >= 0; i--)
                {
                    string value = units[i].Text;
                    text.Append(value.Length == 1 ? ArabicNormalizer.Mirror(value[0]) : value);
                }
            }
            else
            {
                foreach (Unit unit in units)
                {
                    text.Append(unit.Text);
                }
            }
        }

        return text.ToString();
    }

    private static Run Classify(string text)
    {
        foreach (char c in text)
        {
            if (c == Unknown || ScriptInfo.IsArabicStrong(c))
            {
                return Run.Rtl;
            }

            if (char.IsDigit(c) || ScriptInfo.IsNonArabicLetter(c))
            {
                return Run.Ltr;
            }
        }

        return Run.Neutral;
    }

    private sealed class Unit(string text, double x, double width)
    {
        public string Text { get; set; } = text;

        public double X { get; } = x;

        public double Width { get; } = width;

        public bool IsSpace => Text == " ";
    }
}
