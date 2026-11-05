using Mubsir.Core.Common;

namespace Mubsir.Core.Reading;

/// <summary>What the reader was reading, in words, for the start-up greeting and "what was I reading?".</summary>
public static class ReadingJournal
{
    public static string Line(ReadingPosition place, string title, DateTimeOffset now, Lang lang)
    {
        string when = When(place.UpdatedAt, now, lang);
        return lang == Lang.Arabic
            ? $"{when} وقفت عند صفحة {place.Page}{(place.Pages > 0 ? $" من {place.Pages}" : string.Empty)} في {title}."
            : $"{when} you stopped on page {place.Page}{(place.Pages > 0 ? $" of {place.Pages}" : string.Empty)} of {title}.";
    }

    public static string Greeting(ReadingPosition place, string title, DateTimeOffset now, Lang lang) =>
        Line(place, title, now, lang) + (lang == Lang.Arabic ? " قول كمل عشان نكمل." : " Say continue to carry on.");

    public static string Summary(IReadOnlyList<(ReadingPosition Place, string Title)> books, DateTimeOffset now, Lang lang)
    {
        if (books.Count == 0)
        {
            return lang == Lang.Arabic ? "لسه ماقريناش كتب مع بعض." : "We haven't read any books together yet.";
        }

        return string.Join(' ', books.Select(b => Line(b.Place, b.Title, now, lang)));
    }

    private static string When(DateTimeOffset then, DateTimeOffset now, Lang lang)
    {
        int days = (now.ToLocalTime().Date - then.ToLocalTime().Date).Days;
        return (days, lang == Lang.Arabic) switch
        {
            (<= 0, true) => "النهاردة",
            (<= 0, false) => "Today",
            (1, true) => "إمبارح",
            (1, false) => "Yesterday",
            (_, true) => $"من {days} يوم",
            (_, false) => $"{days} days ago",
        };
    }
}
