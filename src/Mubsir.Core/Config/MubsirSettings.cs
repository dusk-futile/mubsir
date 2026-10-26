using Mubsir.Core.Common;
using Mubsir.Core.Input;

namespace Mubsir.Core.Config;

public sealed record MubsirSettings
{
    public const string DefaultBrainModel = "claude-haiku-5-5";
    public const string DefaultEarsModel = "gemini-3.5-flash-lite";
    public const string DefaultNvdaLangTagStyle = "VoiceElement";
    public const int DefaultSpeechRate = 50;
    public const decimal DefaultBudgetSoftCapUsd = 8m;
    public const int DefaultAgentMaxSteps = 15;
    public const int DefaultAgentMaxSeconds = 180;

    public string BrainModel { get; set; } = DefaultBrainModel;

    public string? EscalationModel { get; set; }

    public string EarsModel { get; set; } = DefaultEarsModel;

    public int SpeechRate { get; set; } = DefaultSpeechRate;

    public Verbosity Verbosity { get; set; } = Verbosity.Brief;

    public KeyIdentity TalkKey { get; set; } = KeyIdentity.RightCtrl;

    public decimal BudgetSoftCapUsd { get; set; } = DefaultBudgetSoftCapUsd;

    public bool PrivateMode { get; set; }

    public bool MubsirPaused { get; set; }

    public List<string> BankDomains { get; set; } = BankDomainDefaults.Create();

    public bool UseNvda { get; set; } = true;

    public string NvdaLangTagStyle { get; set; } = DefaultNvdaLangTagStyle;


    public bool SharedPcMode { get; set; }

    public int AgentMaxSteps { get; set; } = DefaultAgentMaxSteps;

    public int AgentMaxSeconds { get; set; } = DefaultAgentMaxSeconds;
}
