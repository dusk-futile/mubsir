using Mubsir.Core.Motion;

namespace Mubsir.Overlay;

/// <summary>One line of the Doing checklist.</summary>
public sealed record CompanionStep(string Text, StepState State);

/// <summary>Whether the Doing checklist is running, finished, being undone, or stopped.</summary>
public enum DoingMode
{
    Running,
    Done,
    Undoing,
    Stopped,
}

/// <summary>
/// What the notch shows in a state. The app fills only what that state uses; anything left empty keeps
/// the state's own wording. Nothing here is logged or stored.
/// </summary>
public sealed record CompanionContent
{
    /// <summary>Listening: the words heard so far.</summary>
    public string? Transcript { get; init; }

    /// <summary>Speaking: the whole sentence.</summary>
    public string? Caption { get; init; }

    /// <summary>Speaking: how many of the caption's words have been spoken; the rest stay dim. Null lights them all.</summary>
    public int? CaptionWordsSpoken { get; init; }

    /// <summary>Speaking: a second line in Arabic, shown right-to-left.</summary>
    public string? CaptionArabic { get; init; }

    /// <summary>Doing: the steps, in order.</summary>
    public IReadOnlyList<CompanionStep>? Steps { get; init; }

    public DoingMode Mode { get; init; } = DoingMode.Running;

    /// <summary>Reading: the book's title.</summary>
    public string? BookTitle { get; init; }

    /// <summary>Reading: chapter and page.</summary>
    public string? BookPlace { get; init; }

    /// <summary>Reading: what the user can say next.</summary>
    public string? BookHint { get; init; }

    /// <summary>Reading: 0–1 through the book.</summary>
    public double BookProgress { get; init; }

    /// <summary>Confirm: the amount, as it should be read.</summary>
    public string? Amount { get; init; }

    /// <summary>Confirm: the site the payment goes to.</summary>
    public string? Site { get; init; }

    /// <summary>Confirm: one line with what is being paid and how.</summary>
    public string? Facts { get; init; }

    /// <summary>Confirm: how many of the four Ctrl presses have happened.</summary>
    public int Taps { get; init; }

    /// <summary>Paid: the sentence to show.</summary>
    public string? PaidText { get; init; }
}
