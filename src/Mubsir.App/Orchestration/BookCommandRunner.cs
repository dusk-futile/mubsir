using Mubsir.App.Speech;
using Mubsir.Core.Common;
using Mubsir.Core.Library;
using Mubsir.Core.Reading;
using Mubsir.Core.Router;
using Mubsir.Core.Speech;

namespace Mubsir.App.Orchestration;

/// <summary>Everything a reader says about a book, handled on this PC. Returns false to let the request go on as usual,
/// so "next page" on a website still reaches the AI when no book is in hand.</summary>
internal sealed class BookCommandRunner(AppServices app)
{
    private static readonly TimeSpan ChoiceLasts = TimeSpan.FromMinutes(3);

    private IReadOnlyList<LibraryCard> _choices = [];
    private DateTimeOffset _choicesAt;

    private bool Choosing => _choices.Count > 0 && app.Clock.UtcNow - _choicesAt < ChoiceLasts;

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

        switch (command.Kind)
        {
            case BookCommandKind.Pick:
                await PickAsync(books, command.Number ?? 0, lang).ConfigureAwait(false);
                return true;
            case BookCommandKind.FindBook:
                await FindAsync(books, command.Text ?? string.Empty, lang).ConfigureAwait(false);
                return true;
            case BookCommandKind.ReadingJournal:
                Say(ReadingJournal.Summary(books.Journal(3), app.Clock.UtcNow, lang), lang);
                return true;
            case BookCommandKind.Continue:
                return await ContinueAsync(books, lang).ConfigureAwait(false);
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

        if (books.LastPlace() is not { Path: { } path })
        {
            return false;
        }

        SayIf(await books.OpenAsync(path, null, lang, CancellationToken.None).ConfigureAwait(false), lang);
        return true;
    }

    private async Task FindAsync(BookAloud books, string query, Lang lang)
    {
        IReadOnlyList<LibraryCard> found = books.Library.Search(query);
        if (found.Count == 0)
        {
            int count = books.Library.Count;
            Say(lang == Lang.Arabic
                ? $"ملقتش كتاب عن {query}. دورت في {count} كتاب لحد دلوقتي."
                : $"I didn't find a book on {query}. I've looked through {count} books so far.", lang);
            return;
        }

        if (found.Count == 1)
        {
            await OpenCardAsync(books, found[0], lang).ConfigureAwait(false);
            return;
        }

        _choices = found;
        _choicesAt = app.Clock.UtcNow;
        Say(BookListText.List(found, lang), lang);
    }

    private async Task PickAsync(BookAloud books, int number, Lang lang)
    {
        if (number < 1 || number > _choices.Count)
        {
            Say(lang == Lang.Arabic ? $"فيه {_choices.Count} بس. أنهي واحد؟" : $"There are only {_choices.Count}. Which one?", lang);
            return;
        }

        LibraryCard card = _choices[number - 1];
        _choices = [];
        await OpenCardAsync(books, card, lang).ConfigureAwait(false);
    }

    private async Task OpenCardAsync(BookAloud books, LibraryCard card, Lang lang)
    {
        Say(lang == Lang.Arabic ? $"بفتح {card.Title}." : $"Opening {card.Title}.", lang);
        SayIf(await books.OpenAsync(card.Path, null, lang, CancellationToken.None).ConfigureAwait(false), lang);
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
