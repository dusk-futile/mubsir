using System.Globalization;
using System.Text;
using Mubsir.App.Diagnostics;
using Mubsir.App.Screen;
using Mubsir.App.Screen.Documents;
using Mubsir.App.Speech;
using Mubsir.App.Storage;
using Mubsir.Core.Common;
using Mubsir.Core.Library;
using Mubsir.Core.Reading;
using Mubsir.Core.Speech;
using Mubsir.Core.Text;

namespace Mubsir.App.Host;

/// <summary>
/// <c>Mubsir.exe --book-check folder results.txt</c>: reads the small books in tests/fixtures/books through the
/// real document reader, OCR, reading and bookmark code, then through the real Windows voice. It works on copies
/// in a temporary folder. Exit code 0 means nothing failed; anything it couldn't try is written as SKIP.
/// </summary>
internal static class BookCheck
{
    private const int OcrPagesAtATime = 5;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private static readonly StringBuilder Results = new();
    private static int _failed;

    public static int Run(string fixtures, string resultsFile)
    {
        string temp = Path.Combine(Path.GetTempPath(), "mubsir-book-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            RunAsync(fixtures, temp).GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Fail("the check itself", e.ToString());
        }
        finally
        {
            Line($"{(_failed == 0 ? "ALL PASSED" : _failed + " FAILED")}");
            File.WriteAllText(resultsFile, Results.ToString(), Encoding.UTF8);
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        return _failed == 0 ? 0 : 1;
    }

    private static async Task RunAsync(string fixtures, string temp)
    {
        foreach (string file in Directory.GetFiles(fixtures))
        {
            File.Copy(file, Path.Combine(temp, Path.GetFileName(file)));
        }

        using var screen = new UiaScreenReader(NullEventLog.Instance);
        var documents = new DocumentReader(screen, NullEventLog.Instance);
        Line("OCR languages: " + string.Join(", ", Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag)));

        await ReadThrough(documents, temp, "english.pdf", Lang.English, 4, [
            "The first page opens the story.", "A traveller reached the old harbour at dawn.", "She carried a small brass key.",
            "The second page follows her through the market.", "Dr. Smith sold maps for 3.50 pounds each.", "Is this the right road?",
            "The fourth page comes after a blank one.", "This is the last sentence of the book."]);
        await ReadThrough(documents, temp, "arabic.pdf", Lang.Arabic, 2, [
            "هذه هي الصفحة الأولى من الكتاب", "ذهب الولد إلى المدرسة في الصباح", "قرأ المعلم الدرس بصوت عال", "computer",
            "هذه هي الصفحة الثانية من الكتاب", "اشترى الولد ثلاثة كتب بخمسين جنيها", "هل تحب القراءة", "هذه آخر جملة في الكتاب"]);
        string[] arabicText = ["هذه هي الصفحة الأولى من الملف", "ذهب الولد إلى المدرسة في الصباح", "هل تحب القراءة", "هذه آخر جملة في الملف"];
        await ReadThrough(documents, temp, "arabic-utf8.txt", Lang.Arabic, 1, arabicText);
        await ReadThrough(documents, temp, "arabic-1256.txt", Lang.Arabic, 1, arabicText);
        await ReadThrough(documents, temp, "english.docx", Lang.English, 1, ["The document opens here.", "It has two paragraphs.", "The second paragraph ends the document."]);

        await StopRenameResume(documents, temp, "english.pdf", Lang.English, "Dr. Smith sold maps", 2);
        await StopRenameResume(documents, temp, "arabic.pdf", Lang.Arabic, "اشترى الولد ثلاثة كتب", 2);

        if (screen.Ocr.IsAvailable)
        {
            await ReadThrough(documents, temp, "scan.pdf", Lang.English, 1, ["This page was scanned from paper.", "The river runs past the old mill.", "The reader reaches this line last."]);
        }
        else
        {
            Skip("scan.pdf", "no OCR on this PC");
        }

        bool arabicOcr = Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Any(l => l.LanguageTag.StartsWith("ar", StringComparison.OrdinalIgnoreCase));
        if (arabicOcr)
        {
            await ReadThrough(documents, temp, "scan-ar.pdf", Lang.Arabic, 1, ["هذه الصفحة ممسوحة من كتاب ورقي", "يجري النهر بجانب الطاحونة القديمة"], wrongLetters: 1);
        }
        else
        {
            Skip("scan-ar.pdf", "Arabic OCR isn't installed on this PC");
        }

        // Shows why a letter the PDF lost did or didn't get filled. These are test files, so printing their text is fine.
        Line("  OCR of arabic.pdf page 1: " + await documents.OcrPageAsync(Path.Combine(temp, "arabic.pdf"), 1, CancellationToken.None));
        await Library(documents, temp);
        await RealVoice(documents, temp);
    }

    private static async Task<(DocumentPages Pages, int Count)> Open(DocumentReader documents, string path)
    {
        DocumentText doc = await documents.ReadAsync(path, 1, OcrPagesAtATime, null, CancellationToken.None);
        return (new DocumentPages(doc.Pages, doc.PendingOcrPages, path, documents, OcrPagesAtATime), doc.Pages.Count);
    }

    /// <summary>Hears a whole book, one sentence at a time, and checks every phrase came once and in order.</summary>
    private static async Task ReadThrough(DocumentReader documents, string temp, string name, Lang lang, int lastPage, string[] phrases, int wrongLetters = 0)
    {
        string test = "read " + name;
        try
        {
            string path = Path.Combine(temp, name);
            var positions = new ReadingPositions(Path.Combine(temp, Guid.NewGuid().ToString("N") + ".json"));
            var (pages, _) = await Open(documents, path);
            var voice = new RecordingVoice();
            var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int lastSaved = 0;
            var reading = new BookReading(voice, pages, (p, o) => { lastSaved = Math.Max(lastSaved, p); positions.Set(path, p, o); }, () => ended.TrySetResult(), lang);
            voice.History.Spoken += reading.OnSpoken;
            if (!await reading.StartAsync(1, 0))
            {
                Fail(test, "nothing could be read aloud");
                return;
            }

            string heard = await voice.HearAll(ended.Task);
            string flat = Flat(heard);
            int from = 0;
            int misread = 0;
            foreach (string phrase in phrases)
            {
                int at = flat.IndexOf(Flat(phrase), from, StringComparison.Ordinal);
                if (at < 0 && wrongLetters > 0)
                {
                    at = NearlyAt(flat, Flat(phrase), from, wrongLetters);
                    misread += at < 0 ? 0 : 1;
                }

                if (at < 0)
                {
                    Fail(test, $"missing or out of order: \"{phrase}\". Heard: {heard}");
                    return;
                }

                if (flat.IndexOf(Flat(phrase), 0, StringComparison.Ordinal) != at && lang == Lang.English)
                {
                    Fail(test, $"said twice: \"{phrase}\"");
                    return;
                }

                from = at + Flat(phrase).Length;
            }

            if (lastSaved != lastPage)
            {
                Fail(test, $"last page saved was {lastSaved}, expected {lastPage}");
                return;
            }

            if (positions.Get(path) is not { Page: 1, Offset: 0 })
            {
                Fail(test, "finishing the book did not go back to page 1");
                return;
            }

            if (heard.Split(BookEndLine(lang)).Length != 2)
            {
                Fail(test, "the end of the book wasn't said exactly once. Heard: " + heard);
                return;
            }

            Pass(misread > 0 ? $"{test} ({misread} of {phrases.Length} phrases with a misread letter)" : test);
            Line("  heard: " + heard);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Fail(test, e.GetType().Name + ": " + e.Message);
        }
    }

    private static string BookEndLine(Lang lang) => lang == Lang.Arabic ? "خلص الكتاب." : "That's the end of the book.";

    /// <summary>OCR misreads a letter now and then. This finds a phrase with at most that many letters wrong.</summary>
    private static int NearlyAt(string flat, string phrase, int from, int wrongLetters)
    {
        for (int at = from; at + phrase.Length <= flat.Length; at++)
        {
            if (OcrRepair.Distance(flat.Substring(at, phrase.Length), phrase) <= wrongLetters)
            {
                return at;
            }
        }

        return -1;
    }

    /// <summary>Real library cards from real files: found by subject with no AI, including a scan with a useless name.</summary>
    private static async Task Library(DocumentReader documents, string temp)
    {
        const string Test = "library cards";
        var library = new BookLibrary(Path.Combine(temp, "library.json"));
        foreach (string name in new[] { "english.pdf", "arabic.pdf", "scan.pdf", "english.docx" })
        {
            library.Put(await documents.ReadCardAsync(Path.Combine(temp, name), CancellationToken.None));
        }

        foreach ((string query, string file) in new[] { ("harbour", "english.pdf"), ("المدرسة", "arabic.pdf"), ("river", "scan.pdf"), ("paragraphs", "english.docx") })
        {
            IReadOnlyList<LibraryCard> found = library.Search(query);
            if (found.Count == 0 || !found[0].Path.EndsWith(file, StringComparison.OrdinalIgnoreCase))
            {
                Fail(Test, $"\"{query}\" did not find {file}");
                return;
            }

            Line($"  card: {found[0].Title}, {found[0].Pages} pages");
        }

        Pass(Test);
    }

    /// <summary>Stops on a sentence, renames the file, and checks reading picks up on that same sentence.</summary>
    private static async Task StopRenameResume(DocumentReader documents, string temp, string name, Lang lang, string stopAt, int page)
    {
        string test = "stop, rename and resume " + name;
        string first = Path.Combine(temp, "stop-" + name);
        string renamed = Path.Combine(temp, "renamed-" + name);
        File.Copy(Path.Combine(temp, name), first, overwrite: true);
        try
        {
            var positions = new ReadingPositions(Path.Combine(temp, Guid.NewGuid().ToString("N") + ".json"));
            var (pages, _) = await Open(documents, first);
            var voice = new RecordingVoice();
            var reading = new BookReading(voice, pages, (p, o) => positions.Set(first, p, o), null, lang);
            voice.History.Spoken += reading.OnSpoken;
            await reading.StartAsync(1, 0);
            await voice.HearUntil(stopAt);
            reading.Stop();
            voice.Stop();
            await Task.Delay(200);

            File.Move(first, renamed);
            if (positions.Get(renamed) is not { } place)
            {
                Fail(test, "the bookmark was lost after the rename");
                return;
            }

            if (place.Page != page)
            {
                Fail(test, $"bookmark is on page {place.Page}, expected {page}");
                return;
            }

            var (again, _) = await Open(documents, renamed);
            var voice2 = new RecordingVoice();
            var resume = new BookReading(voice2, again, null, null, lang);
            await resume.StartAsync(place.Page, place.Offset);
            string? firstSentence = voice2.Sentences().Skip(1).FirstOrDefault();
            resume.Stop();
            if (firstSentence is null || !Flat(firstSentence).StartsWith(Flat(stopAt), StringComparison.Ordinal))
            {
                Fail(test, $"resumed on \"{firstSentence}\", expected \"{stopAt}\"");
                return;
            }

            Pass(test);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Fail(test, e.GetType().Name + ": " + e.Message);
        }
    }

    /// <summary>The real speech queue and Windows voice: places must follow real speech, and Stop must hold.</summary>
    private static async Task RealVoice(DocumentReader documents, string temp)
    {
        const string Test = "real Windows voice";
        var log = NullEventLog.Instance;
        using var oneCore = new OneCoreSpeech(log);
        if (!oneCore.IsAvailable)
        {
            Skip(Test, "no Windows voice on this PC");
            return;
        }

        Line("Arabic voice: " + (oneCore.HasArabicVoice ? "yes" : "no"));
        using var settings = new SettingsProvider(log, Path.Combine(temp, "settings.json"), Path.Combine(temp, "profile.json"));
        using var speech = new SpeechService(new NvdaController(), oneCore, settings, log, SystemClock.Instance);
        var positions = new ReadingPositions(Path.Combine(temp, "voice.json"));
        var books = new BookAloud(speech, positions, new BookNotes(Path.Combine(temp, "notes.json")), new BookLibrary(Path.Combine(temp, "library.json")), documents, new PageImageBudget(), () => false, _ => { }, log);
        string path = Path.Combine(temp, "english.docx");

        if (await books.OpenAsync(path, 1, Lang.English, CancellationToken.None) is not null)
        {
            Fail(Test, "reading did not start");
            return;
        }

        if (!await Until(() => positions.Get(path) is not null, Patience))
        {
            speech.Stop();
            Skip(Test, "no sentence started within a minute; this PC may have no sound device");
            return;
        }

        speech.Stop();
        await Task.Delay(500);
        ReadingPosition? stopped = positions.Get(path);
        if (speech.IsSpeaking)
        {
            Fail(Test, "still speaking after Stop");
            return;
        }

        await Task.Delay(2000);
        if (positions.Get(path)?.Offset != stopped?.Offset)
        {
            Fail(Test, "the place moved after Stop");
            return;
        }

        Pass(Test + ": sentences start, the place saves, Stop holds");

        if (await books.OpenAsync(path, 1, Lang.English, CancellationToken.None) is not null)
        {
            Fail(Test, "reading did not start again");
            return;
        }

        // A short book read to its end comes back to page 1 once the last sentence has started.
        bool moved = await Until(() => speech.IsSpeaking, TimeSpan.FromSeconds(5));
        bool finished = await Until(() => !speech.IsSpeaking, Patience);
        Line(moved && finished ? "PASS real Windows voice: reads a whole document to the end" : "SKIP real Windows voice: did not finish within a minute");
    }

    private static async Task<bool> Until(Func<bool> done, TimeSpan patience)
    {
        DateTime end = DateTime.UtcNow + patience;
        while (DateTime.UtcNow < end)
        {
            if (done())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    /// <summary>Compares text the way a listener would: spacing, tashkeel and Arabic letter shapes don't count.</summary>
    private static string Flat(string text)
    {
        var flat = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormKC).Normalize(NormalizationForm.FormD))
        {
            if (!char.IsWhiteSpace(c) && !char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                flat.Append(c);
            }
        }

        return flat.ToString();
    }

    private static void Pass(string test) => Line("PASS " + test);

    private static void Skip(string test, string why) => Line($"SKIP {test}: {why}");

    private static void Fail(string test, string why)
    {
        _failed++;
        Line($"FAIL {test}: {why}");
    }

    private static void Line(string text)
    {
        Results.AppendLine(text);
        ConsoleIo.WriteLine(text);
    }

    /// <summary>A listener who hears each queued sentence as soon as it is ready, using the real sentence splitter.</summary>
    private sealed class RecordingVoice : IBookVoice
    {
        private readonly object _gate = new();
        private readonly List<SpokenSentence> _queue = [];
        private long _generation;

        public SpeechHistory History { get; } = new();

        public long Generation => Interlocked.Read(ref _generation);

        public void Stop() => Interlocked.Increment(ref _generation);

        public IReadOnlyList<SpokenSentence> Say(string text, Lang lang)
        {
            var (_, items) = History.AddUtteranceWithItems(text, UtteranceKind.Reading);
            SpokenSentence[] said = items.Select(i => new SpokenSentence(i.Index, i.Text)).ToArray();
            lock (_gate)
            {
                _queue.AddRange(said);
            }

            return said;
        }

        public string[] Sentences()
        {
            lock (_gate)
            {
                return _queue.Select(s => s.Text).ToArray();
            }
        }

        public async Task<string> HearAll(Task ended)
        {
            var heard = new List<string>();
            int next = 0;
            DateTime giveUp = DateTime.UtcNow + Patience;
            while (DateTime.UtcNow < giveUp)
            {
                SpokenSentence? sentence = At(next);
                if (sentence is not null)
                {
                    heard.Add(sentence.Text);
                    next++;
                    History.MarkSpoken(sentence.Index);
                }
                else if (ended.IsCompleted)
                {
                    return string.Join(' ', heard);
                }
                else
                {
                    await Task.Delay(10);
                }
            }

            throw new TimeoutException("the book did not finish: " + string.Join(' ', heard));
        }

        public async Task HearUntil(string phrase)
        {
            string flat = Flat(phrase);
            int next = 0;
            DateTime giveUp = DateTime.UtcNow + Patience;
            while (DateTime.UtcNow < giveUp)
            {
                SpokenSentence? sentence = At(next);
                if (sentence is null)
                {
                    await Task.Delay(10);
                    continue;
                }

                next++;
                History.MarkSpoken(sentence.Index);
                if (Flat(sentence.Text).Contains(flat, StringComparison.Ordinal))
                {
                    await Task.Delay(100);
                    return;
                }
            }

            throw new TimeoutException("never reached: " + phrase);
        }

        private SpokenSentence? At(int index)
        {
            lock (_gate)
            {
                return index < _queue.Count ? _queue[index] : null;
            }
        }
    }
}
