using Mubsir.Core.Library;
using Mubsir.Core.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Mubsir.Core.Tests.Text;

public class PdfPageTextTests
{
    private static string Pages(string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "data", "books", file);
        using PdfDocument pdf = PdfDocument.Open(path);
        return string.Join('\n', pdf.GetPages().Select(TextOf));
    }

    private static string TextOf(Page page) => PdfPageText.Build(
        page.Letters.Select(l => new PdfGlyph(l.Value, l.Location.X, l.Location.Y, l.Width, l.BoundingBox.Height)).ToArray())!;

    private static string NoMarks(string text) => new(text.Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

    [Theory]
    [InlineData("هذه هي الصفحة الأولى من الكتاب.")]
    [InlineData("ذهب الولد إلى المدرسة في الصباح.")]
    [InlineData("ثم سأل الطلاب عن معنى كلمة computer في الدرس.")]
    [InlineData("اشترى الولد ثلاثة كتب بخمسين جنيها")]
    [InlineData("هل تحب القراءة؟ هذه آخر جملة في الكتاب.")]
    public void A_real_chrome_pdf_keeps_its_ligatures_in_order(string sentence) =>
        Assert.Contains(sentence, NoMarks(Pages("arabic.pdf")).Replace('\n', ' '), StringComparison.Ordinal);

    [Fact]
    public void A_letter_the_pdf_has_no_text_for_is_marked_not_lost_quietly() =>
        Assert.Contains(PdfPageText.Unknown + "رأ المعلم الدرس بصوت عال.", NoMarks(Pages("arabic.pdf")), StringComparison.Ordinal);

    [Theory]
    [InlineData("The first page opens the story. A traveller reached the old harbour at dawn.")]
    [InlineData("Dr. Smith sold maps for")]
    [InlineData("3.50 pounds each.")]
    [InlineData("This is the last sentence of the book.")]
    public void A_real_english_pdf_reads_as_before(string sentence) =>
        Assert.Contains(sentence, Pages("english.pdf"), StringComparison.Ordinal);

    [Fact]
    public void A_line_drawn_right_to_left_reads_the_same_as_one_drawn_left_to_right()
    {
        PdfGlyph[] rightToLeft = [new("ك", 50, 0, 10, 10), new("ت", 40, 0, 10, 10), new("لا", 30, 0, 10, 10), new("ب", 20, 0, 10, 10)];
        Assert.Equal("كتلاب", PdfPageText.Build(rightToLeft));
        Assert.Equal("كتلاب", PdfPageText.Build(rightToLeft.Reverse().ToArray()));
    }

    [Fact]
    public void Marks_stay_on_their_letters_either_way()
    {
        PdfGlyph[] leftToRight = [new("َ", 22, 3, 0, 2), new("ب", 20, 0, 10, 10), new("ِ", 32, -3, 0, 2), new("ك", 30, 0, 10, 10)];
        Assert.Equal("كِبَ", PdfPageText.Build(leftToRight));
        PdfGlyph[] rightToLeft = [new("ك", 30, 0, 10, 10), new("ِ", 32, -3, 0, 2), new("ب", 20, 0, 10, 10), new("َ", 22, 3, 0, 2)];
        Assert.Equal("كِبَ", PdfPageText.Build(rightToLeft));
    }

    [Fact]
    public void A_pdf_with_no_space_glyphs_still_has_words()
    {
        PdfGlyph[] glyphs = [new("a", 0, 0, 5, 10), new("b", 5, 0, 5, 10), new("c", 20, 0, 5, 10)];
        Assert.Equal("ab c", PdfPageText.Build(glyphs));
    }

    [Fact]
    public void Glyphs_with_no_widths_are_left_to_the_old_way() =>
        Assert.Null(PdfPageText.Build([new("a", 0, 0, 0, 10), new("b", 5, 0, 0, 10), new("c", 9, 0, 0, 10)]));

    [Fact]
    public void A_page_mostly_without_text_goes_to_ocr()
    {
        Assert.True(PdfPageText.TooBroken("����abc"));
        Assert.False(PdfPageText.TooBroken("�" + new string('a', 40)));
    }

    [Fact]
    public void A_lost_letter_is_filled_from_ocr_and_the_rest_of_the_line_stays()
    {
        string text = "�َرَأَ المعلم الدرس computer\nسطر سليم";
        string ocr = "سطر سليم\nقرأ المعلم الدرس comguter";
        Assert.Equal("قرأ المعلم الدرس computer\nسطر سليم", OcrRepair.FillLostLetters(text, ocr));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("كلام مختلف تماما عن السطر")]
    public void With_no_matching_ocr_the_lost_letter_is_just_left_out(string? ocr) =>
        Assert.Equal("رأ المعلم الدرس", OcrRepair.FillLostLetters("�رأ المعلم الدرس", ocr));

    [Fact]
    public void English_words_come_from_the_english_engine()
    {
        OcrWordBox[][] arabic = [[new("scanned", 0, 0, 50, 10), new("from", 55, 0, 30, 10), new(".aper", 90, 0, 40, 10)]];
        OcrWordBox[] latin = [new("scanned", 1, 0, 50, 10), new("from", 55, 0, 30, 10), new("paper.", 90, 0, 40, 10)];
        Assert.Equal("scanned from paper.", OcrRepair.Merge(arabic, latin));
    }

    [Fact]
    public void Arabic_words_stay_with_the_arabic_engine_and_english_inside_them_is_corrected()
    {
        OcrWordBox[][] arabic = [[new("computer.", 0, 0, 50, 10), new("كلمة", 60, 0, 30, 10), new("معنى", 100, 0, 30, 10)]];
        OcrWordBox[] latin = [new("computer", 0, 0, 50, 10), new("Ls", 60, 0, 30, 10), new("xyz", 100, 0, 30, 10)];
        Assert.Equal("معنى كلمة computer", OcrRepair.Merge(arabic, latin));
    }

    [Fact]
    public void A_line_the_arabic_engine_missed_is_kept_in_place()
    {
        OcrWordBox[][] arabic = [[new("سطر", 0, 40, 30, 10)]];
        OcrWordBox[] latin = [new("English", 0, 0, 40, 10), new("line", 45, 0, 20, 10)];
        Assert.Equal("English line\nسطر", OcrRepair.Merge(arabic, latin));
    }

    [Theory]
    [InlineData("english.html", "My Book.pdf", "My Book")]
    [InlineData("chapter3.docx", "notes.pdf", "notes")]
    [InlineData("Introducing Phonology", "x1.pdf", "Introducing Phonology")]
    [InlineData("Phonology vol. 2", "x1.pdf", "Phonology vol. 2")]
    public void A_title_that_is_only_a_file_name_is_ignored(string metadata, string file, string expected) =>
        Assert.Equal(expected, BookTitle.Guess(file, metadata, null));
}
