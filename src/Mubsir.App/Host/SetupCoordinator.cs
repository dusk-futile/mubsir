using System.Diagnostics;
using System.Globalization;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Input;
using Mubsir.Core.Onboarding;
using Mubsir.Core.Sessions;
using Mubsir.Core.Setup;
using Mubsir.Core.Speech;
using Mubsir.Overlay;

namespace Mubsir.App.Host;

/// <summary>Connects the setup window to the keys, the voice and the settings.</summary>
internal sealed class SetupCoordinator(AppServices app, SecretStore secrets, UserPrompter prompter) : ISetupDriver, IDisposable
{
    private readonly object _gate = new();
    private Thread? _window;

    private static string Secret(KeyKind kind) => kind switch
    {
        KeyKind.Anthropic => SecretNames.Anthropic,
        KeyKind.FishAudio => SecretNames.Fish,
        _ => SecretNames.Gemini,
    };

    public void OpenIfNeeded()
    {
        MubsirSettings s = app.Settings.Settings;
        bool keys = Providers.CanStart(s.Brain, s.Ears, secrets.Held());
        if (!keys || !s.OnboardingDone)
        {
            Open(settingsOnly: false);
        }
    }

    public void Open() => Open(settingsOnly: app.Settings.Settings.OnboardingDone);

    public bool HasKey(KeyKind kind) => secrets.Has(Secret(kind));

    public void OpenKeyPage(KeyKind kind)
    {
        string page = kind switch
        {
            KeyKind.Anthropic => KeySetup.AnthropicKeysPage,
            KeyKind.FishAudio => KeySetup.FishKeysPage,
            _ => KeySetup.GeminiKeysPage,
        };
        Launch(page);
    }

    public async Task<(bool Saved, string Message)> SaveKeyAsync(KeyKind kind, string key)
    {
        Lang lang = app.UiLang;
        string name = kind switch
        {
            KeyKind.Anthropic => "Claude",
            KeyKind.FishAudio => "Fish Audio",
            _ => "Gemini",
        };
        KeyTest test = await KeyTester.TestAsync(kind, key, CancellationToken.None).ConfigureAwait(false);
        bool arabic = lang == Lang.Arabic;
        switch (test)
        {
            case KeyTest.Wrong:
                return (false, arabic ? $"مفتاح {name} ده مش شغال. اتأكد إنك نسخته كله." : $"That {name} key doesn't work. Check you copied all of it.");
            case KeyTest.NoCredit:
                secrets.Set(Secret(kind), key);
                return (true, arabic ? $"حفظت مفتاح {name}، بس مفيش رصيد. لازم تشحن الحساب الأول." : $"I saved the {name} key, but the account has no credit. It needs topping up first.");
            case KeyTest.CouldNotCheck:
                secrets.Set(Secret(kind), key);
                return (true, arabic ? $"حفظت مفتاح {name}، بس مقدرتش أجربه دلوقتي. اتأكد إنك متصل بالنت." : $"I saved the {name} key, but couldn't test it just now. Check you're online.");
            default:
                secrets.Set(Secret(kind), key);
                return (true, arabic ? $"مفتاح {name} شغال ومحفوظ." : $"Your {name} key works and is saved.");
        }
    }

    /// <summary>The answer to the question on screen, when it comes by voice. Buttons cancel it.</summary>
    public Task<string?> ListenAsync(CancellationToken ct) => prompter.AskAsync(string.Empty, ct);

    public void GiveConsent()
    {
        DateTimeOffset now = app.Clock.UtcNow;
        app.Settings.UpdateProfile(p => p with { CloudConsentGiven = true, CloudConsentAt = now });
    }

    public OnboardingFacts Facts() => new(app.Nvda.IsRunning());

    public void Say(string text, Lang lang) => app.Speech.Speak(text, UtteranceKind.System, lang, interrupt: true);

    public void Finished(PersonalPlan plan)
    {
        app.Settings.Update(s =>
        {
            MubsirSettings next = s with { };
            plan.ApplyTo(next);
            return next;
        });
        app.Settings.UpdateProfile(plan.ApplyTo);
    }

    public void OpenChats()
    {
        string folder = app.Sessions?.Folder ?? AppPaths.DefaultChatsDir;
        Directory.CreateDirectory(folder);
        Launch(folder);
    }

    public BrainChoice? GetBrain() => app.Settings.Settings.Brain;

    public void SetBrain(BrainChoice brain) => app.Settings.Update(s => s with { Brain = brain });

    public EarsChoice? GetEars() => app.Settings.Settings.Ears;

    public void SetEars(EarsChoice ears) => app.Settings.Update(s => s with { Ears = ears });

    public bool GetOption(string name)
    {
        MubsirSettings s = app.Settings.Settings;
        return name switch
        {
            "ShowNotch" => s.ShowNotch,
            "UseFishVoice" => s.UseFishVoice,
            "OneVoicePerAnswer" => s.OneVoicePerAnswer,
            "ReadArabicBooksWithFish" => s.ReadArabicBooksWithFish,
            "ReadFullTashkeel" => s.ReadFullTashkeel,
            "SaveSessions" => s.SaveSessions,
            _ => false,
        };
    }

    public void SetOption(string name, bool value) => app.Settings.Update(s => name switch
    {
        "ShowNotch" => s with { ShowNotch = value },
        "UseFishVoice" => s with { UseFishVoice = value },
        "OneVoicePerAnswer" => s with { OneVoicePerAnswer = value },
        "ReadArabicBooksWithFish" => s with { ReadArabicBooksWithFish = value },
        "ReadFullTashkeel" => s with { ReadFullTashkeel = value },
        "SaveSessions" => s with { SaveSessions = value },
        _ => s,
    });

    public void InstallNvdaAddon()
    {
        Lang lang = app.UiLang;
        bool isArabic = lang == Lang.Arabic;

        bool nvdaInstalled = app.Nvda.IsRunning();
        if (!nvdaInstalled)
        {
            foreach (string? root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            })
            {
                if (!string.IsNullOrEmpty(root) && File.Exists(Path.Combine(root, "NVDA", "nvda.exe")))
                {
                    nvdaInstalled = true;
                    break;
                }
            }
        }

        if (!nvdaInstalled)
        {
            string notInstalledMsg = isArabic
                ? "برنامج NVDA مش متثبت على الجهاز ده."
                : "NVDA is not installed on this PC.";
            Say(notInstalledMsg, lang);
            return;
        }

        string baseDir = AppContext.BaseDirectory;
        string? addon = Directory.GetFiles(baseDir, "*.nvda-addon").FirstOrDefault();
        if (addon is null)
        {
            string missingMsg = isArabic
                ? "ملف إضافة NVDA مش موجود في مجلد التطبيق."
                : "The NVDA add-on file wasn't found in the app folder.";
            Say(missingMsg, lang);
            return;
        }

        Launch(addon);
        string startedMsg = isArabic
            ? "فتحت مثبت إضافة NVDA. اضغط نعم للموافقة على التثبيت."
            : "Opened the NVDA add-on installer. Press Yes to confirm installation.";
        Say(startedMsg, lang);
    }

    public KeyIdentity GetTalkKey() => app.Settings.Settings.TalkKey;

    public void SetTalkKey(KeyIdentity key)
    {
        app.Settings.Update(s => s with { TalkKey = key });
        Lang lang = app.UiLang;
        string name = key switch
        {
            _ when key == KeyIdentity.Apps => (lang == Lang.Arabic ? "زر التطبيقات" : "Applications key"),
            _ when key == KeyIdentity.ScrollLock => (lang == Lang.Arabic ? "قفل التمرير (Scroll Lock)" : "Scroll Lock"),
            _ => (lang == Lang.Arabic ? "Control اليمين" : "Right Ctrl"),
        };
        Say((lang == Lang.Arabic ? "مفتاح الكلام: " : "Talk key: ") + name, lang);
    }

    public string? GetFishVoiceId() => app.Settings.Settings.FishVoiceIdArabic;

    public void SetFishVoiceId(string? voiceId)
    {
        app.Settings.Update(s => s with { FishVoiceIdArabic = string.IsNullOrWhiteSpace(voiceId) ? null : voiceId.Trim() });
    }

    public void TestVoice(Lang testLang)
    {
        string sentence = testLang == Lang.Arabic
            ? "أهلاً بك، هذا صوت مُبصِر للتجربة."
            : "Hello, this is a test of Mubsir's voice.";
        Say(sentence, testLang);
    }

    public void ReportProblem() => Launch("https://github.com/dusk-futile/mubsir/issues/new");

    public void ExportTodayChat()
    {
        Lang lang = app.UiLang;
        bool isArabic = lang == Lang.Arabic;

        string folder = app.Sessions?.Folder ?? AppPaths.DefaultChatsDir;
        DateTimeOffset now = app.Clock.UtcNow;
        string mdPath = Path.Combine(folder, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");
        string docxPath = Path.Combine(folder, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".docx");

        if (!File.Exists(mdPath))
        {
            string emptyMsg = isArabic
                ? "مفيش محادثات محفوظة النهاردة لتصديرها."
                : "There are no saved chats today to export.";
            Say(emptyMsg, lang);
            return;
        }

        try
        {
            bool ok = DocxWriter.TryExportMarkdownFile(mdPath, docxPath);
            if (ok)
            {
                string okMsg = isArabic
                    ? "تم تصدير محادثة اليوم بصيغة Word بجوار ملف المحادثة."
                    : "Exported today's chat as a Word document next to the chat file.";
                Say(okMsg, lang);
            }
            else
            {
                string failMsg = isArabic
                    ? "تعذر تصدير محادثة اليوم."
                    : "Could not export today's chat.";
                Say(failMsg, lang);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            app.Log.Failure("export_chat_docx", e);
            string failMsg = isArabic ? "حدث خطأ أثناء تصدير المحادثة." : "An error occurred while exporting the chat.";
            Say(failMsg, lang);
        }
    }

    public void Dispose()
    {
    }

    private void Open(bool settingsOnly)
    {
        lock (_gate)
        {
            if (_window is { IsAlive: true })
            {
                return;
            }

            _window = SetupHost.Show(this, app.UiLang, settingsOnly, e => app.Log.Failure("setup_window", e));
        }
    }

    private void Launch(string target)
    {
        try
        {
            using Process? _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            app.Log.Failure("setup_open", e);
        }
    }
}
