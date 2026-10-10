using Basira.Core.Text;

namespace Basira.Core.Tests.Text;

public class OcrLineOrderTests
{
    /// <summary>Lays words out as they appear on screen, left to right, starting at <paramref name="x"/>.</summary>
    private static List<OcrWordBox> Visual(double y, params string[] wordsLeftToRight)
    {
        var boxes = new List<OcrWordBox>();
        double x = 10;
        foreach (string word in wordsLeftToRight)
        {
            double width = 9 * word.Length;
            boxes.Add(new OcrWordBox(word, x, y, width, 20));
            x += width + 8;
        }

        return boxes;
    }

    [Fact]
    public void ArabicLineReadsRightToLeft()
    {
        // On screen (left to right): "الآن الملف افتح" — the reader starts at the right.
        var words = Visual(10, "الآن", "الملف", "افتح");
        Assert.Equal("افتح الملف الآن", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void WindowsOcrReversedWordOrderIsFixed()
    {
        // Windows OCR returns Arabic words in reversed order; the boxes are still right.
        var words = Visual(10, "الآن", "الملف", "افتح");
        words.Reverse();
        Assert.Equal("افتح الملف الآن", OcrLineOrder.ToLogicalLine(words));

        var shuffled = new[] { words[1], words[2], words[0] };
        Assert.Equal("افتح الملف الآن", OcrLineOrder.ToLogicalLine(shuffled));
    }

    [Fact]
    public void LatinAndNumbersInsideArabicStayLeftToRight()
    {
        // Logical: "مرحبا بك في Windows 10 الآن"; on screen the LTR run keeps its own order.
        var words = Visual(10, "الآن", "Windows", "10", "في", "بك", "مرحبا");
        Assert.Equal("مرحبا بك في Windows 10 الآن", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void NeutralWordsBetweenLatinWordsStayInTheLatinRun()
    {
        var words = Visual(10, "اليوم", "Microsoft", "-", "Office", "افتح");
        Assert.Equal("افتح Microsoft - Office اليوم", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void TwoSeparateLatinRunsInArabic()
    {
        // Logical: "افتح PDF ثم Word 2016".
        var words = Visual(10, "Word", "2016", "ثم", "PDF", "افتح");
        Assert.Equal("افتح PDF ثم Word 2016", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void EnglishLineReadsLeftToRightWithArabicRunKeptRightToLeft()
    {
        // Logical: "Open the file أحمد محمد now"; the Arabic name is shown right to left.
        var words = Visual(10, "Open", "the", "file", "محمد", "أحمد", "now");
        Assert.Equal("Open the file أحمد محمد now", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void WordsNotLettersDecideTheDirection()
    {
        // More Latin letters than Arabic ones, but two Arabic words against two Latin ones: still an Arabic line.
        var words = Visual(10, "Microsoft", "Office", "برنامج", "افتح");
        Assert.Equal("افتح برنامج Microsoft Office", OcrLineOrder.ToLogicalLine(words));

        // Three English words against one Arabic word: an English line.
        Assert.Equal("Call me at مساء", OcrLineOrder.ToLogicalLine(Visual(10, "Call", "me", "at", "مساء")));
    }

    [Fact]
    public void NumbersOnlyLineReadsLeftToRight() =>
        Assert.Equal("2026 10 11", OcrLineOrder.ToLogicalLine(Visual(10, "2026", "10", "11")));

    [Fact]
    public void EmptyAndBlankWordsAreIgnored()
    {
        Assert.Equal("", OcrLineOrder.ToLogicalLine([]));
        var words = Visual(10, "بك", " ", "مرحبا");
        Assert.Equal("مرحبا بك", OcrLineOrder.ToLogicalLine(words));
    }

    [Fact]
    public void GroupLinesClustersByVerticalOverlapTopToBottom()
    {
        var line1 = Visual(10, "الأول", "السطر");
        var line2 = Visual(40, "الثاني", "السطر");
        var line3 = Visual(70, "third", "line");

        // Small vertical jitter inside a line, words given in scrambled order.
        line1[0] = line1[0] with { Y = 12, Height = 17 };
        line2[1] = line2[1] with { Y = 37, Height = 24 };
        var all = new List<OcrWordBox> { line3[1], line2[0], line1[1], line3[0], line1[0], line2[1] };

        var lines = OcrLineOrder.GroupLines(all);
        Assert.Equal(3, lines.Count);
        Assert.Equal(["الأول", "السطر"], lines[0].Select(w => w.Text));
        Assert.Equal(["الثاني", "السطر"], lines[1].Select(w => w.Text));
        Assert.Equal(["third", "line"], lines[2].Select(w => w.Text));
        Assert.All(lines, l => Assert.Equal(l.OrderBy(w => w.X).ToList(), l));

        Assert.Equal("السطر الأول\nالسطر الثاني\nthird line", OcrLineOrder.ToLogicalText(all));
    }

    [Fact]
    public void TouchingButNotOverlappingLinesStaySeparate()
    {
        var words = new List<OcrWordBox>
        {
            new("one", 10, 0, 30, 20),
            new("two", 10, 19, 30, 20),
        };
        Assert.Equal(2, OcrLineOrder.GroupLines(words).Count);
    }

    [Fact]
    public void TallWordJoinsTheLineItOverlapsMost()
    {
        var words = new List<OcrWordBox>
        {
            new("a", 10, 0, 10, 20),
            new("b", 30, 30, 10, 20),
            new("big", 50, 25, 20, 30),
        };
        var lines = OcrLineOrder.GroupLines(words);
        Assert.Equal(2, lines.Count);
        Assert.Equal(["b", "big"], lines[1].Select(w => w.Text));
    }

    [Fact]
    public void EmptyInputGivesNoLines() => Assert.Empty(OcrLineOrder.GroupLines([]));

    [Fact]
    public void NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => OcrLineOrder.ToLogicalLine(null!));
        Assert.Throws<ArgumentNullException>(() => OcrLineOrder.GroupLines(null!));
    }
}
