using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mubsir.Core.Common;
using Mubsir.Core.Input;
using Mubsir.Core.Onboarding;
using Mubsir.Core.Setup;

namespace Mubsir.Overlay;

/// <summary>What the setup window needs from the app. The window itself holds no keys and makes no calls.</summary>
public interface ISetupDriver
{
    bool HasKey(KeyKind kind);

    void OpenKeyPage(KeyKind kind);

    /// <summary>Tests and stores a key. Returns what to tell the user.</summary>
    Task<(bool Saved, string Message)> SaveKeyAsync(KeyKind kind, string key);

    OnboardingFacts Facts();

    void Say(string text, Lang lang);

    void Finished(PersonalPlan plan);

    /// <summary>The answer to the question on screen, if it comes by voice first. Null if none came.</summary>
    Task<string?> ListenAsync(CancellationToken ct);

    void GiveConsent();

    void OpenChats();

    void SetOption(string name, bool value);

    bool GetOption(string name);

    void InstallNvdaAddon();

    KeyIdentity GetTalkKey();

    void SetTalkKey(KeyIdentity key);

    string GetBrainModel();

    void SetBrainModel(string model);

    string? GetFishVoiceId();

    void SetFishVoiceId(string? voiceId);

    void TestVoice(Lang lang);

    void ReportProblem();

    void ExportTodayChat();
}

/// <summary>
/// The app's one window: keys, then a short conversation, then how to start. Later it's the settings page. It's
/// built from standard controls so NVDA reads it, every step is also spoken, and all of it works by keyboard.
/// </summary>
public sealed class SetupWindow : Window
{
    private ComboBox VoiceList(string? current, Action<string?> choose)
    {
        var list = new ComboBox { FontFamily = Font, FontSize = 13, Margin = new Thickness(0, 4, 0, 6) };
        AutomationProperties.SetName(list, T("صوت العربية", "Arabic voice"));
        foreach (var voice in Mubsir.Core.Speech.VoiceCatalog.Arabic)
        {
            list.Items.Add(voice.Number + ": " + voice.Name);
            if (voice.Id == current)
            {
                list.SelectedIndex = voice.Number - 1;
            }
        }

        if (list.SelectedIndex < 0)
        {
            list.SelectedIndex = 0;
        }

        list.SelectionChanged += (_, _) => choose(Mubsir.Core.Speech.VoiceCatalog.Get(list.SelectedIndex + 1)?.Id);
        return list;
    }

    private static readonly Brush Ground = Palette.Solid(Color.FromRgb(0xF7, 0xF6, 0xF3));
    private static readonly Brush Ink = Palette.Solid(Color.FromRgb(0x1B, 0x1C, 0x20));
    private static readonly Brush Quiet = Palette.Solid(Color.FromRgb(0x5E, 0x60, 0x68));
    private static readonly Brush Line = Palette.Solid(Color.FromRgb(0xDD, 0xDB, 0xD5));
    private static readonly Brush Accent = Palette.Solid(Color.FromRgb(0x2B, 0x4C, 0x7E));
    private static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI, Tahoma");

    private readonly ISetupDriver _driver;
    private readonly StackPanel _body = new() { Margin = new Thickness(28, 24, 28, 24) };
    private readonly bool _settingsOnly;
    private readonly DispatcherTimer _clipboard = new() { Interval = TimeSpan.FromSeconds(1) };
    private OnboardingFlow? _flow;
    private Lang _lang;
    private string? _lastClipboard;
    private string? _notice;
    private CancellationTokenSource? _listening;

    /// <summary>Opens straight on the first question. Only the demo that takes the pictures uses this.</summary>
    public bool StartOnQuestions { get; init; }

    public SetupWindow(ISetupDriver driver, Lang lang, bool settingsOnly)
    {
        _driver = driver;
        _lang = lang;
        _settingsOnly = settingsOnly;
        Title = "Mubsir";
        Width = 520;
        Height = 660;
        MinWidth = 400;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Ground;
        Content = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
        _clipboard.Tick += (_, _) => WatchClipboard();
        Closed += (_, _) =>
        {
            _clipboard.Stop();
            _listening?.Cancel();
        };
        Loaded += (_, _) =>
        {
            _clipboard.Start();
            if (settingsOnly)
            {
                ShowSettings();
            }
            else if (StartOnQuestions)
            {
                StartQuestions();
            }
            else
            {
                ShowKeys(speak: true);
            }

            Activate();
        };
    }

    private bool Arabic => _lang == Lang.Arabic;

    private string T(string arabic, string english) => Arabic ? arabic : english;

    private void Begin(string heading)
    {
        _body.Children.Clear();
        FlowDirection = Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        Button other = SmallButton(Arabic ? "English" : "العربية", SwitchLanguage);
        DockPanel.SetDock(other, Dock.Right);
        top.Children.Add(other);
        top.Children.Add(Words(heading, 22, Ink, FontWeights.SemiBold));
        _body.Children.Add(top);
        if (_notice is not null)
        {
            TextBlock notice = Words(_notice, 14, Accent, FontWeights.SemiBold);
            AutomationProperties.SetLiveSetting(notice, AutomationLiveSetting.Polite);
            _body.Children.Add(notice);
            _notice = null;
        }
    }

    private void SwitchLanguage()
    {
        _lang = Arabic ? Lang.English : Lang.Arabic;
        if (_settingsOnly)
        {
            ShowSettings();
        }
        else if (_flow is not null)
        {
            ShowQuestion();
        }
        else
        {
            ShowKeys(speak: false);
        }
    }

    private void ShowKeys(bool speak)
    {
        Begin(T("أهلا بك في مبصر", "Welcome to Mubsir"));
        Add(Words(T("مبصر مجاني. محتاج مفتاح واحد مجاني من Google، ومفاتيحك بتفضل مشفرة على جهازك.",
            "Mubsir is free. It needs one free key from Google, and your keys stay encrypted on this PC."), 14, Ink));
        Add(Words(T("انسخ المفتاح من الموقع وهنا هيتحفظ لوحده.", "Copy a key on the website and it's picked up here by itself."), 14, Quiet));
        Button first = KeyRow(KeyKind.Gemini, T("Google Gemini: العقل والسمع", "Google Gemini: the brain and hearing"),
            T("مطلوب ومجاني. جوجل ممكن تستخدم الطلبات المجانية لتحسين خدماتها.", "Required and free. Google may use free requests to improve its products."));
        KeyRow(KeyKind.FishAudio, T("Fish Audio: صوت العربي", "Fish Audio: the Arabic voice"),
            T("مجاني دلوقتي، بصوت طبيعي. من غيره صوت ويندوز المجاني بيتكلم عربي.", "Free for now, in a natural voice. Without it, the free Windows voice speaks Arabic."));

        var extra = new Expander { Header = T("بدائل", "Other options"), Margin = new Thickness(0, 8, 0, 8), FontFamily = Font };
        var inner = new StackPanel();
        var save = _body;
        extra.Content = inner;
        Add(extra);
        KeyRow(KeyKind.Anthropic, T("Claude: عقل مدفوع", "Claude: a paid brain"),
            T("اختياري. لو حطيته هيتستخدم بدل Gemini، وبتدفع اللي بتستخدمه.", "Optional. If you add it, it's used instead of Gemini and you pay for what you use."), inner);

        bool ready = KeySetup.CanStart(_driver.HasKey(KeyKind.Anthropic), _driver.HasKey(KeyKind.FishAudio), _driver.HasKey(KeyKind.Gemini));
        Button next = MainButton(T("التالي", "Continue"), ShowPrivacy);
        next.IsEnabled = ready;
        Add(next);
        (ready ? next : first).Focus();
        if (speak)
        {
            _driver.Say(T(
                "أهلا بك في مبصر. محتاج مفتاح مجاني من Google Gemini. اضغط إدخال عشان أفتح لك صفحته. انشئ مفتاح وانسخه، وأنا هاخده وأجربه وأقولك النتيجة.",
                "Welcome to Mubsir. I need one free key from Google Gemini. Press Enter and I'll open its page. Create a key and copy it, and I'll pick it up, test it and tell you the result."), _lang);
        }
    }

    private void AddCosts()
    {
        MonthlyCost light = CostGuide.Estimate(20), normal = CostGuide.Estimate(60), heavy = CostGuide.Estimate(150);
        var box = new Border { BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(14), Margin = new Thickness(0, 8, 0, 8) };
        var lines = new StackPanel();
        lines.Children.Add(Words(T("التكلفة المتوقعة في الشهر (تقدير)", "What it might cost a month (an estimate)"), 13, Ink, FontWeights.SemiBold));
        lines.Children.Add(Words(T($"خفيف، 20 طلب في اليوم: حوالي {light.Total:0} دولار", $"Light, 20 requests a day: about ${light.Total:0}"), 13, Ink));
        lines.Children.Add(Words(T($"عادي، 60 طلب في اليوم: حوالي {normal.Total:0} دولار", $"Normal, 60 requests a day: about ${normal.Total:0}"), 13, Ink));
        lines.Children.Add(Words(T($"كتير، 150 طلب في اليوم: حوالي {heavy.Total:0} دولار", $"Heavy, 150 requests a day: about ${heavy.Total:0}"), 13, Ink));
        lines.Children.Add(Words(T($"بصوت ويندوز المجاني بدل Fish: حوالي {normal.WithFreeVoice:0} دولار للاستخدام العادي. الكتب بتتقرأ بالصوت المجاني إلا لو غيرت ده.",
            $"With the free Windows voice instead: about ${normal.WithFreeVoice:0} for normal use. Books use the free voice unless you change that."), 12.5, Quiet));
        box.Child = lines;
        Add(box);
    }

    private Button KeyRow(KeyKind kind, string name, string about, StackPanel? into = null)
    {
        StackPanel target = into ?? _body;
        var row = new StackPanel { Margin = new Thickness(0, 10, 0, 6) };
        bool has = _driver.HasKey(kind);
        row.Children.Add(Words(name + (has ? T(" (جاهز)", " (ready)") : string.Empty), 14, Ink, FontWeights.SemiBold));
        row.Children.Add(Words(about, 13, Quiet));
        var status = Words(string.Empty, 13, Accent);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var box = new PasswordBox { Height = 32, FontFamily = Font, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 6) };
        AutomationProperties.SetName(box, T("ألصق المفتاح هنا: ", "Paste the key here: ") + name);
        var buttons = new WrapPanel();
        Button open = SmallButton(T("هات المفتاح من الموقع", "Get a key from the website"), () => _driver.OpenKeyPage(kind));
        Button save = SmallButton(T("احفظ وجرب", "Save and test"), async () =>
        {
            if (box.Password.Length == 0)
            {
                return;
            }

            status.Text = T("بجرب المفتاح...", "Testing the key...");
            string key = box.Password;
            var (saved, message) = await _driver.SaveKeyAsync(kind, key);
            status.Text = message;
            _driver.Say(message, _lang);
            if (saved)
            {
                box.Clear();
                _notice = message;
                Refresh();
            }
        });
        buttons.Children.Add(open);
        buttons.Children.Add(save);
        row.Children.Add(box);
        row.Children.Add(buttons);
        row.Children.Add(status);
        target.Children.Add(row);
        return open;
    }

    /// <summary>Only runs while this window is open, so nothing else copied on this PC is ever looked at.</summary>
    private void WatchClipboard()
    {
        if (_flow is not null && !_settingsOnly)
        {
            return;
        }

        string? text;
        try
        {
            text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return;
        }

        if (text is null || text == _lastClipboard)
        {
            return;
        }

        _lastClipboard = text;
        if (KeySetup.Recognize(text) is { } kind)
        {
            _ = SaveFromClipboardAsync(kind, text);
        }
    }

    private async Task SaveFromClipboardAsync(KeyKind kind, string text)
    {
        // A Fish key is just 32 hex characters, so other copied text can look like one. Only fill an empty slot.
        if (kind == KeyKind.FishAudio && _driver.HasKey(kind))
        {
            return;
        }

        var (saved, message) = await _driver.SaveKeyAsync(kind, text);
        _driver.Say(message, _lang);
        if (!saved)
        {
            // Copying it again after fixing the problem should work.
            _lastClipboard = null;
        }
        else
        {
            try
            {
                Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            _notice = message;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (_settingsOnly)
        {
            ShowSettings();
        }
        else
        {
            ShowKeys(speak: false);
        }
    }

    private void ShowPrivacy()
    {
        Begin(T("قبل ما نبدأ", "Before we start"));
        string text = T(
            "لما تتكلم، صوتك بيروح لخدمة السمع، وطلبك مع نص الشاشة اللازم بيروح لـ Claude، وردودي بتروح لـ Fish Audio عشان تتحول لصوت. كلمات المرور والبطاقات والأكواد مش بتتبعت، وصفحات البنوك الذكاء الاصطناعي بيقف فيها. قول الوضع الخاص في أي وقت عشان توقف كل إرسال.",
            "When you talk, your voice goes to the hearing service, your request and the screen text it needs go to Google Gemini or Claude, and my Arabic replies go to Fish Audio to become a voice. Passwords, cards and codes aren't sent, and the AI stops on bank pages. Say private mode at any time to stop everything being sent.");
        Add(Words(text, 15, Ink));
        Button agree = MainButton(T("موافق", "I agree"), () =>
        {
            _driver.GiveConsent();
            StartQuestions();
        });
        Add(agree);
        Add(SmallButton(T("مش دلوقتي", "Not now"), Close));
        agree.Focus();
        _driver.Say(text + " " + T("اضغط إدخال للموافقة.", "Press Enter to agree."), _lang);
    }

    private void StartQuestions()
    {
        _flow = new OnboardingFlow(_driver.Facts(), _lang);
        ShowQuestion();
    }

    /// <summary>The same question can be answered by voice: hold Right Ctrl and say it, or use the buttons.</summary>
    private async Task ListenForAnswerAsync(CancellationToken ct)
    {
        try
        {
            string? said = null;

            // Silence isn't an answer: someone reading the screen isn't timed out of a question.
            while (said is null)
            {
                said = await _driver.ListenAsync(ct);
                if (ct.IsCancellationRequested || _flow is null)
                {
                    return;
                }
            }

            _flow?.Answer(said);
            ShowQuestion();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ShowQuestion()
    {
        _listening?.Cancel();
        _listening = null;
        if (_flow?.Current is not { } step)
        {
            Finish();
            return;
        }

        _lang = _flow.Language;
        if (!_flow.LastAnswerUnderstood)
        {
            _notice = T("مش متأكد فهمت. جرب تاني أو اختار من هنا.", "I didn't quite catch that. Try again, or pick one here.");
        }

        string? notice = _notice;
        Begin(T("نعرفك الأول", "Let's get to know you"));
        Add(Words(Arabic ? step.Arabic : step.English, 18, Ink));
        Control? first = null;
        foreach (OnboardingOption option in step.Options)
        {
            Button button = MainButton(Arabic ? option.Arabic : option.English, () =>
            {
                _flow?.Choose(option.Id);
                ShowQuestion();
            });
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            Add(button);
            first ??= button;
        }

        if (step.Free)
        {
            var box = new TextBox { Height = 34, FontSize = 14, FontFamily = Font, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };
            AutomationProperties.SetName(box, Arabic ? step.Arabic : step.English);
            void Send()
            {
                _flow?.Answer(box.Text);
                ShowQuestion();
            }

            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Send();
                }
            };
            Add(box);
            Add(MainButton(T("التالي", "Next"), Send));
            first = box;
        }

        first?.Focus();
        _driver.Say((notice is null ? string.Empty : notice + " ") + step.Text(_lang), _lang);
        _listening = new CancellationTokenSource();
        _ = ListenForAnswerAsync(_listening.Token);
    }

    private void Finish()
    {
        PersonalPlan plan = _flow!.Plan();
        _lang = plan.Language;
        _driver.Finished(plan);
        Begin(T("جاهز", "You're ready"));
        string how = T("اضغط مطولا على Control اليمين واتكلم، وسيبه لما تخلص. Control لوحده بيوقف الكلام.",
            "Hold Right Ctrl and talk, then let go. Pressing Ctrl stops me talking.");
        Add(Words(how, 16, Ink));
        Add(Words(plan.FirstSuggestion, 16, Accent));
        Button close = MainButton(T("ابدأ", "Start"), Close);
        Add(close);
        close.Focus();
        _driver.Say(how + " " + plan.FirstSuggestion, _lang);
    }

    private void ShowSettings()
    {
        Begin(T("الإعدادات", "Settings"));

        // Keys
        KeyRow(KeyKind.Anthropic, "Claude", T("العقل", "The brain"));
        KeyRow(KeyKind.FishAudio, "Fish Audio", T("صوت العربي", "The Arabic voice"));
        KeyRow(KeyKind.Gemini, "Google Gemini", T("اختياري، للسمع", "Optional, for hearing"));

        // Talk key
        Add(Words(T("مفتاح الكلام", "Talk key"), 14, Ink, FontWeights.SemiBold));
        Add(Words(T("اضغط مع الاستمرار على المفتاح ده عشان تتكلم، أو اضغط ضغطة واحدة عشان توقف الكلام.", "Hold this key to talk, or tap it once to stop speech."), 12.5, Quiet));
        var talkKeyBox = new ComboBox { FontFamily = Font, FontSize = 13, Height = 32, Margin = new Thickness(0, 4, 0, 10) };
        AutomationProperties.SetName(talkKeyBox, T("اختر مفتاح الكلام", "Choose talk key"));
        var keyOptions = new[]
        {
            (Key: KeyIdentity.RightCtrl, Label: T("Control اليمين (Right Ctrl)", "Right Ctrl (default)")),
            (Key: KeyIdentity.Apps, Label: T("زر التطبيقات (Apps key)", "Applications key (Apps)")),
            (Key: KeyIdentity.ScrollLock, Label: T("قفل التمرير (Scroll Lock)", "Scroll Lock")),
        };
        foreach (var opt in keyOptions)
        {
            talkKeyBox.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Key });
        }

        KeyIdentity currentTalk = _driver.GetTalkKey();
        int talkIdx = Array.FindIndex(keyOptions, o => o.Key == currentTalk);
        talkKeyBox.SelectedIndex = talkIdx >= 0 ? talkIdx : 0;
        talkKeyBox.SelectionChanged += (_, _) =>
        {
            if (talkKeyBox.SelectedItem is ComboBoxItem item && item.Tag is KeyIdentity chosen)
            {
                _driver.SetTalkKey(chosen);
            }
        };
        Add(talkKeyBox);

        // Brain choice
        Add(Words(T("عقل الذكاء الاصطناعي", "Brain model"), 14, Ink, FontWeights.SemiBold));
        Add(Words(T("Claude Haiku 5.5 هو الأسرع والأوفر للمهام اليومية. Claude Sonnet 5.5 للمهام المعقدة ولكن تكلفته 20 ضعف.",
            "Claude Haiku 5.5 is fastest and cheapest for daily tasks. Claude Sonnet 5.5 is for harder tasks but costs 20x more."), 12.5, Quiet));
        var brainBox = new ComboBox { FontFamily = Font, FontSize = 13, Height = 32, Margin = new Thickness(0, 4, 0, 10) };
        AutomationProperties.SetName(brainBox, T("اختر عقل الذكاء الاصطناعي", "Choose brain model"));
        var brainOptions = new[]
        {
            (Model: "claude-haiku-5-5", Label: T("Claude Haiku 5.5 (موصى به، سريع ورخيص)", "Claude Haiku 5.5 (recommended, fast and cheap)")),
            (Model: "claude-sonnet-5-5", Label: T("Claude Sonnet 5.5 (للمهام الأصعب، تكلفة أعلى 20 ضعف)", "Claude Sonnet 5.5 (for harder tasks - 20x higher cost)")),
        };
        foreach (var opt in brainOptions)
        {
            brainBox.Items.Add(new ComboBoxItem { Content = opt.Label, Tag = opt.Model });
        }

        string currentBrain = _driver.GetBrainModel();
        int brainIdx = Array.FindIndex(brainOptions, o => string.Equals(o.Model, currentBrain, StringComparison.OrdinalIgnoreCase));
        brainBox.SelectedIndex = brainIdx >= 0 ? brainIdx : 0;
        brainBox.SelectionChanged += (_, _) =>
        {
            if (brainBox.SelectedItem is ComboBoxItem item && item.Tag is string chosen)
            {
                _driver.SetBrainModel(chosen);
            }
        };
        Add(brainBox);

        // Fish voice id & testing
        Add(Words(T("صوت العربية (Fish Audio)", "Arabic voice (Fish Audio)"), 14, Ink, FontWeights.SemiBold));
        Add(Words(T("اختار من القايمة، أو اكتب معرف صوت من حسابك في Fish Audio.",
            "Pick from the list, or enter a voice ID from Fish Audio."), 12.5, Quiet));
        var voiceIdBox = new TextBox
        {
            Text = _driver.GetFishVoiceId() ?? string.Empty,
            Height = 32,
            FontFamily = Font,
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 6),
        };
        AutomationProperties.SetName(voiceIdBox, T("معرف صوت Fish Audio", "Fish Audio voice ID"));
        voiceIdBox.LostFocus += (_, _) => _driver.SetFishVoiceId(voiceIdBox.Text);
        voiceIdBox.TextChanged += (_, _) => _driver.SetFishVoiceId(voiceIdBox.Text);
        var voiceList = VoiceList(_driver.GetFishVoiceId(), id =>
        {
            _driver.SetFishVoiceId(id);
            voiceIdBox.Text = id ?? string.Empty;
        });
        Add(voiceList);
        Add(voiceIdBox);

        var voiceTestPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        voiceTestPanel.Children.Add(SmallButton(T("جرب الصوت بالعربية", "Test voice in Arabic"), () =>
        {
            _driver.SetFishVoiceId(voiceIdBox.Text);
            _driver.TestVoice(Lang.Arabic);
        }));
        voiceTestPanel.Children.Add(SmallButton(T("جرب الصوت بالإنجليزية", "Test voice in English"), () =>
        {
            _driver.SetFishVoiceId(voiceIdBox.Text);
            _driver.TestVoice(Lang.English);
        }));
        Add(voiceTestPanel);

        // NVDA Add-on installation
        Add(Words(T("إضافة NVDA Bridge", "NVDA Bridge Add-on"), 14, Ink, FontWeights.SemiBold));
        Add(Words(T("لتشغيل جسر NVDA مع مبصر، اضغط هنا لتثبيت الإضافة في NVDA.", "To connect NVDA with Mubsir, click here to install the bridge add-on in NVDA."), 12.5, Quiet));
        Add(SmallButton(T("تثبيت إضافة Mubsir Bridge لـ NVDA", "Install Mubsir Bridge NVDA add-on"), _driver.InstallNvdaAddon));

        // Options
        Add(Words(T("خيارات عامة", "General options"), 14, Ink, FontWeights.SemiBold));
        Option("ShowNotch", T("أظهر الشريط اللي فوق الشاشة (بعد إعادة التشغيل)", "Show the notch at the top of the screen (after a restart)"));
        Option("ShowHudhud", T("أظهر الهدهد (بعد إعادة التشغيل)", "Show Hudhud (after a restart)"));
        Option("UseFishVoice", T("اتكلم عربي بصوت Fish Audio", "Speak Arabic in the Fish Audio voice"));
        Option("ReadArabicBooksWithFish", T("اقرأ الكتب العربية بصوت Fish (10 صفحات بحوالي 50 سنت)", "Read Arabic books in the Fish voice (about $0.50 for 10 pages)"));
        Option("ReadFullTashkeel", T("اقرأ التشكيل كاملاً (لو مقفول، متروك للصوت)", "Read Arabic diacritics fully (when off, left to the voice)"));
        Option("SaveSessions", T("احفظ المحادثات في فولدر على جهازي", "Save chats in a folder on this PC"));

        // Chats and Export
        var chatsPanel = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
        chatsPanel.Children.Add(SmallButton(T("افتح فولدر المحادثات", "Open the chats folder"), _driver.OpenChats));
        chatsPanel.Children.Add(SmallButton(T("تصدير محادثة اليوم بصيغة Word (.docx)", "Export today's chat as Word (.docx)"), _driver.ExportTodayChat));
        Add(chatsPanel);

        // Report a problem
        var helpPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        helpPanel.Children.Add(SmallButton(T("أبلغ عن مشكلة (يفتح صفحة GitHub)", "Report a problem (opens GitHub)"), _driver.ReportProblem));
        Add(helpPanel);
    }

    private void Option(string name, string label)
    {
        var box = new CheckBox { Content = label, IsChecked = _driver.GetOption(name), Margin = new Thickness(0, 8, 0, 0), FontFamily = Font, FontSize = 14, Foreground = Ink };
        AutomationProperties.SetName(box, label);
        box.Click += (_, _) => _driver.SetOption(name, box.IsChecked == true);
        Add(box);
    }

    private void Add(UIElement element) => _body.Children.Add(element);

    private static TextBlock Words(string text, double size, Brush brush, FontWeight? weight = null) => new()
    {
        Text = text,
        FontFamily = Font,
        FontSize = size,
        Foreground = brush,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 3, 0, 3),
    };

    private static Button MainButton(string text, Action click)
    {
        var button = new Button { Content = text, MinHeight = 40, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(14, 6, 14, 6), FontFamily = Font, FontSize = 14 };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => click();
        return button;
    }

    private static Button SmallButton(string text, Action click)
    {
        var button = new Button { Content = text, MinHeight = 30, Margin = new Thickness(0, 2, 8, 2), Padding = new Thickness(10, 3, 10, 3), FontFamily = Font, FontSize = 13 };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => click();
        return button;
    }
}

/// <summary>Runs the setup window on its own thread and closes everything with it, so it costs nothing until it's opened.</summary>
public static class SetupHost
{
    public static Thread Show(ISetupDriver driver, Lang lang, bool settingsOnly, Action<Exception> failed, Action? closed = null, bool startOnQuestions = false)
    {
        var thread = new Thread(() =>
        {
            try
            {
                Dispatcher.CurrentDispatcher.UnhandledException += (_, e) =>
                {
                    e.Handled = true;
                    failed(e.Exception);
                };
                var window = new SetupWindow(driver, lang, settingsOnly) { StartOnQuestions = startOnQuestions };
                window.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                window.Show();
                Dispatcher.Run();
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                failed(e);
            }
            finally
            {
                closed?.Invoke();
            }
        })
        {
            IsBackground = true,
            Name = "Mubsir setup",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }
}
