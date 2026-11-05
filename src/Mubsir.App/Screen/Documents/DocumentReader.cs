using System.Diagnostics;
using Mubsir.Core.Library;
using Mubsir.Core.Logging;
using Mubsir.Core.Reading;
using Mubsir.Core.Text;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using PigDocument = UglyToad.PdfPig.PdfDocument;
using PigParsingOptions = UglyToad.PdfPig.ParsingOptions;
using WinPdfDocument = Windows.Data.Pdf.PdfDocument;

namespace Mubsir.App.Screen.Documents;

public sealed record DocumentText(
    string Kind,
    IReadOnlyList<string> Pages,
    int TotalChars,
    int ScannedPageCount,
    IReadOnlyList<int> PendingOcrPages);

public sealed class DocumentReadException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

internal sealed class DocumentReader(UiaScreenReader screen, IEventLog log)
{
    public const long MaxFileBytes = 300L * 1024 * 1024;
    public const int MaxTextChars = 5_000_000;

    private const double OcrRenderScale = 3.0;
    private const int CardPages = 3;
    private const int PageImageMaxSide = 1600;

    private static readonly TimeSpan PageOcrTimeout = TimeSpan.FromSeconds(30);

    public static bool IsSupported(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".text" or ".md" or ".csv" or ".docx" or ".pdf";

    public async Task<DocumentText> ReadAsync(
    string path, int firstOcrPage, int maxOcrPages, IProgress<(int Page, int Total)>? progress, CancellationToken ct)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new DocumentReadException("not_found");
        }

        if (info.Length > MaxFileBytes)
        {
            throw new DocumentReadException("too_large");
        }

        var ext = info.Extension.ToLowerInvariant();
        var clock = Stopwatch.StartNew();
        try
        {
            DocumentText result = ext switch
            {
                ".pdf" => await ReadPdfAsync(path, firstOcrPage, maxOcrPages, progress, ct).ConfigureAwait(false),
                ".docx" => await Task.Run(() => ReadDocx(path), ct).ConfigureAwait(false),
                ".txt" or ".text" or ".md" or ".csv" => await ReadPlainAsync(path, ct).ConfigureAwait(false),
                _ => throw new DocumentReadException("unsupported_type"),
            };

            log.Ok(LogKind.ToolCall, "document_read", clock.ElapsedMilliseconds);
            return result;
        }
        catch (DocumentReadException)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw new DocumentReadException("access_denied");
        }
        catch (IOException)
        {
            throw new DocumentReadException("io_error");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            log.Fail(LogKind.Error, "document." + MetaLog.Code(e));
            throw new DocumentReadException("unreadable");
        }
    }

    /// <summary>Local OCR of one page. It's free, so it always comes before asking the AI.</summary>
    public async Task<string?> OcrPageAsync(string path, int page, CancellationToken ct)
    {
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !screen.Ocr.IsAvailable)
        {
            return null;
        }

        try
        {
            var pages = new string[page];
            await OcrPagesAsync(path, pages, [page], [page], null, ct).ConfigureAwait(false);
            return pages[page - 1];
        }
        catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
        {
            log.Fail(LogKind.Error, "document.ocr_page." + MetaLog.Code(e));
            return null;
        }
    }

    /// <summary>A picture of one page, for the one place allowed to send it to the AI.</summary>
    public async Task<byte[]?> RenderPageJpegAsync(string path, int page, CancellationToken ct)
    {
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
            var pdf = await WinPdfDocument.LoadFromFileAsync(file).AsTask(ct).ConfigureAwait(false);
            if (page < 1 || page > pdf.PageCount)
            {
                return null;
            }

            using var rendered = pdf.GetPage((uint)(page - 1));
            using var stream = new InMemoryRandomAccessStream();
            var (w, h) = ImageCodec.Fit((int)Math.Ceiling(rendered.Size.Width * 2), (int)Math.Ceiling(rendered.Size.Height * 2), PageImageMaxSide);
            await rendered.RenderToStreamAsync(stream, new PdfPageRenderOptions { DestinationWidth = w, DestinationHeight = h }).AsTask(ct).WaitAsync(PageOcrTimeout, ct).ConfigureAwait(false);
            stream.Seek(0);
            using var bitmap = await ImageCodec.DecodeAsync(stream, PageImageMaxSide, ct).ConfigureAwait(false);
            using var opaque = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output).AsTask(ct).ConfigureAwait(false);
            encoder.SetSoftwareBitmap(opaque);
            await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            await output.AsStreamForRead().CopyToAsync(bytes, ct).ConfigureAwait(false);
            return bytes.Length == 0 ? null : bytes.ToArray();
        }
        catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
        {
            log.Fail(LogKind.Error, "document.render_page." + MetaLog.Code(e));
            return null;
        }
    }

    /// <summary>A library card from a book's first pages, without reading the whole book.</summary>
    public async Task<LibraryCard> ReadCardAsync(string path, CancellationToken ct)
    {
        var info = new FileInfo(path);
        string? print = FileFingerprint.Of(path);
        string? title = null;
        string? author = null;
        int count = 0;
        string opening = string.Empty;
        bool locked = false;
        try
        {
            if (info.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                string?[] first = await Task.Run<string?[]>(() =>
                {
                    using var pdf = PigDocument.Open(path, new PigParsingOptions { UseLenientParsing = true });
                    count = pdf.NumberOfPages;
                    title = pdf.Information.Title;
                    author = pdf.Information.Author;
                    var found = new string?[Math.Min(CardPages, count)];
                    for (int i = 0; i < found.Length; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        found[i] = PdfPageText.WithoutUnknown(PageTextOf(pdf.GetPage(i + 1)));
                    }

                    return found;
                }, ct).ConfigureAwait(false);
                opening = string.Join('\n', first.Where(t => !string.IsNullOrWhiteSpace(t)));
                if (DocumentFormats.LooksScanned(opening) && count > 0)
                {
                    opening = await OcrPageAsync(path, 1, ct).ConfigureAwait(false) ?? string.Empty;
                }
            }
            else
            {
                DocumentText doc = await ReadAsync(path, 1, 0, null, ct).ConfigureAwait(false);
                count = doc.Pages.Count;
                opening = string.Join('\n', doc.Pages.Take(CardPages));
            }
        }
        catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            locked = true;
        }
        catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
        {
            log.Fail(LogKind.Error, "library.card." + MetaLog.Code(e));
        }

        return new LibraryCard(path, print, BookTitle.Guess(info.Name, title, opening), string.IsNullOrWhiteSpace(author) ? null : author.Trim(), count, opening, info.Length, info.LastWriteTimeUtc, locked);
    }

    private static async Task<DocumentText> ReadPlainAsync(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var text = DocumentFormats.DecodeText(bytes);
        if (text.Length > MaxTextChars)
        {
            text = text[..MaxTextChars];
        }

        return Build("text", DocumentFormats.Paginate(text, DocumentFormats.PseudoPageChars), 0, []);
    }

    private static DocumentText ReadDocx(string path)
    {
        using var stream = File.OpenRead(path);
        var pages = DocumentFormats.ExtractDocx(stream, MaxTextChars);
        return Build("docx", pages, 0, []);
    }

    private async Task<DocumentText> ReadPdfAsync(
        string path, int firstOcrPage, int maxOcrPages, IProgress<(int Page, int Total)>? progress, CancellationToken ct)
    {
        var (pages, scanned, lostLetters) = await Task.Run(() => ExtractPdfText(path, ct), ct).ConfigureAwait(false);
        var pending = scanned.Concat(lostLetters).Order().ToList();
        if (pending.Count > 0 && maxOcrPages > 0)
        {
            var toOcr = pending.Where(p => p >= Math.Max(1, firstOcrPage)).Take(maxOcrPages).ToList();
            if (toOcr.Count > 0)
            {
                await OcrPagesAsync(path, pages, toOcr, pending, progress, ct).ConfigureAwait(false);
            }
        }

        if (!screen.Ocr.IsAvailable)
        {
            // A page with a few lost letters still reads, and only OCR could have filled them.
            foreach (int page in lostLetters)
            {
                pages[page - 1] = PdfPageText.WithoutUnknown(pages[page - 1]);
                pending.Remove(page);
            }
        }

        return Build("pdf", pages, scanned.Count, pending);
    }

    /// <summary>Glyph by glyph, so ligatures stay in order. Text that isn't laid out level still uses PdfPig's order.</summary>
    private static string PageTextOf(Page page)
    {
        var letters = page.Letters;
        if (letters.All(l => l.TextOrientation == TextOrientation.Horizontal))
        {
            var glyphs = letters.Select(l => new PdfGlyph(l.Value, l.Location.X, l.Location.Y, l.Width, l.BoundingBox.Height)).ToArray();
            if (PdfPageText.Build(glyphs) is { } text)
            {
                return text;
            }
        }

        return DocumentFormats.FixPdfPageText(ContentOrderTextExtractor.GetText(page));
    }

    private static (string[] Pages, List<int> Scanned, List<int> LostLetters) ExtractPdfText(string path, CancellationToken ct)
    {
        try
        {
            using var pdf = PigDocument.Open(path, new PigParsingOptions { UseLenientParsing = true });
            int count = pdf.NumberOfPages;
            var pages = new string[count];
            var scanned = new List<int>();
            var lostLetters = new List<int>();
            long total = 0;
            for (int i = 1; i <= count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string text;
                try
                {
                    var page = pdf.GetPage(i);
                    text = total < MaxTextChars ? PageTextOf(page) : "";
                }
                catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
                {
                    text = "";
                }

                total += text.Length;
                if (PdfPageText.TooBroken(text) || DocumentFormats.LooksScanned(PdfPageText.WithoutUnknown(text)))
                {
                    scanned.Add(i);
                    text = "";
                }
                else if (text.Contains(PdfPageText.Unknown, StringComparison.Ordinal))
                {
                    lostLetters.Add(i);
                }

                pages[i - 1] = text;
            }

            return (pages, scanned, lostLetters);
        }
        catch (UglyToad.PdfPig.Exceptions.PdfDocumentEncryptedException)
        {
            throw new DocumentReadException("pdf_encrypted");
        }
    }

    private async Task OcrPagesAsync(
        string path, string[] pages, List<int> toOcr, List<int> pending, IProgress<(int Page, int Total)>? progress, CancellationToken ct)
    {
        if (!screen.Ocr.IsAvailable)
        {
            log.Fail(LogKind.Error, "document.ocr_unavailable");
            return;
        }

        var file = await StorageFile.GetFileFromPathAsync(path).AsTask(ct).ConfigureAwait(false);
        var pdf = await WinPdfDocument.LoadFromFileAsync(file).AsTask(ct).ConfigureAwait(false);
        int maxSide = OcrService.MaxImageDimension;
        foreach (var pageNumber in toOcr)
        {
            ct.ThrowIfCancellationRequested();
            if (pageNumber < 1 || pageNumber > pdf.PageCount)
            {
                continue;
            }

            progress?.Report((pageNumber, (int)pdf.PageCount));
            try
            {
                using var page = pdf.GetPage((uint)(pageNumber - 1));
                using var stream = new InMemoryRandomAccessStream();
                var (w, h) = ImageCodec.Fit(
                    (int)Math.Ceiling(page.Size.Width * OcrRenderScale),
                    (int)Math.Ceiling(page.Size.Height * OcrRenderScale),
                    maxSide);
                var options = new PdfPageRenderOptions { DestinationWidth = w, DestinationHeight = h };
                await page.RenderToStreamAsync(stream, options).AsTask(ct).WaitAsync(PageOcrTimeout, ct).ConfigureAwait(false);
                stream.Seek(0);
                using var bitmap = await ImageCodec.DecodeAsync(stream, maxSide, ct).ConfigureAwait(false);
                var text = await screen.OcrBitmapAsync(bitmap, ct).ConfigureAwait(false);
                pages[pageNumber - 1] = string.IsNullOrEmpty(pages[pageNumber - 1]) ? text ?? "" : OcrRepair.FillLostLetters(pages[pageNumber - 1], text);
                pending.Remove(pageNumber);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.Fail(LogKind.Error, "document.page_ocr." + MetaLog.Code(e));
                if (!string.IsNullOrEmpty(pages[pageNumber - 1]))
                {
                    pages[pageNumber - 1] = PdfPageText.WithoutUnknown(pages[pageNumber - 1]);
                    pending.Remove(pageNumber);
                }
            }
        }
    }

    private static DocumentText Build(string kind, IReadOnlyList<string> pages, int scanned, IReadOnlyList<int> pending)
    {
        int total = 0;
        foreach (var p in pages)
        {
            total += p.Length;
        }

        return new DocumentText(kind, pages, total, scanned, pending);
    }
}
