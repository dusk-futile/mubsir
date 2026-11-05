using System.IO.Compression;
using System.Text;
using Mubsir.Core.Sessions;
using Xunit;

namespace Mubsir.Core.Tests.Sessions;

public sealed class DocxWriterTests
{
    [Fact]
    public void Write_creates_valid_zip_holding_document_xml_with_arabic_and_english_text()
    {
        using var stream = new MemoryStream();
        string[] paragraphs =
        [
            "14:30 You: السلام عليكم، كيف أتعلم البرمجة بلغة سي شارب؟",
            "14:31 Mubsir: وعليكم السلام ورحمة الله وبركاته! يمكنك البدء بتثبيت دوت نت.",
            "14:32 You: Thanks for the help! Can we write tests too?",
            "14:33 Mubsir: Absolutely! Writing unit tests ensures everything works reliably.",
        ];

        DocxWriter.Write(stream, paragraphs);
        stream.Position = 0;

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        // Must hold standard OpenXML entries
        Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
        Assert.NotNull(archive.GetEntry("_rels/.rels"));

        ZipArchiveEntry? docEntry = archive.GetEntry("word/document.xml");
        Assert.NotNull(docEntry);

        using var reader = new StreamReader(docEntry.Open(), Encoding.UTF8);
        string documentXml = reader.ReadToEnd();

        // Must contain root WordprocessingML tag
        Assert.Contains("<w:document", documentXml, StringComparison.Ordinal);
        Assert.Contains("</w:document>", documentXml, StringComparison.Ordinal);

        // Must contain Arabic text
        Assert.Contains("السلام عليكم", documentXml, StringComparison.Ordinal);
        Assert.Contains("وعليكم السلام", documentXml, StringComparison.Ordinal);

        // Must contain English text
        Assert.Contains("Thanks for the help!", documentXml, StringComparison.Ordinal);
        Assert.Contains("Writing unit tests", documentXml, StringComparison.Ordinal);

        // Must contain bidi tag for RTL paragraphs
        Assert.Contains("<w:bidi/>", documentXml, StringComparison.Ordinal);
    }

    [Fact]
    public void TryExportMarkdownFile_exports_existing_file()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "mubsir_docx_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string mdPath = Path.Combine(tempDir, "2026-11-05.md");
            string docxPath = Path.Combine(tempDir, "2026-11-05.docx");

            File.WriteAllLines(mdPath, [
                "**10:00 You:** مرحباً",
                "",
                "**10:01 Mubsir:** أهلاً بك",
            ], Encoding.UTF8);

            bool result = DocxWriter.TryExportMarkdownFile(mdPath, docxPath);
            Assert.True(result);
            Assert.True(File.Exists(docxPath));

            using var fs = File.OpenRead(docxPath);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = archive.GetEntry("word/document.xml");
            Assert.NotNull(entry);
            using var r = new StreamReader(entry.Open(), Encoding.UTF8);
            string content = r.ReadToEnd();
            Assert.Contains("مرحباً", content, StringComparison.Ordinal);
            Assert.Contains("أهلاً بك", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
