using Mubsir.Core.Reminders;
using Xunit;

namespace Mubsir.Core.Tests.Reminders;

public class ReminderBookTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 18, 0, 0); // A Thursday.

    private static string File0() => Path.Combine(Path.GetTempPath(), "mubsir-rem-" + Guid.NewGuid().ToString("N") + ".json");

    [Theory]
    [InlineData(30, null, null, "2026-11-05T18:30:00")]
    [InlineData(null, "20:00", null, "2026-11-05T20:00:00")]
    [InlineData(null, "08:00", null, "2026-11-06T08:00:00")]
    [InlineData(null, "08:00", "tomorrow", "2026-11-06T08:00:00")]
    [InlineData(null, "09:00", "saturday", "2026-11-07T09:00:00")]
    [InlineData(null, "09:00", "thursday", "2026-11-12T09:00:00")]
    [InlineData(null, "09:00", "2026-12-25", "2026-12-25T09:00:00")]
    public void Times_are_worked_out(int? minutes, string? time, string? day, string expected) =>
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ReminderTime.Resolve(Now, minutes, time, day));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(0, null, null)]
    [InlineData(null, "not a time", null)]
    [InlineData(null, "09:00", "2020-01-01")]
    public void A_time_that_makes_no_sense_is_refused(int? minutes, string? time, string? day) =>
        Assert.Null(ReminderTime.Resolve(Now, minutes, time, day));

    [Fact]
    public void Reminders_survive_a_restart_and_are_said_once()
    {
        string file = File0();
        try
        {
            new ReminderBook(file).Add("الدوا", Now.AddMinutes(5));
            var again = new ReminderBook(file);
            Assert.Single(again.All);
            Assert.Empty(again.TakeDue(Now));
            Assert.Single(again.TakeDue(Now.AddMinutes(6)));
            Assert.Empty(again.TakeDue(Now.AddMinutes(7)));
            Assert.Empty(new ReminderBook(file).All);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_daily_reminder_moves_on_even_after_days_off()
    {
        string file = File0();
        try
        {
            var book = new ReminderBook(file);
            book.Add("الدوا", Now, ReminderRepeat.Daily);
            Assert.Single(book.TakeDue(Now.AddDays(3).AddHours(1)));
            Assert.Equal(Now.AddDays(4), Assert.Single(book.All).At);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void A_reminder_can_be_cancelled_and_a_damaged_file_doesnt_break_it()
    {
        string file = File0();
        try
        {
            var book = new ReminderBook(file);
            Reminder r = book.Add("a", Now.AddDays(1))!;
            Assert.True(book.Cancel(r.Id));
            Assert.False(book.Cancel(r.Id));
            File.WriteAllText(file, "{ not json");
            Assert.Empty(new ReminderBook(file).All);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
