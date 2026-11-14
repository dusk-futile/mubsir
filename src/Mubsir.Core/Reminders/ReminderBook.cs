using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mubsir.Core.Reminders;

public sealed record Reminder(int Id, string Text, DateTime At, string Repeat = ReminderRepeat.None);

public static class ReminderRepeat
{
    public const string None = "none";
    public const string Daily = "daily";
    public const string Weekly = "weekly";
}

[JsonSerializable(typeof(List<Reminder>))]
internal sealed partial class ReminderJson : JsonSerializerContext;

/// <summary>Works out when a reminder is due from what the AI understood. Times are the PC's local time.</summary>
public static class ReminderTime
{
    private const int MaxMinutes = 366 * 24 * 60;

    public static DateTime? Resolve(DateTime now, int? inMinutes, string? time, string? day)
    {
        if (inMinutes is { } minutes)
        {
            return minutes is > 0 and <= MaxMinutes ? now.AddMinutes(minutes) : null;
        }

        if (!TimeOnly.TryParse(time, CultureInfo.InvariantCulture, out TimeOnly clock))
        {
            return null;
        }

        string d = (day ?? "today").Trim().ToLowerInvariant();
        DateOnly today = DateOnly.FromDateTime(now);
        if (d == "tomorrow")
        {
            return today.AddDays(1).ToDateTime(clock);
        }

        if (Enum.TryParse(d, ignoreCase: true, out DayOfWeek weekday) && !int.TryParse(d, out _))
        {
            int ahead = ((int)weekday - (int)now.DayOfWeek + 7) % 7;
            DateTime sameWeek = today.AddDays(ahead).ToDateTime(clock);
            return sameWeek > now ? sameWeek : sameWeek.AddDays(7);
        }

        if (d == "today")
        {
            DateTime at = today.ToDateTime(clock);
            return at > now ? at : at.AddDays(1);
        }

        return DateOnly.TryParse(d, CultureInfo.InvariantCulture, out DateOnly date) && date.ToDateTime(clock) > now ? date.ToDateTime(clock) : null;
    }
}

/// <summary>Reminders kept in a file on the PC, so they survive a restart.</summary>
public sealed class ReminderBook(string file)
{
    public const int MaxReminders = 200;
    private readonly object _gate = new();
    private List<Reminder>? _items;

    public IReadOnlyList<Reminder> All
    {
        get
        {
            lock (_gate)
            {
                return Items().OrderBy(r => r.At).ToArray();
            }
        }
    }

    public Reminder? Add(string text, DateTime at, string repeat = ReminderRepeat.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            List<Reminder> items = Items();
            if (items.Count >= MaxReminders || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var reminder = new Reminder(items.Count == 0 ? 1 : items.Max(r => r.Id) + 1, text.Trim(), at, repeat is ReminderRepeat.Daily or ReminderRepeat.Weekly ? repeat : ReminderRepeat.None);
            items.Add(reminder);
            Save();
            return reminder;
        }
    }

    public bool Cancel(int id)
    {
        lock (_gate)
        {
            bool removed = Items().RemoveAll(r => r.Id == id) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    /// <summary>What's due now, each said once. A repeating one moves on to its next time, even if the PC was off for a while.</summary>
    public IReadOnlyList<Reminder> TakeDue(DateTime now)
    {
        lock (_gate)
        {
            List<Reminder> items = Items();
            Reminder[] due = items.Where(r => r.At <= now).ToArray();
            if (due.Length == 0)
            {
                return [];
            }

            foreach (Reminder r in due)
            {
                items.Remove(r);
                if (r.Repeat != ReminderRepeat.None)
                {
                    int step = r.Repeat == ReminderRepeat.Daily ? 1 : 7;
                    DateTime next = r.At;
                    while (next <= now)
                    {
                        next = next.AddDays(step);
                    }

                    items.Add(r with { At = next });
                }
            }

            Save();
            return due;
        }
    }

    private List<Reminder> Items()
    {
        if (_items is not null)
        {
            return _items;
        }

        try
        {
            _items = File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), ReminderJson.Default.ListReminder) ?? [] : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            _items = [];
        }

        return _items;
    }

    private void Save()
    {
        try
        {
            string? folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_items ?? [], ReminderJson.Default.ListReminder));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveFailed = true;
        }
    }

    /// <summary>True if the last save couldn't be written, so the person can be told.</summary>
    public bool SaveFailed { get; private set; }
}
