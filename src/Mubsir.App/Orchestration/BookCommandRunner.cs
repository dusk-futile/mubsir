using Mubsir.App.Speech;
using Mubsir.Core.Common;
using Mubsir.Core.Library;
using Mubsir.Core.Localization;
using Mubsir.Core.Reading;
using Mubsir.Core.Router;
using Mubsir.Core.Speech;

namespace Mubsir.App.Orchestration;

/// <summary>Everything a reader says about a book, handled on this PC. Returns false to let the request go on as usual,
/// so "next page" on a website still reaches the AI when no book is in hand.</summary>
internal sealed class BookCommandRunner(AppServices app)
{
    private static readonly TimeSpan ChoiceLasts = TimeSpan.FromMinutes(1);

    private volatile Choices _choices = new([], default);
    private CancellationTokenSource? _phraseSearch;

    private bool Choosing => _choices is { Items.Count: > 0 } c && app.Clock.UtcNow - c.At < ChoiceLasts;

    public async Task<bool> TryRunAsync(string text, Lang lang)
    {
        if (app.Books is not { } books)
        {
            return false;
        }

        BookSession session = books.Session;
        BookCommand? command = BookCommands.Parse(text, Choosing);
        if (command is null)
        {
            if (session.Engaged && CommandGrammar.Match(text)?.Command == LocalCommand.WhereAmI)
            {
                await ReplyAndResume(session, session.Where(), lang).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        if (command.Kind != BookCommandKind.Pick)
        {
            _choices = new([], default);
        }

        switch (command.Kind)
        {
            case BookCommandKind.Pick:
                await PickAsync(books, command.Number ?? 0, lang).ConfigureAwait(false);
                return true;
            case BookCommandKind.FindBook:
                return await FindAsync(books, command.Text ?? string.Empty, lang).ConfigureAwait(false);
            case BookCommandKind.FindPhrase:
                FindPhrase(books, command.Text ?? string.Empty, lang);
                return true;
            case BookCommandKind.ReadingJournal:
                Say(ReadingJournal.Summary(books.Journal(3), app.Clock.UtcNow, lang), lang);
                return true;
            case BookCommandKind.Continue:
                return await ContinueAsync(books, lang).ConfigureAwait(false);
            case BookCommandKind.Speed:
                await ReplyAndResume(session, ChangeSpeed(command.Number ?? 0, lang), lang).ConfigureAwait(false);
                return true;
        }

        if (!session.Engaged)
        {
            return false;
        }

        switch (command.Kind)
        {
            case BookCommandKind.NextPage:
                SayIf(await session.NextPageAsync().ConfigureAwait(false), lang);
                break;
            case BookCommandKind.PreviousPage:
                SayIf(await session.PreviousPageAsync().ConfigureAwait(false), lang);
                break;
            case BookCommandKind.RepeatPage:
                SayIf(await session.RepeatPageAsync().ConfigureAwait(false), lang);
                break;
            case BookCommandKind.GoToPage:
                SayIf(await session.GoToPageAsync(command.Number ?? 0).ConfigureAwait(false), lang);
                break;
            case BookCommandKind.GoToLine or BookCommandKind.GoToSentence or BookCommandKind.GoToParagraph:
                SayIf(await session.GoToPartAsync(command.Kind, command.Number ?? 1).ConfigureAwait(false), lang);
                break;
            case BookCommandKind.ReadEnding:
                SayIf(await session.EndingAsync().ConfigureAwait(false), lang);
                break;
            case BookCommandKind.ReadHeading:
                await ReplyAndResume(session, await session.HeadingAsync().ConfigureAwait(false) ?? string.Empty, lang).ConfigureAwait(false);
                break;
            case BookCommandKind.WhereInBook:
                await ReplyAndResume(session, session.Where(), lang).ConfigureAwait(false);
                break;
            case BookCommandKind.Mark:
                await ReplyAndResume(session, session.Mark(null), lang).ConfigureAwait(false);
                break;
            case BookCommandKind.Note:
                await ReplyAndResume(session, session.Mark(command.Text), lang).ConfigureAwait(false);
                break;
            case BookCommandKind.ReadNotes:
                Say(session.ReadNotes(), lang);
                break;
            case BookCommandKind.SleepTimer:
                await ReplyAndResume(session, session.SetTimer(command.Number ?? 1), lang).ConfigureAwait(false);
                break;
            case BookCommandKind.CancelTimer:
                await ReplyAndResume(session, session.CancelTimer(), lang).ConfigureAwait(false);
                break;
        }

        return true;
    }

    private async Task<bool> ContinueAsync(BookAloud books, Lang lang)
    {
        if (books.Session.HasBook)
        {
            SayIf(await books.Session.ContinueAsync().ConfigureAwait(false), lang);
            return true;
        }

        // "Continue" with a task on hold means the task, not yesterday's book.
        if (app.Input.AgentRunning || books.LastPlace() is not { Path: { } path })
        {
            return false;
        }

        SayIf(await books.OpenAsync(path, null, lang, CancellationToken.None).ConfigureAwait(false), lang);
        return true;
    }

    /// <summary>False when no book matched, so the AI can try: it may be a question about books, or something online.</summary>
    private async Task<bool> FindAsync(BookAloud books, string query, Lang lang)
    {
        app.Speech.PlayEarcon(Earcon.Thinking);
        IReadOnlyList<LibraryCard> found = await books.FindBooksAsync(query, lang, CancellationToken.None).ConfigureAwait(false);
        if (found.Count == 0)
        {
            return false;
        }

        if (found.Count == 1)
        {
            await OpenCardAsync(books, found[0], lang).ConfigureAwait(false);
            return true;
        }

        _choices = new(found.Select(c => new PhraseHit(c)).ToArray(), app.Clock.UtcNow);
        Say(BookListText.List(found, lang), lang);
        return true;
    }

    /// <summary>Runs behind, so the reader can go on with something else: it says it has started, and says the answer
    /// when it's done. A new search ends the one before.</summary>
    private void FindPhrase(BookAloud books, string phrase, Lang lang)
    {
        _phraseSearch?.Cancel();
        var search = _phraseSearch = new CancellationTokenSource();
        Say(lang == Lang.Arabic ? $"بدور في {books.Library.Count} كتاب. هقولك لما أخلص." : $"Searching {books.Library.Count} books. I'll tell you when I'm done.", lang);
        _ = Task.Run(async () =>
        {
            try
            {
                (IReadOnlyList<PhraseHit> hits, int searched) = await books.FindPhraseAsync(phrase, search.Token).ConfigureAwait(false);
                _choices = new(hits, app.Clock.UtcNow);
                app.Speech.PlayEarcon(Earcon.Done);
                app.Speech.Speak(PhraseSearch.Heard(hits, searched, lang), UtteranceKind.System, lang);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                app.Log.Failure("phrase_search", e);
            }
        });
    }

    private async Task PickAsync(BookAloud books, int number, Lang lang)
    {
        IReadOnlyList<PhraseHit> items = _choices.Items;
        if (number < 1 || number > items.Count)
        {
            Say(lang == Lang.Arabic ? $"فيه {items.Count} بس. أنهي واحد؟" : $"There are only {items.Count}. Which one?", lang);
            return;
        }

        _choices = new([], default);
        await OpenCardAsync(books, items[number - 1].Card, lang, items[number - 1].Page).ConfigureAwait(false);
    }

    private async Task OpenCardAsync(BookAloud books, LibraryCard card, Lang lang, int page = 0)
    {
        Say(lang == Lang.Arabic ? $"بفتح {card.Title}." : $"Opening {card.Title}.", lang);
        SayIf(await books.OpenAsync(card.Path, page > 0 ? page : null, lang, CancellationToken.None).ConfigureAwait(false), lang);
    }

    private sealed record Choices(IReadOnlyList<PhraseHit> Items, DateTimeOffset At);

    /// <summary>One percent is one step of the rate: 50 is the voice's normal speed, and each step is 1% of it.</summary>
    private string ChangeSpeed(int percent, Lang lang)
    {
        int before = app.Speech.Rate;
        int after = Math.Clamp(before + percent, 0, 100);
        if (after == before)
        {
            return AppText.Get(AppPhrase.SpeedAtLimit, lang);
        }

        app.Speech.Rate = after;
        app.Settings.Update(s => s with { SpeechRate = after });
        return Strings.Get(percent > 0 ? StringKey.SpeakingFaster : StringKey.SpeakingSlower, lang);
    }

    /// <summary>A short reply, then straight back to the book if talking to Mubsir is what stopped it.</summary>
    private async Task ReplyAndResume(BookSession session, string reply, Lang lang)
    {
        bool resume = session.JustInterrupted;
        Say(reply, lang);
        if (resume)
        {
            await session.ContinueAsync().ConfigureAwait(false);
        }
    }

    private void SayIf(string? text, Lang lang)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Say(text, lang);
        }
    }

    private void Say(string text, Lang lang) => app.Speech.Speak(text, UtteranceKind.System, lang, interrupt: true);
}
