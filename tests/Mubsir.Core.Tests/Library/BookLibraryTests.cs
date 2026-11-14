using Mubsir.Core.Common;
using Mubsir.Core.Library;

namespace Mubsir.Core.Tests.Library;

public class BookLibraryTests
{
    private static LibraryCard Card(string path, string title, string opening = "") =>
        new(path, "sha:" + path, title, null, 200, opening, 10, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void A_title_beats_a_mention_in_the_opening_and_unnamed_books_are_still_found()
    {
        var library = new BookLibrary(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        library.Put(Card(@"C:\a\syntax.pdf", "Syntax", "unlike phonology, syntax is about sentences"));
        library.Put(Card(@"C:\a\introducing.pdf", "Introducing Phonology"));
        library.Put(Card(@"C:\a\scan0001.pdf", "scan0001", "Chapter one. Phonology is the study of sound."));
        library.Put(Card(@"C:\a\cooking.pdf", "Cooking"));

        IReadOnlyList<LibraryCard> found = library.Search("phonology");

        Assert.Equal(["Introducing Phonology", "scan0001", "Syntax"], found.Select(c => c.Title).OrderByDescending(t => t == "Introducing Phonology").ThenByDescending(t => t == "scan0001"));
        Assert.Equal("Introducing Phonology", found[0].Title);
        Assert.DoesNotContain(found, c => c.Title == "Cooking");
    }

    [Fact]
    public void Arabic_search_ignores_spelling_differences()
    {
        var library = new BookLibrary(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        library.Put(Card(@"C:\a\b.pdf", "أساسيات اللُّغة العربية"));

        Assert.Single(library.Search("اللغه"));
    }

    [Fact]
    public void Cards_survive_a_restart_and_a_changed_file_is_read_again()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var first = new BookLibrary(file);
            LibraryCard card = Card(@"C:\a\b.pdf", "Title");
            first.Put(card);
            first.Save();

            var second = new BookLibrary(file);
            Assert.True(second.IsCurrent(card.Path, card.Length, card.Written));
            Assert.False(second.IsCurrent(card.Path, card.Length + 1, card.Written));
            Assert.Equal("Title", second.ByPrint(card.Print!)!.Title);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("scan0001.pdf", "Introducing Phonology", "x", "Introducing Phonology")]
    [InlineData("scan0001.pdf", "Microsoft Word - draft", "Introducing Phonology\nChapter 1", "Introducing Phonology")]
    [InlineData("Phonology notes.pdf", null, "ignored line", "Phonology notes")]
    [InlineData("0001.pdf", null, null, "0001")]
    public void A_title_comes_from_the_best_place_available(string file, string? metadata, string? opening, string expected) =>
        Assert.Equal(expected, BookTitle.Guess(file, metadata, opening));

    private static BookLibrary Shelf()
    {
        var library = new BookLibrary(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        library.Put(Card(@"C:\a\intro.pdf", "Introducing Phonology"));
        library.Put(Card(@"C:\a\aswat.pdf", "علم الأصوات"));
        library.Put(Card(@"C:\a\cook.pdf", "Cooking"));
        return library;
    }

    [Fact]
    public async Task The_ai_keywords_find_books_in_both_languages()
    {
        IReadOnlyList<LibraryCard> found = await BookSearch.FindAsync(Shelf(), "كتاب فونولوجيا for my exam", _ => Task.FromResult<string?>("phonology\n- علم الأصوات\n1. phonetics"));

        Assert.Equal(["Introducing Phonology", "علم الأصوات"], found.Select(c => c.Title).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_clear_title_match_does_not_call_the_ai()
    {
        int calls = 0;
        IReadOnlyList<LibraryCard> found = await BookSearch.FindAsync(Shelf(), "phonology", _ =>
        {
            calls++;
            return Task.FromResult<string?>("x");
        });

        Assert.Equal(0, calls);
        Assert.Equal("Introducing Phonology", found[0].Title);
    }

    [Fact]
    public async Task A_mixed_sentence_always_asks_the_ai()
    {
        int calls = 0;
        await BookSearch.FindAsync(Shelf(), "phonology بتاع الجامعة", _ =>
        {
            calls++;
            return Task.FromResult<string?>("phonology");
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task When_the_ai_fails_or_is_off_the_plain_search_still_answers()
    {
        Assert.Equal("Cooking", (await BookSearch.FindAsync(Shelf(), "cooking recipes", _ => throw new HttpRequestException()))[0].Title);
        Assert.Equal("Cooking", (await BookSearch.FindAsync(Shelf(), "cooking recipes", _ => Task.FromResult<string?>(null)))[0].Title);
        Assert.Equal("Cooking", (await BookSearch.FindAsync(Shelf(), "cooking recipes", null))[0].Title);
    }

    [Theory]
    [InlineData("1. phonology\n2) علم الأصوات\n- \"phonetics\"\n\n", new[] { "phonology", "علم الأصوات", "phonetics" })]
    [InlineData("a\nphonology\nPHONOLOGY", new[] { "phonology" })]
    public void Keywords_are_cleaned_up(string answer, string[] expected) => Assert.Equal(expected, BookSearch.ParseKeywords(answer));

    [Theory]
    [InlineData("which book has the phrase the quick brown fox", "the quick brown fox")]
    [InlineData("which book contains \"survival of the fittest\"?", "survival of the fittest")]
    [InlineData("search my books for photosynthesis", "photosynthesis")]
    [InlineData("أنهي كتاب فيه جملة العلم نور", "العلم نور")]
    [InlineData("دور في الكتب على كلمة الفاعل", "كلمة الفاعل")]
    public void Asking_which_book_holds_some_words_keeps_the_words(string said, string wanted)
    {
        Mubsir.Core.Reading.BookCommand? command = Mubsir.Core.Reading.BookCommands.Parse(said);

        Assert.NotNull(command);
        Assert.Equal(Mubsir.Core.Reading.BookCommandKind.FindPhrase, command.Kind);
        Assert.Equal(wanted, command.Text);
    }

    [Fact]
    public void Exact_words_are_found_on_their_page_whatever_the_case_tashkeel_or_line_breaks()
    {
        string[] pages = ["Chapter one.", "It was the best of times,\nit was the worst of times.", "قَالَ: العِلْمُ نُورٌ."];

        Assert.Equal(2, PhraseSearch.FirstPage(pages, "Best of times it was"));
        Assert.Equal(3, PhraseSearch.FirstPage(pages, "العلم نور"));
        Assert.Equal(0, PhraseSearch.FirstPage(pages, "the age of wisdom"));
        Assert.Equal(0, PhraseSearch.FirstPage(pages, "?!"));
    }

    [Fact]
    public void What_was_found_is_said_with_its_page_and_nothing_found_says_how_many_were_searched()
    {
        PhraseHit[] hits = [new(Card(@"C:\a\tale.pdf", "A Tale"), 2)];

        Assert.Equal("I found it in 1 of 40 books. 1: A Tale, page 2. Say the number and I'll open it at that page.", PhraseSearch.Heard(hits, 40, Lang.English));
        Assert.Equal("I searched 40 books and didn't find those words.", PhraseSearch.Heard([], 40, Lang.English));
    }

    [Fact]
    public void A_spoken_list_is_numbered_and_tells_same_named_books_apart()
    {
        LibraryCard[] cards = [Card(@"C:\School\grammar.pdf", "Grammar"), Card(@"C:\Downloads\grammar.pdf", "Grammar")];

        string text = BookListText.List(cards, Lang.English);

        Assert.StartsWith("I found 2. 1: Grammar, 200 pages", text, StringComparison.Ordinal);
        Assert.EndsWith("Say the number.", text, StringComparison.Ordinal);
    }
}
