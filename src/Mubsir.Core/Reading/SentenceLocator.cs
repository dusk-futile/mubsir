using Mubsir.Core.Text;

namespace Mubsir.Core.Reading;

public static class SentenceLocator
{
    /// <summary>
    /// Where each spoken sentence starts in the page. A sentence that can't be found (the splitter changed its
    /// spacing, say) gets the end of the one before, so resuming there reads it again and never skips it.
    /// </summary>
    public static int[] Locate(string page, IReadOnlyList<string> sentences, int start)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(sentences);
        var offsets = new int[sentences.Count];
        int cursor = Math.Clamp(start, 0, page.Length);
        for (int i = 0; i < offsets.Length; i++)
        {
            int found = sentences[i].Length > 0 ? page.IndexOf(sentences[i], cursor, StringComparison.Ordinal) : -1;
            if (found >= 0)
            {
                offsets[i] = found;
                cursor = found + sentences[i].Length;
            }
            else
            {
                offsets[i] = cursor;
            }
        }

        return offsets;
    }
}

/// <summary>Where the lines, sentences and paragraphs of a page start, so the reader can ask for one by number.</summary>
public static class PageParts
{
    private const int EndingSentences = 3;

    public static int[] Starts(string page, BookCommandKind part)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (part == BookCommandKind.GoToSentence)
        {
            return SentenceLocator.Locate(page, SentenceSplitter.Split(page), 0);
        }

        // A PDF page has no blank lines, and each line of a Word page is a paragraph, so there a paragraph is a line.
        bool blocks = part == BookCommandKind.GoToParagraph && page.Contains("\n\n", StringComparison.Ordinal);
        var starts = new List<int>();
        bool open = false;
        int at = 0;
        foreach (string line in page.Split('\n'))
        {
            int text = line.Length - line.TrimStart().Length;
            if (text == line.Length)
            {
                open = false;
            }
            else if (!blocks || !open)
            {
                starts.Add(at + text);
                open = true;
            }

            at += line.Length + 1;
        }

        return [.. starts];
    }

    /// <summary>The first line of the page that has words on it.</summary>
    public static string? Heading(string page) =>
        page.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

    /// <summary>Where the last few sentences of the page start.</summary>
    public static int Ending(string page)
    {
        int[] sentences = Starts(page, BookCommandKind.GoToSentence);
        return sentences.Length == 0 ? 0 : sentences[^Math.Min(EndingSentences, sentences.Length)];
    }
}
