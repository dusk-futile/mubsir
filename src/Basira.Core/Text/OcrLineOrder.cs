namespace Basira.Core.Text;

/// <summary>One word recognized by OCR, with its bounding box in screen pixels (Y grows downward).</summary>
public sealed record OcrWordBox(string Text, double X, double Y, double Width, double Height);

/// <summary>
/// Puts OCR words into reading order. Windows OCR returns Arabic words in reversed order; this sorts them
/// right-to-left by position while keeping embedded Latin words and numbers left-to-right.
/// </summary>
public static class OcrLineOrder
{
    private enum Direction
    {
        Neutral,
        Rtl,
        Ltr,
    }

    /// <summary>
    /// Joins one line's words in logical order. Arabic-dominant lines read right to left, with consecutive
    /// Latin/number words kept left to right; English-dominant lines read left to right, with consecutive
    /// Arabic words kept right to left.
    /// </summary>
    public static string ToLogicalLine(IReadOnlyList<OcrWordBox> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count == 0)
        {
            return "";
        }

        var visual = words.Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderBy(w => w.X)
            .ThenBy(w => w.Y)
            .ToList();

        int arabic = 0, latin = 0;
        foreach (var word in visual)
        {
            var (a, l) = ScriptInfo.CountLetters(word.Text);
            arabic += a;
            latin += l;
        }

        bool rtlBase = arabic > 0 && arabic >= latin;
        var groups = GroupEmbedded(visual, rtlBase ? Direction.Ltr : Direction.Rtl);
        var ordered = new List<string>(visual.Count);
        if (rtlBase)
        {
            for (int g = groups.Count - 1; g >= 0; g--)
            {
                ordered.AddRange(groups[g].Select(w => w.Text.Trim()));
            }
        }
        else
        {
            foreach (var group in groups)
            {
                bool embeddedRtl = group.Count > 1 || Classify(group[0].Text) == Direction.Rtl;
                IEnumerable<OcrWordBox> inOrder = embeddedRtl ? Enumerable.Reverse(group) : group;
                ordered.AddRange(inOrder.Select(w => w.Text.Trim()));
            }
        }

        return string.Join(' ', ordered);
    }

    /// <summary>
    /// Clusters words into lines by vertical overlap (at least half the smaller height), top to bottom.
    /// Words in each line are in left-to-right visual order; use <see cref="ToLogicalLine"/> for reading order.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<OcrWordBox>> GroupLines(IReadOnlyList<OcrWordBox> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var lines = new List<LineBuilder>();
        foreach (var word in words.OrderBy(w => w.Y + (Math.Max(w.Height, 1) / 2)).ThenBy(w => w.X))
        {
            double top = word.Y, bottom = word.Y + Math.Max(word.Height, 1);
            LineBuilder? best = null;
            double bestOverlap = 0;
            foreach (var line in lines)
            {
                double overlap = Math.Min(bottom, line.Bottom) - Math.Max(top, line.Top);
                double needed = 0.5 * Math.Min(bottom - top, line.Bottom - line.Top);
                if (overlap >= needed && overlap > bestOverlap)
                {
                    best = line;
                    bestOverlap = overlap;
                }
            }

            if (best is null)
            {
                best = new LineBuilder(top, bottom);
                lines.Add(best);
            }

            best.Add(word, top, bottom);
        }

        return lines
            .OrderBy(l => l.CenterY)
            .Select(l => (IReadOnlyList<OcrWordBox>)l.Words.OrderBy(w => w.X).ToList())
            .ToList();
    }

    /// <summary>All lines in reading order, joined with newlines.</summary>
    public static string ToLogicalText(IReadOnlyList<OcrWordBox> words) =>
        string.Join('\n', GroupLines(words).Select(ToLogicalLine));

    private static Direction Classify(string text)
    {
        var (arabic, latin) = ScriptInfo.CountLetters(text);
        if (arabic > 0)
        {
            return Direction.Rtl;
        }

        return latin > 0 || text.Any(char.IsDigit) ? Direction.Ltr : Direction.Neutral;
    }

    /// <summary>
    /// Splits the visual sequence into groups: runs of <paramref name="embedded"/>-direction words (with any
    /// neutral words between them) form one group; every other word is its own group.
    /// </summary>
    private static List<List<OcrWordBox>> GroupEmbedded(List<OcrWordBox> visual, Direction embedded)
    {
        var dirs = visual.Select(w => Classify(w.Text)).ToArray();
        var groups = new List<List<OcrWordBox>>();
        int i = 0;
        while (i < visual.Count)
        {
            if (dirs[i] != embedded)
            {
                groups.Add([visual[i]]);
                i++;
                continue;
            }

            int end = i;
            for (int j = i + 1; j < visual.Count; j++)
            {
                if (dirs[j] == embedded)
                {
                    end = j;
                }
                else if (dirs[j] != Direction.Neutral)
                {
                    break;
                }
            }

            groups.Add(visual.GetRange(i, end - i + 1));
            i = end + 1;
        }

        return groups;
    }

    private sealed class LineBuilder(double top, double bottom)
    {
        private double _centerSum;

        public double Top { get; private set; } = top;

        public double Bottom { get; private set; } = bottom;

        public List<OcrWordBox> Words { get; } = [];

        public double CenterY => _centerSum / Words.Count;

        public void Add(OcrWordBox word, double top, double bottom)
        {
            Words.Add(word);
            _centerSum += (top + bottom) / 2;
            Top = Math.Min(Top, top);
            Bottom = Math.Max(Bottom, bottom);
        }
    }
}
