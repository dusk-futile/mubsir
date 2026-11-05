using Mubsir.Core.Reading;

namespace Mubsir.App.Screen.Documents;

/// <summary>A book's pages for reading aloud. Scanned pages are read with OCR only as the reader reaches them.</summary>
internal sealed class DocumentPages(
    IReadOnlyList<string> pages, IReadOnlyCollection<int> pending, string? path, DocumentReader documents, int ocrPagesAtATime) : IBookPages
{
    private readonly string[] _pages = [.. pages];
    private readonly HashSet<int> _pending = [.. pending];

    public int Count => _pages.Length;

    public async Task<string?> GetAsync(int page, CancellationToken ct)
    {
        if (!_pending.Contains(page))
        {
            return _pages[page - 1];
        }

        if (path is null)
        {
            return null;
        }

        try
        {
            DocumentText doc = await documents.ReadAsync(path, page, ocrPagesAtATime, progress: null, ct).ConfigureAwait(false);
            var stillPending = doc.PendingOcrPages.ToHashSet();
            foreach (int done in _pending.Where(p => !stillPending.Contains(p) && p <= doc.Pages.Count).ToArray())
            {
                _pages[done - 1] = doc.Pages[done - 1];
                _pending.Remove(done);
            }
        }
        catch (DocumentReadException)
        {
            return null;
        }

        return _pending.Contains(page) ? null : _pages[page - 1];
    }
}
