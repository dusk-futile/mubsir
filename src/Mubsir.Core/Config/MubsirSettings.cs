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

    /// <summary>English books through the paid voice. They read well for free, so it's off.</summary>
    public bool ReadBooksWithFish { get; set; }

    /// <summary>Arabic books through the paid voice, about $0.50 for 10 pages. The free Arabic voices are much weaker, so it's on.</summary>
    public bool ReadArabicBooksWithFish { get; set; } = true;

    /// <summary>Send the opening of each book to the AI once, to describe it for searching. The person chooses this.</summary>
    public bool DescribeBooks { get; set; }

    /// <summary>Read Arabic diacritics fully instead of leaving them to the voice.</summary>
    public bool ReadFullTashkeel { get; set; } = true;

    /// <summary>The Fish Audio voice id for English. Empty uses the first voice in the catalog.</summary>
    public string? FishVoiceId { get; set; }

    /// <summary>The Fish Audio voice id for Arabic. Empty uses the first voice in the catalog.</summary>
    public string? FishVoiceIdArabic { get; set; }

    /// <summary>Keep conversations as files the user can open. Private mode saves nothing.</summary>
    public bool SaveSessions { get; set; } = true;

    /// <summary>Where chats go. Empty means Documents\\Mubsir\\Chats.</summary>
    public string? SessionsFolder { get; set; }
}
