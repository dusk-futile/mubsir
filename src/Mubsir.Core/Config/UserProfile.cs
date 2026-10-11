using Mubsir.Core.Common;

namespace Mubsir.Core.Config;

public sealed record UserProfile
{
    public string? Name { get; set; }

    public Lang PreferredLanguage { get; set; } = Lang.Arabic;

    public string? Dialect { get; set; }

    public ReplyStyle ReplyStyle { get; set; } = ReplyStyle.SimpleMsa;

    public string? Studies { get; set; }

    public string? Work { get; set; }

    public List<string> Apps { get; set; } = [];

    public string? Needs { get; set; }

    public List<string> TopTasks { get; set; } = [];

    public bool CloudConsentGiven { get; set; }

    public DateTimeOffset? CloudConsentAt { get; set; }
}
