using System.Diagnostics;
using Mubsir.Core.Logging;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Data.Pdf;
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
        var (pages, scanned) = await Task.Run(() => ExtractPdfText(path, ct), ct).ConfigureAwait(false);
        var pending = new List<int>(scanned);
        if (scanned.Count > 0 && maxOcrPages > 0)
        {
            var toOcr = scanned.Where(p => p >= Math.Max(1, firstOcrPage)).Take(maxOcrPages).ToList();
            if (toOcr.Count > 0)
            {
                await OcrPagesAsync(path, pages, toOcr, pending, progress, ct).ConfigureAwait(false);
            }
        }

        return Build("pdf", pages, scanned.Count, pending);
    }

    private static (string[] Pages, List<int> Scanned) ExtractPdfText(string path, CancellationToken ct)
    {
        try
        {
            using var pdf = PigDocument.Open(path, new PigParsingOptions { UseLenientParsing = true });
            int count = pdf.NumberOfPages;
            var pages = new string[count];
            var scanned = new List<int>();
            long total = 0;
            for (int i = 1; i <= count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string text;
                try
                {
                    var page = pdf.GetPage(i);
                    text = total < MaxTextChars ? DocumentFormats.FixPdfPageText(ContentOrderTextExtractor.GetText(page)) : "";
                }
                catch (Exception e) when (e is not OutOfMemoryException and not OperationCanceledException)
                {
                    text = "";
                }

                total += text.Length;
                if (DocumentFormats.LooksScanned(text))
                {
                    scanned.Add(i);
                    text = "";
                }

                pages[i - 1] = text;
            }

            return (pages, scanned);
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
                pages[pageNumber - 1] = text ?? "";
                pending.Remove(pageNumber);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.Fail(LogKind.Error, "document.page_ocr." + MetaLog.Code(e));
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
