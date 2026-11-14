using Mubsir.Core.Common;
using Mubsir.Core.Input;
using Mubsir.Core.Setup;

namespace Mubsir.Core.Config;

public sealed record MubsirSettings
{
    public const string DefaultBrainModel = "claude-haiku-5-5";
    public const string DefaultEarsModel = "gemini-3.5-flash-lite";
    public const string DefaultGeminiBrainModel = "gemini-3.5-flash-lite";
    public const string BackupGeminiBrainModel = "gemini-3.5-flash";
    public const string DefaultNvdaLangTagStyle = "VoiceElement";
    public const int DefaultSpeechRate = 50;
    public const decimal DefaultBudgetSoftCapUsd = 20m;
    public const int DefaultAgentMaxSteps = 15;
    public const int DefaultAgentMaxSeconds = 180;

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

    /// <summary>The notch shows anyone watching the screen what's going on. The app works the same without it.</summary>
    public bool ShowNotch { get; set; } = true;

    /// <summary>Speak Arabic through Fish Audio when it has a key. Otherwise, or if it fails, the free Windows voice speaks. English always uses the free voice.</summary>
    public bool UseFishVoice { get; set; } = true;

    /// <summary>The brain the person chose. Null until they choose: then a Claude key means Claude, otherwise Gemini.</summary>
    public BrainChoice? Brain { get; set; }

    /// <summary>The address of another AI service to use as the brain, such as https://openrouter.ai/api/v1. Used when <see cref="Brain"/> is Custom.</summary>
    public string? CustomBrainUrl { get; set; }

    /// <summary>The model to ask for at <see cref="CustomBrainUrl"/>.</summary>
    public string? CustomBrainModel { get; set; }

    /// <summary>What the person calls the assistant. Null means Mubsir.</summary>
    public string? AssistantName { get; set; }

    /// <summary>A weak PC: no Ryan voice download, so English uses the Windows voice. Null until the PC has been checked.</summary>
    public bool? LightMode { get; set; }

    /// <summary>The hearing the person chose. Null until they choose: Gemini when its key is there.</summary>
    public EarsChoice? Ears { get; set; }

    /// <summary>When an answer has any Arabic, the Fish voice says all of it, English sentences too, so the speaker never changes mid-answer. Costs Fish for those English sentences, so it's off unless chosen.</summary>
    public bool OneVoicePerAnswer { get; set; }

    /// <summary>Arabic books through the paid voice, about $0.50 for 10 pages. The free Arabic voices are much weaker, so it's on.</summary>
    public bool ReadArabicBooksWithFish { get; set; } = true;

    /// <summary>The Gemini model used as the brain when there's no Claude key.</summary>
    public string GeminiBrainModel { get; set; } = DefaultGeminiBrainModel;

    /// <summary>The AI chat they're signed in to. Plain questions go there, so they don't use the API.</summary>
    public Mubsir.Core.Chat.ChatSite ChatSite { get; set; }

    /// <summary>Send the opening of each book to the AI once, to describe it for searching. The person chooses this.</summary>
    public bool DescribeBooks { get; set; }

    /// <summary>Read Arabic diacritics fully instead of leaving them to the voice.</summary>
    public bool ReadFullTashkeel { get; set; } = true;

    /// <summary>The Fish Audio voice id for Arabic. Empty uses the first voice in the catalog.</summary>
    public string? FishVoiceIdArabic { get; set; }

    /// <summary>Keep conversations as files the user can open. Private mode saves nothing.</summary>
    public bool SaveSessions { get; set; } = true;

    /// <summary>Save chats encrypted, so only this Windows account can read them. They no longer open in Notepad;
    /// Mubsir can still look through them and export a day as a Word file.</summary>
    public bool EncryptSessions { get; set; }

    /// <summary>Where chats go. Empty means Documents\\Mubsir\\Chats.</summary>
    public string? SessionsFolder { get; set; }
}
