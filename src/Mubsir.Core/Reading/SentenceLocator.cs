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
