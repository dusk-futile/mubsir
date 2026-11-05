using Mubsir.Core.Common;
using Mubsir.Core.Reading;
using Mubsir.Core.Speech;

namespace Mubsir.Core.Tests.Reading;

public class BookReadingTests
{
    private sealed class Voice : IBookVoice
    {
        public SpeechHistory History { get; } = new();

        public long Generation { get; set; }

        public bool Off { get; set; }

        public List<SpokenSentence> Queue { get; } = [];

        public IReadOnlyList<SpokenSentence> Say(string text, Lang lang)
        {
            if (Off)
            {
                return [];
            }

            var (_, items) = History.AddUtteranceWithItems(text, UtteranceKind.Reading);
            var said = items.Select(i => new SpokenSentence(i.Index, i.Text)).ToArray();
            Queue.AddRange(said);
            return said;
        }

        public SpokenSentence Find(string text) => Queue.First(s => s.Text.Contains(text, StringComparison.Ordinal));

        public bool Queued(string text) => Queue.Any(s => s.Text.Contains(text, StringComparison.Ordinal));
    }

    private sealed class Pages(params string?[] pages) : IBookPages
    {
        public int Count => pages.Length;

        public Task<string?> GetAsync(int page, CancellationToken ct) => Task.FromResult(pages[page - 1]);
    }

    private sealed class Harness
    {
        public Voice Voice { get; } = new();

        public (int Page, int Offset)? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public int Ended { get; private set; }

        public BookReading Reading { get; }

        public Harness(params string?[] pages)
        {
            Reading = new BookReading(Voice, new Pages(pages), (p, o) => { Saved = (p, o); SaveCount++; }, () => Ended++, Lang.English);
            Voice.History.Spoken += Reading.OnSpoken;
        }

        public void Hear(string text) => Voice.History.MarkSpoken(Voice.Find(text).Index);
    }

    private const string PageOne = "First sentence here. Second sentence here. Third sentence here.";
    private const string PageTwo = "Fourth sentence here. Fifth sentence here.";
    private const string PageThree = "Sixth sentence here.";

    [Fact]
    public async Task The_end_of_the_book_is_said_once_however_many_sentences_the_last_page_has()
    {
        var h = new Harness(PageOne);
        await h.Reading.StartAsync(1, 0);
        h.Hear("First");
        h.Hear("Second");
        h.Hear("Third");
        Assert.Single(h.Voice.Queue, s => s.Text.Contains("end of the book", StringComparison.Ordinal));
        h.Hear("end of the book");
        Assert.Equal(1, h.Ended);
        Assert.Equal((1, 0), h.Saved);
    }

    [Fact]
    public async Task Nothing_is_saved_until_a_sentence_is_heard()
    {
        var h = new Harness(PageOne, PageTwo);
        Assert.True(await h.Reading.StartAsync(1, 0));
        Assert.True(h.Voice.Queued("Third"));
        Assert.Null(h.Saved);
    }

    [Fact]
    public async Task The_place_is_the_start_of_the_sentence_being_heard()
    {
        var h = new Harness(PageOne, PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Hear("Second");
        Assert.Equal((1, PageOne.IndexOf("Second", StringComparison.Ordinal)), h.Saved);
    }

    [Fact]
    public async Task Stopping_before_hearing_anything_keeps_the_old_place()
    {
        var h = new Harness(PageOne, PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Reading.Stop();
        Assert.Null(h.Saved);
        Assert.Equal(1, h.Ended);
    }

    [Fact]
    public async Task Only_one_page_is_queued_ahead_of_what_is_heard()
    {
        var h = new Harness(PageOne, PageTwo, PageThree);
        await h.Reading.StartAsync(1, 0);
        Assert.False(h.Voice.Queued("Fourth"));

        h.Hear("First");
        Assert.True(h.Voice.Queued("Fourth"));
        Assert.False(h.Voice.Queued("Sixth"));

        h.Hear("Second");
        Assert.False(h.Voice.Queued("Sixth"));

        h.Hear("Fourth");
        Assert.True(h.Voice.Queued("Sixth"));
        Assert.Equal(1, h.Voice.Queue.Count(s => s.Text.Contains("Fourth", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Nothing_more_is_queued_after_a_stop_but_going_back_still_moves_the_place()
    {
        var h = new Harness(PageOne, PageTwo, PageThree);
        await h.Reading.StartAsync(1, 0);
        h.Hear("Second");
        h.Reading.Stop();

        h.Hear("Fourth");
        Assert.False(h.Voice.Queued("Sixth"));
        Assert.Equal((2, 0), h.Saved);

        h.Hear("First");
        Assert.Equal((1, 0), h.Saved);
    }

    [Fact]
    public async Task A_stop_seen_only_as_a_new_generation_stops_the_feeding()
    {
        var h = new Harness(PageOne, PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Voice.Generation++;
        h.Hear("First");
        Assert.False(h.Voice.Queued("Fourth"));
    }

    [Fact]
    public async Task Resuming_reads_from_the_saved_sentence_and_says_the_page()
    {
        var h = new Harness(PageOne, PageTwo);
        int offset = PageOne.IndexOf("Second", StringComparison.Ordinal);
        await h.Reading.StartAsync(1, offset);
        Assert.False(h.Voice.Queued("First"));
        Assert.Equal("Page 1 of 2.", h.Voice.Queue[0].Text);

        h.Hear("Page 1");
        Assert.Equal((1, offset), h.Saved);
        h.Hear("Third");
        Assert.Equal((1, PageOne.IndexOf("Third", StringComparison.Ordinal)), h.Saved);
    }

    [Fact]
    public async Task Hearing_the_end_of_the_book_sends_the_place_back_to_the_start()
    {
        var h = new Harness(PageOne);
        await h.Reading.StartAsync(1, 0);
        h.Hear("Third");
        Assert.Equal(0, h.Ended);

        h.Hear("end of the book");
        Assert.Equal((1, 0), h.Saved);
        Assert.Equal(1, h.Ended);
        Assert.False(h.Reading.IsActive);
    }

    [Fact]
    public async Task Blank_pages_are_skipped_without_a_word()
    {
        var h = new Harness(PageOne, "  \n ", "", PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Hear("First");
        Assert.True(h.Voice.Queued("Page 4."));
        Assert.False(h.Voice.Queued("Page 2"));
        h.Hear("Fifth");
        Assert.Equal(4, h.Saved?.Page);
    }

    [Fact]
    public async Task An_unreadable_page_is_named_and_reading_goes_on()
    {
        var h = new Harness(PageOne, null, PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Hear("First");
        Assert.True(h.Voice.Queued("couldn't read page 2"));
        Assert.True(h.Voice.Queued("Fourth"));
        Assert.True(h.Reading.IsActive);
    }

    [Fact]
    public async Task Three_unreadable_pages_in_a_row_stop_the_reading_without_losing_the_place()
    {
        var h = new Harness(PageOne, null, null, null, PageTwo);
        await h.Reading.StartAsync(1, 0);
        h.Hear("Second");

        Assert.False(h.Reading.IsActive);
        Assert.Equal(1, h.Ended);
        Assert.True(h.Voice.Queued("so I stopped"));
        Assert.False(h.Voice.Queued("Fourth"));
        Assert.Equal((1, PageOne.IndexOf("Second", StringComparison.Ordinal)), h.Saved);
    }

    [Fact]
    public async Task With_speech_off_it_says_it_could_not_start()
    {
        var h = new Harness(PageOne);
        h.Voice.Off = true;
        Assert.False(await h.Reading.StartAsync(1, 0));
        Assert.False(h.Reading.IsActive);
    }

    [Fact]
    public async Task A_saved_offset_past_the_end_of_a_changed_page_starts_the_page_again()
    {
        var h = new Harness(PageThree);
        await h.Reading.StartAsync(1, 5000);
        Assert.True(h.Voice.Queued("Sixth"));
    }

    [Fact]
    public async Task A_saved_page_past_the_end_of_a_shorter_book_reads_the_last_page()
    {
        var h = new Harness(PageOne, PageTwo);
        await h.Reading.StartAsync(90, 0);
        Assert.True(h.Voice.Queued("Page 2 of 2."));
    }

    [Fact]
    public async Task Sentences_in_arabic_with_wrapped_lines_land_on_their_real_start()
    {
        const string page = "الجملة الأولى هنا.\nالجملة الثانية هنا!\n\nالفقرة التانية تبدأ هنا. وبعدين جملة كمان.";
        var h = new Harness(page);
        await h.Reading.StartAsync(1, 0);
        foreach (string start in new[] { "الجملة الثانية", "الفقرة التانية", "وبعدين" })
        {
            h.Hear(start);
            Assert.Equal((1, page.IndexOf(start, StringComparison.Ordinal)), h.Saved);
        }
    }

    [Fact]
    public async Task The_place_is_saved_once_per_sentence_heard()
    {
        var h = new Harness(PageOne);
        await h.Reading.StartAsync(1, 0);
        h.Hear("First");
        h.Hear("Second");
        Assert.Equal(2, h.SaveCount);
    }

    [Fact]
    public void An_unfindable_sentence_resumes_early_rather_than_skipping()
    {
        int[] offsets = SentenceLocator.Locate("One two. Three four. Five.", ["One two.", "Not on the page.", "Five."], 0);
        Assert.Equal([0, 8, 21], offsets);
    }
}
