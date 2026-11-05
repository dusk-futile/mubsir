using Mubsir.Core.Common;
using Mubsir.Core.Input;

namespace Mubsir.Core.Config;

public sealed record MubsirSettings
{
    public const string DefaultBrainModel = "claude-haiku-5-5";
    public const string DefaultEarsModel = "gemini-3.5-flash-lite";
    public const string DefaultNvdaLangTagStyle = "VoiceElement";
    public const int DefaultSpeechRate = 50;
    public const decimal DefaultBudgetSoftCapUsd = 20m;
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

    /// <summary>Say the last book and page at start-up. Turn off on a shared PC.</summary>
    public bool GreetWithLastBook { get; set; } = true;

    public bool MubsirPaused { get; set; }

    public List<string> BankDomains { get; set; } = BankDomainDefaults.Create();

    public bool UseNvda { get; set; } = true;

    public string NvdaLangTagStyle { get; set; } = DefaultNvdaLangTagStyle;


    public bool SharedPcMode { get; set; }

    public int AgentMaxSteps { get; set; } = DefaultAgentMaxSteps;

    public int AgentMaxSeconds { get; set; } = DefaultAgentMaxSeconds;

    public UserKind UserKind { get; set; } = UserKind.Unknown;

    /// <summary>The first conversation has finished. Setup opens until it has.</summary>
    public bool OnboardingDone { get; set; }

    /// <summary>Everything is taught, not done, until the user says otherwise. A child can't leave it by voice.</summary>
    public bool StartInTeachMode { get; set; }

    /// <summary>In teaching, the real mouse moves to the place too. It never clicks.</summary>
    public bool TeachMovesMouse { get; set; } = true;

    /// <summary>The notch shows anyone watching the screen what's going on. The app works the same without it.</summary>
    public bool ShowNotch { get; set; } = true;

    public bool ShowHudhud { get; set; } = true;

    /// <summary>Speak through Fish Audio when it has a key. Otherwise, or if it fails, the free Windows voice speaks.</summary>
    public bool UseFishVoice { get; set; } = true;

    /// <summary>Whole books through the paid voice. A 300-page Arabic book costs about $18, so it's off.</summary>
    public bool ReadBooksWithFish { get; set; }

    /// <summary>A Fish Audio voice id. Empty uses Fish's default.</summary>
    public string? FishVoiceId { get; set; }

    /// <summary>Keep conversations as files the user can open. Private mode saves nothing.</summary>
    public bool SaveSessions { get; set; } = true;

    /// <summary>Where chats go. Empty means Documents\\Mubsir\\Chats.</summary>
    public string? SessionsFolder { get; set; }
}
