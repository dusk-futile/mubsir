using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Text;

namespace Mubsir.Core.Onboarding;

/// <summary>What Mubsir already knows before asking, so it never asks what it can find out.</summary>
public sealed record OnboardingFacts(bool ScreenReaderRunning, bool MicrophoneWorks = true);

public sealed record OnboardingOption(string Id, string Arabic, string English, params string[] Words);

/// <summary>One question. <see cref="Free"/> means any words are a good answer and none are needed.</summary>
public sealed record OnboardingStep(string Id, string Arabic, string English, IReadOnlyList<OnboardingOption> Options, bool Free = false)
{
    public string Text(Lang lang)
    {
        string question = lang == Lang.Arabic ? Arabic : English;
        if (Options.Count == 0)
        {
            return question;
        }

        return question + " " + string.Join(lang == Lang.Arabic ? "، أو " : ", or ", Options.Select(o => lang == Lang.Arabic ? o.Arabic : o.English)) + (lang == Lang.Arabic ? "؟" : "?");
    }
}

/// <summary>What the answers add up to. Each field changes how the app behaves.</summary>
public sealed record PersonalPlan(
    UserKind Kind,
    Lang Language,
    Verbosity Verbosity,
    int SpeechRate,
    bool UseNvda,
    string? Goal,
    string? Subject,
    string? Name,
    string FirstSuggestionArabic,
    string FirstSuggestionEnglish,
    bool DescribeBooks = false,
    Mubsir.Core.Chat.ChatSite ChatSite = Mubsir.Core.Chat.ChatSite.None)
{
    public string FirstSuggestion => Language == Lang.Arabic ? FirstSuggestionArabic : FirstSuggestionEnglish;

    public void ApplyTo(MubsirSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.UserKind = Kind;
        settings.Verbosity = Verbosity;
        settings.SpeechRate = SpeechRate;
        settings.UseNvda = UseNvda;
        settings.DescribeBooks = DescribeBooks;
        settings.ChatSite = ChatSite;
        settings.OnboardingDone = true;
    }

    public UserProfile ApplyTo(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var tasks = new List<string>();
        if (!string.IsNullOrWhiteSpace(Goal))
        {
            tasks.Add(Goal);
        }

        return profile with
        {
            Name = string.IsNullOrWhiteSpace(Name) ? profile.Name : Name,
            PreferredLanguage = Language,
            TopTasks = tasks.Count > 0 ? tasks : profile.TopTasks,
            Needs = string.IsNullOrWhiteSpace(Subject) ? profile.Needs : Subject,
        };
    }
}

/// <summary>
/// The first conversation. Each answer picks the next question and changes the plan, a question is skipped when
/// the answer is already known, and an answer it can't place is asked once more and then given a safe default,
/// so nobody can get stuck. The same flow runs by voice and in the window.
/// </summary>
public sealed class OnboardingFlow
{
    private const int MaxTriesPerStep = 2;

    private static readonly OnboardingStep AskLanguage = new("language", "تحب أكلمك عربي ولا إنجليزي؟", "Arabic or English?", [
        new("arabic", "عربي", "Arabic", "عربي", "عربى", "العربيه", "arabic", "arabi", "3arabi"),
        new("english", "إنجليزي", "English", "انجليزي", "انجليزى", "انكليزي", "english", "inglizi"),
    ]);

    private static readonly OnboardingStep AskSight = new("sight", "بتستخدم الكمبيوتر إزاي؟", "How do you use your computer?", [
        new("blind", "بسمعه ومش بشوف الشاشة", "by listening, I can't see the screen", "كفيف", "كفيفه", "مش بشوف", "مابشوفش", "مش شايف", "اعمى", "بسمع", "بالسمع", "blind", "listening", "cant see", "can not see", "screen reader", "nvda"),
        new("lowvision", "بشوف بصعوبة", "I see it with difficulty", "ضعيف", "ضعف", "بصعوبه", "مكبر", "صغير", "low vision", "difficulty", "partly", "barely", "magnif", "large text"),
    ]);

    private static readonly OnboardingStep AskBlindGoal = new("blind_goal", "أكتر حاجة تحب تعملها إيه؟", "What do you want to do most?", [
        new("books", "أقرأ كتب وملفات", "read books and files", "كتب", "كتاب", "اقرا", "ملفات", "مذاكره", "ادرس", "books", "book", "read", "study", "pdf"),
        new("messages", "رسايل وواتساب", "messages and WhatsApp", "رسايل", "رساله", "واتس", "ايميل", "بريد", "messages", "whatsapp", "email", "mail", "chat"),
        new("web", "أدور وأتصفح النت", "search and browse the web", "نت", "انترنت", "ادور", "بحث", "جوجل", "يوتيوب", "web", "browse", "search", "internet", "youtube", "google"),
        new("work", "شغل على الكمبيوتر", "work on the computer", "شغل", "وورد", "اكسل", "work", "word", "excel", "office", "job"),
    ]);

    private static readonly OnboardingStep AskBlindSpeed = new("speed", "سرعة الكلام تحبها إزاي؟", "How fast should I talk?", [
        new("normal", "عادية", "normal speed", "عادي", "عاديه", "normal", "regular"),
        new("fast", "سريعة", "fast", "سريع", "سريعه", "fast", "quick"),
        new("slow", "هادية", "slower", "هادي", "هاديه", "بطيء", "براحه", "slow"),
    ]);

    private static readonly OnboardingStep AskStudyPlace = new("subject_books", "بتقرأ إيه أكتر؟ قول الموضوع، أو قول خلاص.", "What do you read most? Say the subject, or say skip.", [], Free: true);

    private static readonly OnboardingStep AskDescribeBooks = new("describe_books", "أوصف كتبك عشان ألاقيها بالموضوع؟ هبعت أول صفحة من كل كتاب للذكاء الاصطناعي مرة واحدة، و500 كتاب بحوالي 20 سنت.", "Should I describe your books so you can find them by subject? I'd send the first page of each book to the AI once, about 20 cents for 500 books.", [
        new("yes", "أيوه", "yes", "ايوه", "اه", "نعم", "طبعا", "موافق", "yes", "okay", "sure"),
        new("no", "لأ", "no", "لا", "لأ", "مش عايز", "no", "skip"),
    ]);

    private static readonly OnboardingStep AskMessagesApp = new("subject_messages", "بتستخدم إيه أكتر: واتساب، إيميل، ولا حاجة تانية؟", "Which do you use most: WhatsApp, email, or something else?", [], Free: true);

    private static readonly OnboardingStep AskWorkApps = new("subject_work", "بتشتغل على أي برامج؟ قولهم، أو قول خلاص.", "Which programs do you work in? Name them, or say skip.", [], Free: true);

    private static readonly OnboardingStep AskChat = new("chat_site", "لو عندك حساب في شات جي بي تي أو جيميني على المتصفح، أسأله هو للأسئلة العادية عشان ماتدفعش. تستخدم إيه؟", "If you're signed in to ChatGPT or Gemini in your browser, I can ask it plain questions so you don't pay. Which do you use?", [
        new("chatgpt", "شات جي بي تي", "ChatGPT", "شات", "جي بي تي", "chatgpt", "chat gpt", "gpt"),
        new("gemini", "جيميني", "Gemini", "جيميني", "جمناي", "gemini"),
        new("none", "ولا حاجة", "neither", "ولا", "لا", "مفيش", "neither", "none", "no", "skip", "خلاص"),
    ]);

    private static readonly OnboardingStep AskName = new("name", "أناديك بإيه؟ أو قول خلاص.", "What should I call you? Or say skip.", [], Free: true);

    private static readonly string[] SkipWords = ["خلاص", "لا", "مش دلوقتي", "مفيش", "skip", "no", "nothing", "not now", "none"];

    private readonly OnboardingFacts _facts;
    private readonly Dictionary<string, string> _answers = new(StringComparer.Ordinal);
    private int _tries;
    private Lang _language;
    private string? _name;
    private string? _subject;

    public OnboardingFlow(OnboardingFacts facts, Lang startLanguage = Lang.Arabic)
    {
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _language = startLanguage;
        Current = AskLanguage;
    }

    /// <summary>The question to ask now, or null when the flow is finished.</summary>
    public OnboardingStep? Current { get; private set; }

    public Lang Language => _language;

    public bool LastAnswerUnderstood { get; private set; } = true;

    /// <summary>A step's options in order, for buttons. Picking one is the same as saying it.</summary>
    public void Choose(string optionId)
    {
        ArgumentNullException.ThrowIfNull(optionId);
        if (Current is not null)
        {
            Record(Current, optionId);
        }
    }

    /// <summary>Takes what the user said. Null or empty means they said nothing; that counts as a try.</summary>
    public void Answer(string? said)
    {
        if (Current is not { } step)
        {
            return;
        }

        if (step.Free)
        {
            string text = (said ?? string.Empty).Trim();
            bool skipped = text.Length == 0 || IsSkip(text);
            LastAnswerUnderstood = true;
            Record(step, skipped ? string.Empty : text);
            return;
        }

        string? id = Match(step, said);
        if (id is not null)
        {
            LastAnswerUnderstood = true;
            Record(step, id);
            return;
        }

        LastAnswerUnderstood = false;
        if (++_tries >= MaxTriesPerStep)
        {
            Record(step, DefaultFor(step));
        }
    }

    public PersonalPlan Plan()
    {
        string goal = Get("blind_goal") ?? "web";
        UserKind kind = Get("sight") == "lowvision" ? UserKind.LowVision : UserKind.Blind;
        bool noSight = kind == UserKind.Blind;
        Verbosity verbosity = noSight ? Verbosity.Brief : Verbosity.Normal;
        int rate = Get("speed") switch
        {
            "fast" => 70,
            "slow" => 35,
            _ => MubsirSettings.DefaultSpeechRate,
        };
        (string ar, string en) = FirstSuggestion(goal, _subject);
        ar += " وتقدر تقول غير الصوت عشان تختار صوت العربي.";
        en += " You can also say change the voice to pick the Arabic voice.";

        return new PersonalPlan(
            kind,
            _language,
            verbosity,
            rate,
            UseNvda: noSight || _facts.ScreenReaderRunning,
            goal,
            _subject,
            _name,
            ar,
            en,
            Get("describe_books") == "yes",
            Get("chat_site") switch
            {
                "chatgpt" => Mubsir.Core.Chat.ChatSite.ChatGpt,
                "gemini" => Mubsir.Core.Chat.ChatSite.Gemini,
                _ => Mubsir.Core.Chat.ChatSite.None,
            });
    }

    private static (string Arabic, string English) FirstSuggestion(string goal, string? subject) => goal switch
    {
        "books" => (subject is null ? "جرب تقول: دور لي على كتاب." : $"جرب تقول: دور لي على كتاب عن {subject}.", subject is null ? "Try saying: find me a book." : $"Try saying: find me a book on {subject}."),
        "messages" => ("جرب تقول: مين بعتلي رسالة؟", "Try saying: who messaged me?"),
        "work" => ("جرب تقول: اقرأ لي الشاشة.", "Try saying: read my screen."),
        _ => ("جرب تقول: افتح يوتيوب.", "Try saying: open YouTube."),
    };

    private static string DefaultFor(OnboardingStep step) => step.Id switch
    {
        "language" => "arabic",
        "sight" => "blind",
        "blind_goal" => "web",
        "describe_books" => "no",
        "chat_site" => "none",
        "speed" => "normal",
        _ => step.Options[0].Id,
    };

    private static string? Match(OnboardingStep step, string? said)
    {
        if (string.IsNullOrWhiteSpace(said))
        {
            return null;
        }

        string folded = " " + ArabicFolding.Fold(said) + " ";
        OnboardingOption? best = null;
        int bestLength = 0;
        foreach (OnboardingOption option in step.Options)
        {
            foreach (string word in option.Words)
            {
                string key = ArabicFolding.Fold(word);
                if (key.Length > bestLength && folded.Contains(key.Length <= 2 ? " " + key + " " : key, StringComparison.Ordinal))
                {
                    best = option;
                    bestLength = key.Length;
                }
            }
        }

        return best?.Id;
    }

    private static bool IsSkip(string text)
    {
        string folded = ArabicFolding.Fold(text).Trim();
        return SkipWords.Any(s => ArabicFolding.Fold(s) == folded);
    }

    private string? Get(string id) => _answers.TryGetValue(id, out string? value) ? value : null;

    private void Record(OnboardingStep step, string answer)
    {
        _answers[step.Id] = answer;
        _tries = 0;
        switch (step.Id)
        {
            case "language":
                _language = answer == "english" ? Lang.English : Lang.Arabic;
                break;
            case "name":
                _name = answer.Length == 0 ? null : answer;
                break;
            case "subject_books" or "subject_messages" or "subject_work":
                _subject = answer.Length == 0 ? null : answer;
                break;
        }

        Current = Next(step.Id, answer);
    }

    /// <summary>The whole algorithm: where each answer leads.</summary>
    private OnboardingStep? Next(string id, string answer) => id switch
    {
        // A running screen reader answers the sight question for us.
        "language" => _facts.ScreenReaderRunning ? AfterBlind() : AskSight,
        "sight" => answer == "blind" ? AfterBlind() : AskBlindGoal,
        "blind_goal" => answer switch
        {
            "books" => AskStudyPlace,
            "messages" => AskMessagesApp,
            "work" => AskWorkApps,
            _ => Get("sight") == "lowvision" ? AskChat : AskBlindSpeed,
        },
        "subject_books" => AskDescribeBooks,
        "describe_books" or "subject_messages" or "subject_work" => Get("sight") == "lowvision" ? AskChat : AskBlindSpeed,
        "speed" => AskChat,
        "chat_site" => AskName,
        _ => null,
    };

    private OnboardingStep AfterBlind()
    {
        _answers["sight"] = "blind";
        return AskBlindGoal;
    }
}
