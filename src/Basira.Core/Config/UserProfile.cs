using Basira.Core.Common;

namespace Basira.Core.Config;

/// <summary>What the spoken setup interview learns about the user. Stored encrypted on the PC.</summary>
public sealed record UserProfile
{
    public string? Name { get; init; }

    public Lang PreferredLanguage { get; init; } = Lang.Arabic;

    /// <summary>Free label such as "Egyptian" or "Libyan"; informs speech-to-text hints and dialect replies.</summary>
    public string? Dialect { get; init; }

    public ReplyStyle ReplyStyle { get; init; } = ReplyStyle.SimpleMsa;

    public string? Studies { get; init; }

    public string? Work { get; init; }

    public List<string> Apps { get; init; } = [];

    public string? Needs { get; init; }

    /// <summary>The user's top everyday tasks; they double as acceptance tests.</summary>
    public List<string> TopTasks { get; init; } = [];

    /// <summary>Spoken consent to send requests to cloud AI providers.</summary>
    public bool CloudConsentGiven { get; init; }

    public DateTimeOffset? CloudConsentAt { get; init; }
}
