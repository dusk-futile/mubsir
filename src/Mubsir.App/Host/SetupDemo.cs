using Mubsir.Core.Common;
using Mubsir.Core.Config;
using Mubsir.Core.Input;
using Mubsir.Core.Onboarding;
using Mubsir.Core.Setup;
using Mubsir.Overlay;

namespace Mubsir.App.Host;

/// <summary><c>Mubsir.exe --setup-demo keys|keys-ar|questions|settings</c>: shows the setup window with nothing behind it, for pictures.</summary>
internal static class SetupDemo
{
    public static int Run(string stage)
    {
        Lang lang = stage.EndsWith("-ar", StringComparison.Ordinal) ? Lang.Arabic : Lang.English;
        Thread window = SetupHost.Show(
            new Driver(),
            lang,
            settingsOnly: stage.StartsWith("settings", StringComparison.Ordinal),
            e => Environment.ExitCode = 1,
            startOnQuestions: stage.StartsWith("questions", StringComparison.Ordinal));
        window.Join();
        return Environment.ExitCode;
    }

    private sealed class Driver : ISetupDriver
    {
        public bool HasKey(KeyKind kind) => false;

        public void OpenKeyPage(KeyKind kind)
        {
        }

        public Task<(bool Saved, string Message)> SaveKeyAsync(KeyKind kind, string key) => Task.FromResult((false, "This is a demo."));

        public OnboardingFacts Facts() => new(false);

        public void Say(string text, Lang lang)
        {
        }

        public void Finished(PersonalPlan plan)
        {
        }

        public Task<string?> ListenAsync(CancellationToken ct) => Task.Delay(Timeout.Infinite, ct).ContinueWith<string?>(_ => null, TaskScheduler.Default);

        public void GiveConsent()
        {
        }

        public void OpenChats()
        {
        }

        public void SetOption(string name, bool value)
        {
        }

        public bool GetOption(string name) => name is "ShowNotch" or "SaveSessions";

        public void InstallNvdaAddon()
        {
        }

        public KeyIdentity GetTalkKey() => KeyIdentity.RightCtrl;

        public void SetTalkKey(KeyIdentity key)
        {
        }

        public string GetBrainModel() => MubsirSettings.DefaultBrainModel;

        public void SetBrainModel(string model)
        {
        }

        public string? GetFishVoiceId() => null;

        public string? GetFishVoiceIdArabic() => null;

        public void SetFishVoiceIdArabic(string? voiceId)
        {
        }

        public void SetFishVoiceId(string? voiceId)
        {
        }

        public void TestVoice(Lang lang)
        {
        }

        public void ReportProblem()
        {
        }

        public void ExportTodayChat()
        {
        }
    }
}
