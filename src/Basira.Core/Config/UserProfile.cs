using Basira.Core.Common;

namespace Basira.Core.Config;

/// <summary>What the spoken setup interview learns about the user. Stored encrypted on the PC.</summary>
/// <remarks>Plain setters so source-generated JSON keeps the defaults of missing fields (see <see cref="BasiraSettings"/>).</remarks>
public sealed record UserProfile
{
    public string? Name { get; set; }

    public Lang PreferredLanguage { get; set; } = Lang.Arabic;

    /// <summary>Free label such as "Egyptian" or "Libyan"; informs speech-to-text hints and dialect replies.</summary>
    public string? Dialect { get; set; }

    public ReplyStyle ReplyStyle { get; set; } = ReplyStyle.SimpleMsa;

    public string? Studies { get; set; }

    public string? Work { get; set; }

    public List<string> Apps { get; set; } = [];

    public string? Needs { get; set; }

    /// <summary>The user's top everyday tasks; they double as acceptance tests.</summary>
    public List<string> TopTasks { get; set; } = [];

    /// <summary>Spoken consent to send requests to cloud AI providers.</summary>
    public bool CloudConsentGiven { get; set; }

    public DateTimeOffset? CloudConsentAt { get; set; }
}
