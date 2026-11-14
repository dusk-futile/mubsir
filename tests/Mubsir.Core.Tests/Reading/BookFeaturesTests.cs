using Mubsir.Core.Common;
using Mubsir.Core.Reading;
using Mubsir.Core.Speech;

namespace Mubsir.Core.Tests.Reading;

public class BookFeaturesTests
{
    [Theory]
    [InlineData("next page", BookCommandKind.NextPage, null)]
    [InlineData("please go to the next page", BookCommandKind.NextPage, null)]
    [InlineData("الصفحة اللي بعدها", BookCommandKind.NextPage, null)]
    [InlineData("الصفحه الجايه", BookCommandKind.NextPage, null)]
    [InlineData("ارجع صفحة", BookCommandKind.PreviousPage, null)]
    [InlineData("go to page 34", BookCommandKind.GoToPage, 34)]
    [InlineData("page thirty four", BookCommandKind.GoToPage, 34)]
    [InlineData("روح لصفحة ٣٤", BookCommandKind.GoToPage, 34)]
    [InlineData("صفحة اربعة وتلاتين", BookCommandKind.GoToPage, 34)]
    [InlineData("أنا في صفحة كام", BookCommandKind.WhereInBook, null)]
    [InlineData("كمل", BookCommandKind.Continue, null)]
    [InlineData("continue reading", BookCommandKind.Continue, null)]
    [InlineData("mark this", BookCommandKind.Mark, null)]
    [InlineData("اقرالي ملاحظاتي", BookCommandKind.ReadNotes, null)]
    [InlineData("read for 10 minutes", BookCommandKind.SleepTimer, 10)]
    [InlineData("stop after half an hour", BookCommandKind.SleepTimer, 30)]
    [InlineData("اقرأ لمدة ربع ساعة", BookCommandKind.SleepTimer, 15)]
    [InlineData("اقف بعد دقيقتين", BookCommandKind.SleepTimer, 2)]
    [InlineData("اقرا عشر دقايق", BookCommandKind.SleepTimer, 10)]
    [InlineData("كنت بقرأ إيه", BookCommandKind.ReadingJournal, null)]
    public void Reader_commands_are_understood(string said, BookCommandKind kind, int? number)
    {
        BookCommand? command = BookCommands.Parse(said);

        Assert.NotNull(command);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(number, command.Number);
    }

    [Theory]
    [InlineData("روح page 40", BookCommandKind.GoToPage, 40)]
    [InlineData("go to صفحة ٤٠", BookCommandKind.GoToPage, 40)]
    [InlineData("روح لصفحة forty", BookCommandKind.GoToPage, 40)]
    [InlineData("صفحة thirty four", BookCommandKind.GoToPage, 34)]
    [InlineData("next صفحة", BookCommandKind.NextPage, null)]
    [InlineData("الصفحة next", BookCommandKind.NextPage, null)]
    [InlineData("page الجاية", BookCommandKind.NextPage, null)]
    [InlineData("previous صفحة", BookCommandKind.PreviousPage, null)]
    [InlineData("ارجع page", BookCommandKind.PreviousPage, null)]
    [InlineData("كمل reading", BookCommandKind.Continue, null)]
    [InlineData("continue الكتاب", BookCommandKind.Continue, null)]
    [InlineData("read for عشر دقايق", BookCommandKind.SleepTimer, 10)]
    [InlineData("اقرأ for ten minutes", BookCommandKind.SleepTimer, 10)]
    [InlineData("وقف after 5 دقايق", BookCommandKind.SleepTimer, 5)]
    public void Sentences_mixing_both_languages_are_understood(string said, BookCommandKind kind, int? number)
    {
        BookCommand? command = BookCommands.Parse(said);

        Assert.NotNull(command);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(number, command.Number);
    }

    [Theory]
    [InlineData("find كتاب phonology", "phonology")]
    [InlineData("هات the book on النحو", "النحو")]
    [InlineData("عايز book عن grammar", "grammar")]
    [InlineData("give me كتاب بتاع phonetics", "phonetics")]
    [InlineData("هات الكتاب بتاع English grammar", "English grammar")]
    [InlineData("كتاب phonology", "phonology")]
    public void A_mixed_search_keeps_the_subject(string said, string wanted)
    {
        BookCommand? command = BookCommands.Parse(said);

        Assert.Equal(BookCommandKind.FindBook, command!.Kind);
        Assert.Equal(wanted, command.Text);
    }

    [Theory]
    [InlineData("book a flight")]
    [InlineData("open the page")]
    [InlineData("find a flight to Cairo")]
    [InlineData("read the next email")]
    public void Mixed_rules_leave_other_requests_alone(string said) => Assert.Null(BookCommands.Parse(said));

    [Theory]
    [InlineData("open chrome")]
    [InlineData("اقرأ الشاشة")]
    [InlineData("اكتب ايميل لمحمد")]
    [InlineData("read the email from my boss")]
    [InlineData("the weather today")]
    public void Other_requests_are_left_alone(string said) => Assert.Null(BookCommands.Parse(said));

    [Fact]
    public void A_note_keeps_the_readers_own_words()
    {
        Assert.Equal("Ask my Professor about this", BookCommands.Parse("note: Ask my Professor about this")!.Text);
        Assert.Equal("اسأل الدكتور", BookCommands.Parse("ملاحظة اسأل الدكتور")!.Text);
    }

    [Theory]
    [InlineData("give me the book on phonology", "phonology")]
    [InlineData("هات كتاب النحو", "النحو")]
    [InlineData("دور على كتاب عن التاريخ", "التاريخ")]
    public void Finding_a_book_keeps_what_to_look_for(string said, string wanted)
    {
        BookCommand? command = BookCommands.Parse(said);

        Assert.Equal(BookCommandKind.FindBook, command!.Kind);
        Assert.Equal(wanted, command.Text);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("number two", 2)]
    [InlineData("رقم ٣", 3)]
    [InlineData("التاني", 2)]
    [InlineData("the first", 1)]
    public void A_number_picks_from_a_list_only_while_choosing(string said, int number)
    {
        Assert.Equal(number, BookCommands.Parse(said, choosing: true)!.Number);
        Assert.NotEqual(BookCommandKind.Pick, BookCommands.Parse(said)?.Kind);
    }

    private sealed class Voice : IBookVoice
    {
        public SpeechHistory History { get; } = new();

        public long GenerationValue;

        public long Generation => GenerationValue;

        public List<SpokenSentence> Queue { get; } = [];

        public IReadOnlyList<SpokenSentence> Say(string text, Lang lang)
        {
            var (_, items) = History.AddUtteranceWithItems(text, UtteranceKind.Reading);
            var said = items.Select(i => new SpokenSentence(i.Index, i.Text)).ToArray();
            Queue.AddRange(said);
            return said;
        }

        public void Hear(string text) => History.MarkSpoken(Queue.Last(s => s.Text.Contains(text, StringComparison.Ordinal)).Index);
    }

    private sealed class Pages(params string[] pages) : IBookPages
    {
        public int Count => pages.Length;

        public int Reads { get; private set; }

        public Task<string?> GetAsync(int page, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<string?>(pages[page - 1]);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "mubsir-tests-" + Guid.NewGuid().ToString("N"));

        public Harness()
        {
            Directory.CreateDirectory(_dir);
            BookPath = Path.Combine(_dir, "book.txt");
            File.WriteAllText(BookPath, "the book");
            Positions = new ReadingPositions(Path.Combine(_dir, "positions.json"));
            Session = new BookSession(Voice, () => { Voice.GenerationValue++; Session!.OnStopped(); }, Positions, new BookNotes(Path.Combine(_dir, "notes.json")), _ => { });
            Voice.History.Spoken += i => Session.OnSpoken(i);
        }

        public Voice Voice { get; } = new();

        public string BookPath { get; }

        public ReadingPositions Positions { get; }

        public BookSession Session { get; }

        public Task Open() => Session.StartAsync(new Pages("One start. One end.", "Two start. Two end.", "Three start. Three end."), BookPath, "My Book", 1, 0, Lang.English);

        public void Dispose() => Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("go to the fourth line", BookCommandKind.GoToLine, 4)]
    [InlineData("line 12", BookCommandKind.GoToLine, 12)]
    [InlineData("read from line twenty two", BookCommandKind.GoToLine, 22)]
    [InlineData("السطر الرابع", BookCommandKind.GoToLine, 4)]
    [InlineData("روح للسطر ٧", BookCommandKind.GoToLine, 7)]
    [InlineData("آخر سطر", BookCommandKind.GoToLine, -1)]
    [InlineData("the last sentence", BookCommandKind.GoToSentence, -1)]
    [InlineData("الجملة التانية", BookCommandKind.GoToSentence, 2)]
    [InlineData("go to paragraph 3", BookCommandKind.GoToParagraph, 3)]
    [InlineData("اقرأ من الفقرة الثالثة", BookCommandKind.GoToParagraph, 3)]
    [InlineData("go to السطر 5", BookCommandKind.GoToLine, 5)]
    [InlineData("read the heading", BookCommandKind.ReadHeading, null)]
    [InlineData("اقرأ العنوان", BookCommandKind.ReadHeading, null)]
    [InlineData("read the ending", BookCommandKind.ReadEnding, null)]
    [InlineData("اقرأ آخر الصفحة", BookCommandKind.ReadEnding, null)]
    [InlineData("20% faster", BookCommandKind.Speed, 20)]
    [InlineData("read ten percent slower", BookCommandKind.Speed, -10)]
    [InlineData("speed up by 15 percent", BookCommandKind.Speed, 15)]
    [InlineData("أسرع عشرين في المية", BookCommandKind.Speed, 20)]
    [InlineData("أبطأ ١٠٪", BookCommandKind.Speed, -10)]
    [InlineData("اقرأ أبطأ بنسبة عشرة بالمئة", BookCommandKind.Speed, -10)]
    public void Finer_reading_commands_are_understood(string said, BookCommandKind kind, int? number)
    {
        BookCommand? command = BookCommands.Parse(said);

        Assert.NotNull(command);
        Assert.Equal(kind, command.Kind);
        Assert.Equal(number, command.Number);
    }

    [Theory]
    [InlineData("the battery is at 20 percent")]
    [InlineData("is it 20% faster than mine")]
    [InlineData("200% faster")]
    [InlineData("draw a line")]
    [InlineData("write a sentence about cats")]
    [InlineData("first in line")]
    public void Other_sentences_are_not_reading_commands(string said) => Assert.Null(BookCommands.Parse(said));

    [Fact]
    public void A_page_is_split_into_lines_sentences_and_paragraphs()
    {
        const string pdf = "Chapter One\nIt was late. The rain\nhad stopped. He went home.";
        Assert.Equal([0, 12, 34], PageParts.Starts(pdf, BookCommandKind.GoToLine));
        Assert.Equal([0, 12, 34], PageParts.Starts(pdf, BookCommandKind.GoToParagraph));
        Assert.Equal("Chapter One", PageParts.Heading("\n  Chapter One\nText."));

        const string blocks = "First block\nstill first.\n\nSecond block.";
        Assert.Equal([0, 26], PageParts.Starts(blocks, BookCommandKind.GoToParagraph));

        const string three = "One. Two. Three. Four.";
        Assert.Equal([0, 5, 10, 17], PageParts.Starts(three, BookCommandKind.GoToSentence));
        Assert.Equal(5, PageParts.Ending(three));
        Assert.Equal(0, PageParts.Ending("Only one."));
    }

    [Fact]
    public async Task A_sentence_or_line_can_be_asked_for_by_number()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One start");

        int before = h.Voice.Queue.Count;
        Assert.Null(await h.Session.GoToPartAsync(BookCommandKind.GoToSentence, 2));
        Assert.Equal("One end.", h.Voice.Queue[before + 1].Text);
        h.Voice.Hear("One end");
        Assert.Equal(11, h.Positions.Get(h.BookPath)!.Offset);

        Assert.Equal("This page has only 2 sentences.", await h.Session.GoToPartAsync(BookCommandKind.GoToSentence, 9));
        Assert.Equal("This page has only 1 lines.", await h.Session.GoToPartAsync(BookCommandKind.GoToLine, 2));
        Assert.Equal("One start. One end.", await h.Session.HeadingAsync());

        before = h.Voice.Queue.Count;
        Assert.Null(await h.Session.GoToPartAsync(BookCommandKind.GoToSentence, -1));
        Assert.Equal("One end.", h.Voice.Queue[before + 1].Text);
    }

    [Fact]
    public async Task Next_and_previous_page_work_from_what_was_heard()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One end");

        await h.Session.NextPageAsync();
        Assert.Contains(h.Voice.Queue, s => s.Text == "Two start.");
        h.Voice.Hear("Two start");
        Assert.Equal(2, h.Positions.Get(h.BookPath)!.Page);

        await h.Session.PreviousPageAsync();
        h.Voice.Hear("One start");
        Assert.Equal(1, h.Positions.Get(h.BookPath)!.Page);
        Assert.Equal("This is the first page.", await h.Session.PreviousPageAsync());
    }

    [Fact]
    public async Task A_page_out_of_range_asks_again_and_keeps_the_place()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One start");

        Assert.Equal("There are only 3 pages. Which one?", await h.Session.GoToPageAsync(50));
        Assert.Equal(1, h.Positions.Get(h.BookPath)!.Page);
    }

    [Fact]
    public async Task Where_says_the_place_and_continue_picks_it_up_after_a_stop()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One end");
        h.Voice.Hear("Two end");
        h.Session.OnStopped();
        h.Voice.GenerationValue++;

        Assert.False(h.Session.IsActive);
        Assert.Equal("My Book. Page 2 of 3, 67 percent. 1 pages left.", h.Session.Where());

        int before = h.Voice.Queue.Count;
        await h.Session.ContinueAsync();
        Assert.Equal("Two end.", h.Voice.Queue[before + 1].Text);
    }

    [Fact]
    public async Task Notes_and_marks_keep_their_page()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One start");
        Assert.Equal("Marked page 1.", h.Session.Mark(null));
        await h.Session.GoToPageAsync(3);
        h.Voice.Hear("Three start");
        Assert.Equal("Noted on page 3.", h.Session.Mark("ask about this"));

        Assert.Equal("Page 1: a bookmark. Page 3: ask about this.", h.Session.ReadNotes());
    }

    [Fact]
    public async Task The_sleep_timer_stops_reading_and_keeps_the_place()
    {
        using var h = new Harness();
        await h.Open();
        h.Voice.Hear("One end");
        h.Session.SetTimer(10);

        h.Session.OnTimer();
        Assert.False(h.Session.IsActive);
        Assert.Contains(h.Voice.Queue, s => s.Text.Contains("Time's up", StringComparison.Ordinal));
        Assert.Equal(1, h.Positions.Get(h.BookPath)!.Page);
        h.Session.CancelTimer();

        int before = h.Voice.Queue.Count;
        h.Session.OnTimer();
        Assert.Equal(before, h.Voice.Queue.Count);
    }

    [Fact]
    public void With_no_book_every_command_says_so() =>
        Assert.Equal("No book is open right now.", new Harness().Session.Where());

    [Fact]
    public void The_greeting_says_when_where_and_how_to_go_on()
    {
        var now = DateTimeOffset.Now;
        var place = new ReadingPosition(34, 120, now.AddDays(-1), "X", 312);

        Assert.Equal("Yesterday you stopped on page 34 of 312 of Phonology. Say continue to carry on.", ReadingJournal.Greeting(place, "Phonology", now, Lang.English));
        Assert.Contains("إمبارح", ReadingJournal.Greeting(place, "الكتاب", now, Lang.Arabic), StringComparison.Ordinal);
    }

    [Fact]
    public void Recent_lists_newest_first_with_the_page_count()
    {
        using var h = new Harness();
        h.Positions.Set(h.BookPath, 7, 0, 100);

        ReadingPosition latest = Assert.Single(h.Positions.Recent(5));
        Assert.Equal((7, 100), (latest.Page, latest.Pages));
    }

    [Theory]
    [InlineData("A normal page of English text that reads perfectly well from start to end.", PageKind.Clean)]
    [InlineData("هذه صفحة عادية من كتاب عربي مكتوبة بشكل صحيح تماما وليس فيها أي مشكلة أبدا.", PageKind.Clean)]
    [InlineData("ÇáßÊÇÈ ÇáÚÑÈí ÇáãßÊæÈ ÈÇáÊÑãíÒ ÇáÎØÃ ÝíåÇ ãÔßáÉ ßÈíÑÉ ÌÏÇ", PageKind.Broken)]
    [InlineData("\uE001\uE002\uE003\uE004\uE005\uE006 some text here that is otherwise fine enough", PageKind.Broken)]
    [InlineData("ا ل ك ت ا ب ا ل ع ر ب ي ا ل م ك ت و ب ه ن ا ب ش ك ل غ ل ط", PageKind.Broken)]
    [InlineData("The vowel in the word is written as [ʃəʊæŋθðɪʊɛɔʌɑ] in the phonetic alphabet.", PageKind.Symbols)]
    public void Page_quality_is_told_apart(string text, PageKind kind) => Assert.Equal(kind, PageQuality.Check(text));

    private sealed class Repair(string? ocr = null, string? image = "Fixed by image.") : IPageRepair
    {
        public int Images { get; private set; }

        public int Ocrs { get; private set; }

        public Task<string?> OcrAsync(int page, CancellationToken ct)
        {
            Ocrs++;
            return Task.FromResult(ocr);
        }

        public Task<string?> ImageAsync(int page, string reason, CancellationToken ct)
        {
            Images++;
            return Task.FromResult(image);
        }
    }

    private const string Bad = "ÇáßÊÇÈ ÇáÚÑÈí ÇáãßÊæÈ ÈÇáÊÑãíÒ ÇáÎØÃ ÝíåÇ ãÔßáÉ ßÈíÑÉ ÌÏÇ";

    private static CheckedPages Checked(IBookPages pages, Repair repair, PageImageBudget budget, bool cloud = true) =>
        new(pages, "book", repair, budget, () => cloud, () => DateTimeOffset.UtcNow, Lang.English);

    [Fact]
    public async Task A_clean_100_page_book_sends_no_images()
    {
        var repair = new Repair();
        var pages = Checked(new Pages(Enumerable.Repeat("A normal page of English text that reads perfectly well.", 100).ToArray()), repair, new PageImageBudget());
        for (int i = 1; i <= 100; i++)
        {
            await pages.GetAsync(i, TestContext.Current.CancellationToken);
        }

        Assert.Equal((0, 0), (repair.Images, repair.Ocrs));
    }

    [Fact]
    public async Task A_broken_book_stops_after_too_many_images_in_a_row_and_warns()
    {
        var repair = new Repair();
        var pages = Checked(new Pages(Enumerable.Repeat(Bad, 50).ToArray()), repair, new PageImageBudget());
        string? last = null;
        for (int i = 1; i <= 50; i++)
        {
            last = await pages.GetAsync(i, TestContext.Current.CancellationToken);
        }

        Assert.Equal(PageImageBudget.MaxInARow, repair.Images);
        Assert.Contains("hard to read", last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_book_stops_at_its_daily_images_even_with_good_pages_between()
    {
        var repair = new Repair();
        string[] mixed = Enumerable.Range(0, 60).Select(i => i % 2 == 0 ? Bad : "A normal page of English text that reads perfectly well.").ToArray();
        var pages = Checked(new Pages(mixed), repair, new PageImageBudget());
        for (int i = 1; i <= 60; i++)
        {
            await pages.GetAsync(i, TestContext.Current.CancellationToken);
        }

        Assert.Equal(PageImageBudget.MaxPerBookPerDay, repair.Images);
    }

    [Fact]
    public async Task A_repaired_page_is_never_paid_for_twice()
    {
        var repair = new Repair();
        var pages = Checked(new Pages(Bad), repair, new PageImageBudget());

        Assert.Equal("Fixed by image.", await pages.GetAsync(1, TestContext.Current.CancellationToken));
        Assert.Equal("Fixed by image.", await pages.GetAsync(1, TestContext.Current.CancellationToken));
        Assert.Equal(1, repair.Images);
    }

    [Fact]
    public async Task Local_ocr_comes_first_and_private_mode_never_sends_an_image()
    {
        var byOcr = new Repair(ocr: "A normal page of English text that reads perfectly well.");
        Assert.StartsWith("A normal", await Checked(new Pages(Bad), byOcr, new PageImageBudget()).GetAsync(1, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(0, byOcr.Images);

        var offline = new Repair();
        await Checked(new Pages(Bad), offline, new PageImageBudget(), cloud: false).GetAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(0, offline.Images);
    }

    [Fact]
    public void The_image_budget_survives_a_restart()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var now = DateTimeOffset.UtcNow;
            var first = new PageImageBudget(file);
            for (int i = 0; i < PageImageBudget.MaxPerBookPerDay; i++)
            {
                first.NoteGoodPage();
                Assert.True(first.TryTake("book", now));
            }

            first.Keep("book", 4, "kept");
            var second = new PageImageBudget(file);
            Assert.False(second.TryTake("book", now));
            Assert.Equal("kept", second.Repaired("book", 4));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Questions_find_the_right_passage_and_stay_small()
    {
        string filler = string.Join(' ', Enumerable.Repeat("Language changes slowly over many generations of speakers.", 40));
        string[] pages = Enumerable.Repeat(filler, 400).ToArray();
        pages[122] = "An allophone is a variant of a phoneme that does not change the meaning of a word.";

        IReadOnlyList<Passage> found = PassageFinder.Find(pages, "what's an allophone?", 1);

        Assert.Equal(123, found[0].Page);
        Assert.True(found.Sum(p => p.Text.Length) <= PassageFinder.MaxChars);
    }

    [Fact]
    public void Arabic_questions_find_passages_despite_spelling_and_tashkeel()
    {
        string[] pages = ["نص عن الزراعة في الريف.", "شرح اللُّغَةُ العربية وقواعدها.", "نص عن الطقس."];

        Assert.Equal(2, PassageFinder.Find(pages, "يعني ايه اللغة", 1)[0].Page);
    }

    [Fact]
    public void A_vague_question_uses_where_they_are_and_the_opening()
    {
        string[] pages = ["Opening.", "Second.", "Third.", "Fourth."];

        Assert.Equal([3, 1, 2], PassageFinder.Find(pages, "what's this about?", 3).Select(p => p.Page));
    }

    [Fact]
    public void An_arabic_question_about_an_english_book_needs_keywords()
    {
        string[] pages = ["An allophone is a variant of a phoneme."];

        Assert.False(PassageFinder.Matches(pages, "يعني ايه الوحدة الصوتية"));
        Assert.True(PassageFinder.Matches(pages, "يعني ايه allophone"));
        Assert.Equal(1, PassageFinder.Find(pages, "يعني ايه الوحدة الصوتية allophone phoneme", 1)[0].Page);
    }

    [Fact]
    public void Real_quotes_stay_and_invented_ones_are_taken_out()
    {
        Passage[] passages = [new Passage(3, "An allophone is a variant of a phoneme.")];

        var (answer, verified, removed) = QuoteChecker.Check("It says «a variant of a phoneme» and «it never changes».", passages);

        Assert.Equal("It says a variant of a phoneme and .", answer);
        Assert.Equal((1, 1), (verified, removed));
    }
}
