using System.IO.Compression;
using System.Text;
using System.Xml;
using Basira.Core.Text;

namespace Basira.App.Screen.Documents;

/// <summary>Pure text extraction helpers for plain text, .docx and PDF page text. No Windows APIs.</summary>
internal static class DocumentFormats
{
    /// <summary>Pages of plain text and .docx files are cut at paragraph ends near this many characters.</summary>
    public const int PseudoPageChars = 3000;

    /// <summary>A PDF page with fewer letters than this is treated as a scanned image and sent to OCR.</summary>
    public const int ScannedPageLetterThreshold = 20;

    private static readonly Lazy<Encoding> Arabic1256 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1256);
    });

    /// <summary>Decodes a text file: BOM first (UTF-8, UTF-16 LE/BE), then strict UTF-8, else Windows-1256 (Arabic ANSI).</summary>
    public static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(bytes[2..]);
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Arabic1256.Value.GetString(bytes);
        }
    }

    /// <summary>
    /// Paragraph text of a .docx (word/document.xml): w:t runs, w:tab as a tab, w:br/w:cr as line breaks, one paragraph
    /// per line. Explicit page breaks start a new page; documents without them are paged by size.
    /// </summary>
    public static List<string> ExtractDocx(Stream docx, int maxChars)
    {
        using var zip = new ZipArchive(docx, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry("word/document.xml") ?? throw new InvalidDataException("docx_no_body");
        using var xml = entry.Open();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
        using var reader = XmlReader.Create(xml, settings);

        const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var pages = new List<string>();
        var page = new StringBuilder();
        var paragraph = new StringBuilder();
        bool explicitBreaks = false;
        int total = 0;

        void EndPage()
        {
            if (paragraph.Length > 0)
            {
                page.Append(paragraph).Append('\n');
                paragraph.Clear();
            }

            if (page.Length > 0)
            {
                pages.Add(page.ToString().TrimEnd());
                page.Clear();
            }
        }

        bool advance = true;
        while (total < maxChars)
        {
            // ReadElementContentAsString leaves the reader on the next node already; do not skip it.
            if (advance && !reader.Read())
            {
                break;
            }

            advance = true;
            if (reader.NamespaceURI != W)
            {
                continue;
            }

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (reader.LocalName == "p")
                {
                    page.Append(paragraph).Append('\n');
                    total += paragraph.Length + 1;
                    paragraph.Clear();
                }

                continue;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.LocalName)
            {
                case "t" when !reader.IsEmptyElement:
                    paragraph.Append(reader.ReadElementContentAsString());
                    advance = false;
                    break;
                case "tab":
                    paragraph.Append('\t');
                    break;
                case "br" when reader.GetAttribute("type", W) == "page":
                case "lastRenderedPageBreak":
                    // Word records where its pages ended when the file was last saved: page numbers then match Word's.
                    explicitBreaks = true;
                    EndPage();
                    break;
                case "br" or "cr":
                    paragraph.Append('\n');
                    break;
            }
        }

        EndPage();
        if (!explicitBreaks)
        {
            return Paginate(string.Join("\n", pages), PseudoPageChars);
        }

        return pages.Count == 0 ? [""] : pages;
    }

    /// <summary>Cuts long text into pages of about <paramref name="target"/> characters at paragraph or line ends.</summary>
    public static List<string> Paginate(string text, int target)
    {
        var pages = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            pages.Add("");
            return pages;
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(text.Length, start + target);
            if (end < text.Length)
            {
                int para = text.LastIndexOf("\n\n", end, end - start, StringComparison.Ordinal);
                int line = text.LastIndexOf('\n', end - 1, end - start);
                int space = text.LastIndexOf(' ', end - 1, end - start);
                int cut = para > start + target / 2 ? para + 2
                    : line > start + target / 2 ? line + 1
                    : space > start + target / 2 ? space + 1
                    : end;
                if (cut > 0 && cut < text.Length && char.IsLowSurrogate(text[cut]))
                {
                    cut--;
                }

                end = cut;
            }

            pages.Add(text[start..end].Trim('\n'));
            start = end;
        }

        return pages;
    }

    /// <summary>
    /// Repairs text extracted from a PDF page: lines stored in reversed visual order are put back in logical order,
    /// then Arabic presentation forms are normalized to regular letters.
    /// </summary>
    public static string FixPdfPageText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (ArabicNormalizer.LooksReversed(lines[i]))
            {
                lines[i] = ArabicNormalizer.FixReversedLine(lines[i]);
            }
        }

        return ArabicNormalizer.NormalizePresentationForms(string.Join('\n', lines));
    }

    /// <summary>True when a PDF page has (almost) no text layer.</summary>
    public static bool LooksScanned(string pageText)
    {
        int letters = 0;
        foreach (var c in pageText)
        {
            if (char.IsLetter(c) && ++letters >= ScannedPageLetterThreshold)
            {
                return false;
            }
        }

        return true;
    }
}
