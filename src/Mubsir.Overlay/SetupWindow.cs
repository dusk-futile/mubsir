using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Mubsir.Core.Common;
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
}

/// <summary>
/// The app's one window: keys, then a short conversation, then how to start. Later it's the settings page. It's
/// built from standard controls so NVDA reads it, every step is also spoken, and all of it works by keyboard.
/// </summary>
public sealed class SetupWindow : Window
{
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
        Add(Words(T("مبصر مجاني. بيشتغل بمفاتيحك انت، فبتدفع بس اللي بتستخدمه، ومفاتيحك بتفضل مشفرة على جهازك.",
            "Mubsir is free. It runs on your own keys, so you only pay for what you use, and your keys stay encrypted on this PC."), 14, Ink));
        Add(Words(T("انسخ المفتاح من الموقع وهنا هيتحفظ لوحده.", "Copy a key on the website and it's picked up here by itself."), 14, Quiet));
        Button first = KeyRow(KeyKind.Anthropic, T("Claude: العقل", "Claude: the brain"),
            T("مطلوب. هنستخدم Haiku 5.5، الأسرع والأرخص والأكثر اعتمادية للمهام دي.", "Required. Mubsir uses Haiku 5.5: the fastest, cheapest and most reliable fit for this."));
        KeyRow(KeyKind.FishAudio, T("Fish Audio: الصوت والسمع", "Fish Audio: voice and hearing"),
            T("بيحول كلامك لنص وبيتكلم بصوت طبيعي. لو فشل، صوت ويندوز المجاني بيكمل.", "Turns your speech into text and speaks in a natural voice. If it fails, the free Windows voice takes over."));
        AddCosts();

        var extra = new Expander { Header = T("بدائل", "Other options"), Margin = new Thickness(0, 8, 0, 8), FontFamily = Font };
        var inner = new StackPanel();
        var save = _body;
        extra.Content = inner;
        Add(extra);
        KeyRow(KeyKind.Gemini, T("Google Gemini: للسمع بدل Fish", "Google Gemini: hearing instead of Fish"),
            T("اختياري. لو حطيته هيتستخدم للسمع، وFish احتياطي.", "Optional. If you add it, it's used for hearing with Fish as the backup."), inner);

        bool ready = KeySetup.CanStart(_driver.HasKey(KeyKind.Anthropic), _driver.HasKey(KeyKind.FishAudio), _driver.HasKey(KeyKind.Gemini));
        Button next = MainButton(T("التالي", "Continue"), ShowPrivacy);
        next.IsEnabled = ready;
        Add(next);
        (ready ? next : first).Focus();
        if (speak)
        {
            _driver.Say(T(
                "أهلا بك في مبصر. محتاج مفتاحين: مفتاح Claude ومفتاح Fish Audio. اضغط إدخال عشان أفتح لك صفحة مفتاح Claude. انشئ مفتاح وانسخه، وأنا هلقطه لوحدي وأجربه.",
                "Welcome to Mubsir. I need two keys: one from Claude and one from Fish Audio. Press Enter and I'll open the page for your Claude key. Create a key and copy it, and I'll pick it up and test it myself."), _lang);
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
            box.Clear();
            var (saved, message) = await _driver.SaveKeyAsync(kind, key);
            status.Text = message;
            _driver.Say(message, _lang);
            if (saved)
            {
                _ = Dispatcher.BeginInvoke(() => ShowKeys(speak: false));
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
        var (saved, message) = await _driver.SaveKeyAsync(kind, text);
        _driver.Say(message, _lang);
        if (saved)
        {
            try
            {
                Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            if (_settingsOnly)
            {
                ShowSettings();
            }
            else
            {
                ShowKeys(speak: false);
            }
        }
    }

    private void ShowPrivacy()
    {
        Begin(T("قبل ما نبدأ", "Before we start"));
        string text = T(
            "لما تتكلم، صوتك بيروح لخدمة السمع، وطلبك مع نص الشاشة اللازم بيروح لـ Claude، وردودي بتروح لـ Fish Audio عشان تتحول لصوت. كلمات المرور والبطاقات والأكواد مش بتتبعت، وصفحات البنوك الذكاء الاصطناعي بيقف فيها. قول الوضع الخاص في أي وقت عشان توقف كل إرسال.",
            "When you talk, your voice goes to the hearing service, your request and the screen text it needs go to Claude, and my replies go to Fish Audio to become a voice. Passwords, cards and codes aren't sent, and the AI stops on bank pages. Say private mode at any time to stop everything being sent.");
        Add(Words(text, 15, Ink));
        Button agree = MainButton(T("موافق", "I agree"), () =>
        {
            _driver.GiveConsent();
            StartQuestions();
        });
        Add(agree);
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
        _driver.Say(step.Text(_lang), _lang);
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
        KeyRow(KeyKind.Anthropic, "Claude", T("العقل", "The brain"));
        KeyRow(KeyKind.FishAudio, "Fish Audio", T("الصوت والسمع", "Voice and hearing"));
        KeyRow(KeyKind.Gemini, "Google Gemini", T("اختياري، للسمع", "Optional, for hearing"));
        Option("ShowNotch", T("أظهر الشريط اللي فوق الشاشة (بعد إعادة التشغيل)", "Show the notch at the top of the screen (after a restart)"));
        Option("ShowHudhud", T("أظهر الهدهد (بعد إعادة التشغيل)", "Show Hudhud (after a restart)"));
        Option("UseFishVoice", T("اتكلم بصوت Fish Audio", "Talk in the Fish Audio voice"));
        Option("ReadBooksWithFish", T("اقرأ الكتب بصوت Fish Audio (الكتاب الكبير ممكن يتكلف 18 دولار)", "Read books in the Fish Audio voice (a long book can cost about $18)"));
        Option("TeachMovesMouse", T("وأنا بعلم، حرك الماوس للمكان الصح", "When teaching, move the mouse to the right place"));
        Option("SaveSessions", T("احفظ المحادثات في فولدر على جهازي", "Save chats in a folder on this PC"));
        Add(SmallButton(T("افتح فولدر المحادثات", "Open the chats folder"), _driver.OpenChats));
    }

    private void Option(string name, string label)
    {
        var box = new CheckBox { Content = label, IsChecked = _driver.GetOption(name), Margin = new Thickness(0, 8, 0, 0), FontFamily = Font, FontSize = 14, Foreground = Ink };
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
        button.Click += (_, _) => click();
        return button;
    }

    private static Button SmallButton(string text, Action click)
    {
        var button = new Button { Content = text, MinHeight = 30, Margin = new Thickness(0, 2, 8, 2), Padding = new Thickness(10, 3, 10, 3), FontFamily = Font, FontSize = 13 };
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
