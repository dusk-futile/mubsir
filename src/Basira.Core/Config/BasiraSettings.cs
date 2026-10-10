using Basira.Core.Common;
using Basira.Core.Input;

namespace Basira.Core.Config;

/// <summary>App settings, changed through the spoken settings menu or the config file.</summary>
/// <remarks>
/// Treat a loaded instance as read-only and make changes with <c>with</c>. The setters exist for
/// source-generated JSON: with <c>init</c> setters it would reset every missing field to zero
/// instead of keeping the defaults below.
/// </remarks>
public sealed record BasiraSettings
{
    public const string DefaultBrainModel = "claude-haiku-5-5";
    public const string DefaultEarsModel = "gemini-3.5-flash-lite";
    public const string DefaultNvdaLangTagStyle = "VoiceElement";
    public const int DefaultSpeechRate = 50;
    public const decimal DefaultBudgetSoftCapUsd = 8m;
    public const int DefaultAgentMaxSteps = 15;
    public const int DefaultAgentMaxSeconds = 180;

    public string BrainModel { get; set; } = DefaultBrainModel;

    /// <summary>Stronger model for hard requests; null means never escalate.</summary>
    public string? EscalationModel { get; set; }

    /// <summary>Speech-to-text model.</summary>
    public string EarsModel { get; set; } = DefaultEarsModel;

    /// <summary>0 (slowest) to 100 (fastest).</summary>
    public int SpeechRate { get; set; } = DefaultSpeechRate;

    public Verbosity Verbosity { get; set; } = Verbosity.Brief;

    public KeyIdentity TalkKey { get; set; } = KeyIdentity.RightCtrl;

    public decimal BudgetSoftCapUsd { get; set; } = DefaultBudgetSoftCapUsd;

    /// <summary>Blocks all cloud calls; local reading keeps working.</summary>
    public bool PrivateMode { get; set; }

    public bool BasiraPaused { get; set; }

    /// <summary>Bank, payment, wallet and government sites that switch on bank mode (domain suffixes; editable).</summary>
    public List<string> BankDomains { get; set; } = BankDomainDefaults.Create();

    public bool UseNvda { get; set; } = true;

    /// <summary>How language changes are tagged in SSML sent to NVDA; settled by the M0 NVDA spike.</summary>
    public string NvdaLangTagStyle { get; set; } = DefaultNvdaLangTagStyle;

    /// <summary>Debug content logging is on until this time (expires after 24 h); null means off.</summary>
    public DateTimeOffset? ContentLoggingUntil { get; set; }

    /// <summary>Charity shared-PC mode: no memory between people.</summary>
    public bool SharedPcMode { get; set; }

    public int AgentMaxSteps { get; set; } = DefaultAgentMaxSteps;

    public int AgentMaxSeconds { get; set; } = DefaultAgentMaxSeconds;

    /// <summary>Whether debug content logging is on at <paramref name="now"/>.</summary>
    public bool IsContentLoggingOn(DateTimeOffset now) => ContentLoggingUntil is { } until && now < until;
}
