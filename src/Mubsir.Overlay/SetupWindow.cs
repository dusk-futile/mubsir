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

    /// <summary>The brain the person chose, or null if they haven't chosen.</summary>
    BrainChoice? GetBrain();

    void SetBrain(BrainChoice brain);

    /// <summary>The hearing the person chose, or null if they haven't chosen.</summary>
    EarsChoice? GetEars();

    void SetEars(EarsChoice ears);

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

    // The notch's colours: near-black ground, cream words, amber for what matters.
    private static readonly Brush Ground = Palette.Solid(Color.FromRgb(0x0B, 0x0C, 0x14));
    private static readonly Brush Card = Palette.Solid(Color.FromRgb(0x16, 0x18, 0x24));
    private static readonly Brush Ink = Palette.Solid(Color.FromRgb(0xFF, 0xF4, 0xDE));
    private static readonly Brush Quiet = Palette.Solid(Color.FromRgb(0xFF, 0xF4, 0xDE), 0.62);
    private static readonly Brush Accent = Palette.Solid(Color.FromRgb(0xF3, 0xC3, 0x43));
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
    private string? _focusChoice;

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
        Foreground = Ink;

        // Windows 11's own dark controls, so every box, list and button matches and screen readers know them.
#pragma warning disable WPF0001
        ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
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
        Add(Words(T("اختار الخدمة لكل جزء، وحط مفتاحها بس. Gemini مجاني.", "Choose a service for each part, then add only its key. Gemini is free."), 14, Quiet));
        Button? first = ShowServices();

        bool ready = Providers.CanStart(_driver.GetBrain(), _driver.GetEars(), Held());
        Button next = MainButton(T("التالي", "Continue"), ShowPrivacy);
        next.IsEnabled = ready;
        Add(next);
        if (_focusChoice is null)
        {
            (ready ? next : first ?? next).Focus();
        }

        if (speak)
        {
            _driver.Say(T(
                "أهلا بك في مبصر. اختار العقل والسمع والصوت. Gemini مجاني ومفتاح واحد منه يكفي للعقل والسمع. انسخ المفتاح من الموقع، وأنا هاخده وأجربه وأقولك النتيجة.",
                "Welcome to Mubsir. Choose the brain, the hearing and the voice. Gemini is free, and one Gemini key covers the brain and the hearing. Copy a key on the website and I'll pick it up, test it and tell you the result."), _lang);
        }
    }

    private KeysHeld Held() => new(_driver.HasKey(KeyKind.Anthropic), _driver.HasKey(KeyKind.FishAudio), _driver.HasKey(KeyKind.Gemini));

    /// <summary>
    /// The three choices, each followed by the one key it needs. A key already asked for above isn't asked for
    /// twice. Returns the first key button, to focus when nothing is set up yet.
    /// </summary>
    private Button? ShowServices()
    {
        KeysHeld held = Held();
        var shown = new HashSet<KeyKind>();
        Button? first = null;

        void Need(KeyKind kind, string name, string about)
        {
            if (!shown.Add(kind))
            {
                Add(Words(T("بيستخدم نفس المفتاح اللي فوق.", "Uses the same key as above."), 13, Quiet));
                return;
            }

            Button button = KeyRow(kind, name, about);
            first ??= button;
        }

        BrainChoice brain = Providers.Chosen(_driver.GetBrain(), held);
        Section(T("العقل", "Brain"));
        Choice("brain", (int)brain,
            [T("Google Gemini · مجاني", "Google Gemini · free"), T($"Claude Haiku · حوالي {CostGuide.Estimate(60).Brain:0} دولار في الشهر", $"Claude Haiku · about ${CostGuide.Estimate(60).Brain:0} a month")],
            i => _driver.SetBrain((BrainChoice)i));
        Need(Providers.KeyFor(brain), brain == BrainChoice.Claude ? "Claude" : "Google Gemini",
            brain == BrainChoice.Claude
                ? T("بتدفع اللي بتستخدمه.", "You pay for what you use.")
                : T("جوجل ممكن تستخدم الطلبات المجانية لتحسين خدماتها.", "Google may use free requests to improve its products."));

        EarsChoice ears = Providers.Chosen(_driver.GetEars(), held);
        Section(T("السمع", "Hearing"));
        Choice("ears", (int)ears, [T("Google Gemini · مجاني", "Google Gemini · free"), "Fish Audio"], i => _driver.SetEars((EarsChoice)i));
        Need(Providers.KeyFor(ears), ears == EarsChoice.Fish ? "Fish Audio" : "Google Gemini", string.Empty);

        bool fishVoice = _driver.GetOption("UseFishVoice");
        Section(T("صوت العربي", "Arabic voice"));
        Choice("voice", fishVoice ? 1 : 0, [T("صوت ويندوز · مجاني", "Windows voice · free"), T("Fish Audio · صوت طبيعي", "Fish Audio · natural")],
            i => _driver.SetOption("UseFishVoice", i == 1));
        if (fishVoice)
        {
            Need(KeyKind.FishAudio, "Fish Audio", string.Empty);
        }

        Add(Words(T("صوت الإنجليزي: Ryan، مجاني وشغال على جهازك.", "English voice: Ryan, free, on this PC."), 13, Quiet));
        return first;
    }

    private void Section(string title) => Add(Words(title, 15, Accent, FontWeights.SemiBold, top: 18));

    /// <summary>One choice as radio buttons, so a screen reader says "1 of 2". Choosing redraws the page.</summary>
    private void Choice(string group, int chosen, string[] options, Action<int> choose)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            var radio = new RadioButton
            {
                Content = options[i],
                GroupName = group,
                IsChecked = i == chosen,
                FontFamily = Font,
                FontSize = 14,
                Margin = new Thickness(0, 4, 18, 4),
            };
            AutomationProperties.SetName(radio, options[i]);
            radio.Checked += (_, _) =>
            {
                if (index == chosen)
                {
                    return;
                }

                choose(index);
                _focusChoice = group + index;
                Refresh();
            };
            if (_focusChoice == group + index)
            {
                _focusChoice = null;
                Dispatcher.BeginInvoke(() => radio.Focus(), DispatcherPriority.Input);
            }

            row.Children.Add(radio);
        }

        Add(row);
    }

    private Button KeyRow(KeyKind kind, string name, string about, StackPanel? into = null)
    {
        StackPanel target = into ?? _body;
        var row = new StackPanel();
        bool has = _driver.HasKey(kind);
        row.Children.Add(Words(name + T(" · المفتاح", " key") + (has ? T(" ✓ جاهز", " ✓ ready") : string.Empty), 14, has ? Accent : Ink, FontWeights.SemiBold));
        if (about.Length > 0)
        {
            row.Children.Add(Words(about, 13, Quiet));
        }

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
        target.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 6, 0, 6), Child = row });
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

        ShowServices();

        // Talk key
        Section(T("مفتاح الكلام", "Talk key"));
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

        // Fish voice id & testing
        Section(T("صوت Fish Audio", "Fish Audio voice"));
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
        Section(T("إضافة NVDA", "NVDA add-on"));
        Add(Words(T("لتشغيل جسر NVDA مع مبصر، اضغط هنا لتثبيت الإضافة في NVDA.", "To connect NVDA with Mubsir, click here to install the bridge add-on in NVDA."), 12.5, Quiet));
        Add(SmallButton(T("تثبيت إضافة Mubsir Bridge لـ NVDA", "Install Mubsir Bridge NVDA add-on"), _driver.InstallNvdaAddon));

        // Options
        Section(T("خيارات", "Options"));
        Option("ShowNotch", T("أظهر الشريط اللي فوق الشاشة (بعد إعادة التشغيل)", "Show the notch at the top of the screen (after a restart)"));
        Option("OneVoicePerAnswer", T("صوت واحد للرد كله لو فيه عربي (الجمل الإنجليزي كمان بصوت Fish، بتكلفة)", "One voice for a whole answer that has Arabic (English sentences in the Fish voice too, at a cost)"));
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

    private static TextBlock Words(string text, double size, Brush brush, FontWeight? weight = null, double top = 3) => new()
    {
        Text = text,
        FontFamily = Font,
        FontSize = size,
        Foreground = brush,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, top, 0, 3),
    };

    private static Button MainButton(string text, Action click)
    {
        var button = new Button
        {
            Content = text,
            MinHeight = 42,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
            FontFamily = Font,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Background = Accent,
            Foreground = Palette.Solid(Colors.Black),
        };
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
