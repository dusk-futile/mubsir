using System.Diagnostics;
using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Onboarding;
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
        bool keys = KeySetup.CanStart(HasKey(KeyKind.Anthropic), HasKey(KeyKind.FishAudio), HasKey(KeyKind.Gemini));
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

    public bool GetOption(string name)
    {
        MubsirSettings s = app.Settings.Settings;
        return name switch
        {
            "ShowNotch" => s.ShowNotch,
            "ShowHudhud" => s.ShowHudhud,
            "UseFishVoice" => s.UseFishVoice,
            "ReadBooksWithFish" => s.ReadBooksWithFish,
            "TeachMovesMouse" => s.TeachMovesMouse,
            "SaveSessions" => s.SaveSessions,
            _ => false,
        };
    }

    public void SetOption(string name, bool value) => app.Settings.Update(s => name switch
    {
        "ShowNotch" => s with { ShowNotch = value },
        "ShowHudhud" => s with { ShowHudhud = value },
        "UseFishVoice" => s with { UseFishVoice = value },
        "ReadBooksWithFish" => s with { ReadBooksWithFish = value },
        "TeachMovesMouse" => s with { TeachMovesMouse = value },
        "SaveSessions" => s with { SaveSessions = value },
        _ => s,
    });

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
