using System.IO.Compression;
using System.Text;
using Mubsir.Core.Text;

namespace Mubsir.Core.Sessions;

/// <summary>
/// Writes clean .docx documents using ZipArchive and standard WordprocessingML, with no external packages.
/// Used for exporting chat archives.
/// </summary>
public static class DocxWriter
{
    private const string ContentTypesXml =
        """<?xml version="1.0" encoding="utf-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>""";

    private const string RootRelsXml =
        """<?xml version="1.0" encoding="utf-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""";

    public static void Write(Stream output, IEnumerable<string> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(paragraphs);

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        ZipArchiveEntry contentTypes = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
        using (var writer = new StreamWriter(contentTypes.Open(), new UTF8Encoding(false)))
        {
            writer.Write(ContentTypesXml);
        }

        ZipArchiveEntry rels = archive.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
        using (var writer = new StreamWriter(rels.Open(), new UTF8Encoding(false)))
        {
            writer.Write(RootRelsXml);
        }

        ZipArchiveEntry doc = archive.CreateEntry("word/document.xml", CompressionLevel.Fastest);
        using (var writer = new StreamWriter(doc.Open(), new UTF8Encoding(false)))
        {
            writer.Write("""<?xml version="1.0" encoding="utf-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>""");
            foreach (string p in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    continue;
                }

                bool isRtl = ScriptInfo.CountLetters(p).Arabic > 0;
                string escaped = EscapeXml(p.Trim());
                writer.Write("<w:p>");
                if (isRtl)
                {
                    writer.Write("<w:pPr><w:bidi/></w:pPr>");
                }

                writer.Write("<w:r><w:t xml:space=\"preserve\">");
                writer.Write(escaped);
                writer.Write("</w:t></w:r></w:p>");
            }

            writer.Write("</w:body></w:document>");
        }
    }

    public static void WriteToFile(string path, IEnumerable<string> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(path);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = File.Create(path);
        Write(fs, paragraphs);
    }

    public static bool TryExportMarkdownFile(string markdownPath, string docxPath)
    {
        ArgumentNullException.ThrowIfNull(markdownPath);
        ArgumentNullException.ThrowIfNull(docxPath);

        if (!File.Exists(markdownPath))
        {
            return false;
        }

        string[] lines = File.ReadAllLines(markdownPath, Encoding.UTF8);
        WriteToFile(docxPath, lines);
        return true;
    }

    private static string EscapeXml(string text)
    {
        var sb = new StringBuilder(text.Length + 16);
        foreach (char c in text)
        {
            switch (c)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '\'':
                    sb.Append("&apos;");
                    break;
                default:
                    if (c >= 0x20 || c is '\t' or '\n' or '\r')
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }
}
