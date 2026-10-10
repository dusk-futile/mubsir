using Basira.Core.Common;
using Basira.Core.Input;

namespace Basira.Core.Config;

/// <summary>App settings, changed through the spoken settings menu or the config file.</summary>
public sealed record BasiraSettings
{
    public const string DefaultBrainModel = "claude-haiku-5-5";
    public const string DefaultEarsModel = "gemini-3.5-flash-lite";
    public const string DefaultNvdaLangTagStyle = "VoiceElement";
    public const int DefaultSpeechRate = 50;
    public const decimal DefaultBudgetSoftCapUsd = 8m;
    public const int DefaultAgentMaxSteps = 15;
    public const int DefaultAgentMaxSeconds = 180;

    public string BrainModel { get; init; } = DefaultBrainModel;

    /// <summary>Stronger model for hard requests; null means never escalate.</summary>
    public string? EscalationModel { get; init; }

    /// <summary>Speech-to-text model.</summary>
    public string EarsModel { get; init; } = DefaultEarsModel;

    /// <summary>0 (slowest) to 100 (fastest).</summary>
    public int SpeechRate { get; init; } = DefaultSpeechRate;

    public Verbosity Verbosity { get; init; } = Verbosity.Brief;

    public KeyIdentity TalkKey { get; init; } = KeyIdentity.RightCtrl;

    public decimal BudgetSoftCapUsd { get; init; } = DefaultBudgetSoftCapUsd;

    /// <summary>Blocks all cloud calls; local reading keeps working.</summary>
    public bool PrivateMode { get; init; }

    public bool BasiraPaused { get; init; }

    /// <summary>Bank, payment, wallet and government sites that switch on bank mode (domain suffixes; editable).</summary>
    public List<string> BankDomains { get; init; } = BankDomainDefaults.Create();

    public bool UseNvda { get; init; } = true;

    /// <summary>How language changes are tagged in SSML sent to NVDA; settled by the M0 NVDA spike.</summary>
    public string NvdaLangTagStyle { get; init; } = DefaultNvdaLangTagStyle;

    /// <summary>Debug content logging is on until this time (expires after 24 h); null means off.</summary>
    public DateTimeOffset? ContentLoggingUntil { get; init; }

    /// <summary>Charity shared-PC mode: no memory between people.</summary>
    public bool SharedPcMode { get; init; }

    public int AgentMaxSteps { get; init; } = DefaultAgentMaxSteps;

    public int AgentMaxSeconds { get; init; } = DefaultAgentMaxSeconds;

    /// <summary>Whether debug content logging is on at <paramref name="now"/>.</summary>
    public bool IsContentLoggingOn(DateTimeOffset now) => ContentLoggingUntil is { } until && now < until;
}
