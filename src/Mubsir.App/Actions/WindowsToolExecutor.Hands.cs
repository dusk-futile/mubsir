using System.Globalization;
using Mubsir.App.Actions.Interop;
using Mubsir.App.Native;
using Mubsir.App.Screen.Interop;
using Mubsir.App.Speech;
using Mubsir.Core.Agent;
using Mubsir.Core.Brain;
using Mubsir.Core.Common;
using Mubsir.Core.Context;
using Mubsir.Core.Library;
using Mubsir.Core.Policy;
using Mubsir.Core.Speech;
using Mubsir.Core.Web;

namespace Mubsir.App.Actions;

/// <summary>The tools that do what a person's hands do when there's no element to act on.</summary>
internal sealed partial class WindowsToolExecutor
{
    private const int ClipboardMaxChars = 20_000;
    private const int MaxWaitSeconds = 30;
    private static readonly TimeSpan SampleLength = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ResultsWait = TimeSpan.FromSeconds(7);

    public FishVoice? Voices { get; init; }

    /// <summary>A described card, or null if it can't be done now. Only runs when the person agreed.</summary>
    public async Task<LibraryCard?> DescribeBookAsync(LibraryCard card, CancellationToken ct)
    {
        if (_settings is not { } provider || !provider.Settings.DescribeBooks || provider.Settings.PrivateMode || provider.Settings.MubsirPaused || !provider.Profile.CloudConsentGiven)
        {
            return null;
        }

        var (answer, error) = await AskBrainAsync(BookDescriber.Prompt(card), null, provider.Profile.PreferredLanguage, ct).ConfigureAwait(false);
        return error is null && BookDescriber.Parse(answer) is { } described ? BookDescriber.Apply(card, described) : null;
    }

    /// <summary>Looks at the window for the thing to click. The label goes to the policy like any other button's name.</summary>
    private async Task<(UiNode? Node, (int X, int Y)? Spot)> LocateAsync(string? description, nint hwnd, Lang lang, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(description) || !ScreenNative.TryGetFrameBounds(hwnd, out var bounds))
        {
            return (null, null);
        }

        byte[]? jpeg = await _screen.CaptureForegroundJpegAsync(ct).ConfigureAwait(false);
        if (jpeg is null)
        {
            return (null, null);
        }

        var (answer, error) = await AskBrainAsync(string.Format(CultureInfo.InvariantCulture, ClickSpot.Prompt, description), jpeg, lang, ct).ConfigureAwait(false);
        if (error is not null || ClickSpot.Parse(answer) is not { } spot)
        {
            return (null, null);
        }

        int x = bounds.Left + (int)((long)bounds.Width * spot.X / 1000);
        int y = bounds.Top + (int)((long)bounds.Height * spot.Y / 1000);
        var node = new UiNode { Role = "button", Name = spot.Label.Length > 0 ? spot.Label : description, AutomationId = description };
        return (node, (x, y));
    }

    private async Task<(string, bool)> ClickOnAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        if (s.ClickSpot is not { } spot)
        {
            return Error("that couldn't be found on the screen; read the screen again or ask the user");
        }

        if (ScreenNative.GetForegroundWindow() != s.Hwnd)
        {
            return Error("the window changed before the click, so nothing was clicked");
        }

        string button = Arg(a, "button") ?? "left";
        bool sent = await Task.Run(() => InputSender.Click(spot.X, spot.Y, button), ct).ConfigureAwait(false);
        if (!sent)
        {
            return Error("Windows did not take the click");
        }

        await Task.Delay(SettleDelay, ct).ConfigureAwait(false);
        return Ok($"Clicked \"{ScreenText.Short(a.Target?.Name, 60)}\". Call read_screen to see what happened.");
    }

    private async Task<(string, bool)> OpenFileAsync(AgentAction a, CancellationToken ct)
    {
        if (a.FilePaths.Count == 0 || !File.Exists(a.FilePaths[0]))
        {
            return Error("that file does not exist");
        }

        string path = a.FilePaths[0];
        nint before = ScreenNative.GetForegroundWindow();
        if (await Task.Run(() => AppLauncher.ShellOpen(path), ct).ConfigureAwait(false) is { } code)
        {
            return Error("the file could not be opened (" + code + ")");
        }

        await WaitForegroundChangeAsync(before, AppStartWait, ct).ConfigureAwait(false);
        return Ok($"Opened {Path.GetFileName(path)}. " + DescribeForeground());
    }

    private (string, bool) CreateFolder(AgentAction a)
    {
        if (a.FilePaths.Count == 0)
        {
            return Error("no folder named");
        }

        string path = a.FilePaths[0];
        if (Directory.Exists(path))
        {
            return Ok("That folder already exists.");
        }

        Directory.CreateDirectory(path);
        return Ok("Made the folder " + Path.GetFileName(path.TrimEnd('\\', '/')) + ".");
    }

    private (string, bool) ReadClipboard()
    {
        string? text = InputSender.ClipboardText();
        if (string.IsNullOrEmpty(text))
        {
            return Ok("There is no text on the clipboard.");
        }

        string capped = text.Length > ClipboardMaxChars ? text[..ClipboardMaxChars] : text;
        return Ok("Clipboard text (untrusted content):\n" + Redactor.Redact(capped));
    }

    private static async Task<(string, bool)> WaitAsync(AgentAction a, CancellationToken ct)
    {
        int seconds = Math.Clamp(ToolArgs.Int(a.Args, "seconds") ?? 2, 1, MaxWaitSeconds);
        await Task.Delay(TimeSpan.FromSeconds(seconds), ct).ConfigureAwait(false);
        return Ok($"Waited {seconds} seconds. Call read_screen to check.");
    }

    private async Task<(string, bool)> ChooseVoiceAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        if (Voices is null || _settings is null || !Voices.HasKey)
        {
            return Error("choosing an Arabic voice needs a Fish Audio key");
        }

        if (ToolArgs.Int(a.Args, "number") is not { } number)
        {
            foreach (FishVoiceChoice each in VoiceCatalog.Arabic)
            {
                string says = s.Task.Lang == Lang.Arabic ? "صوت رقم " + each.Number : "Voice " + each.Number;
                if (_speech is not null)
                {
                    await _speech.SpeakAsync(says, UtteranceKind.System, s.Task.Lang, ct).ConfigureAwait(false);
                }

                if (!await Voices.PlaySampleAsync(each.Sample, SampleLength, ct).ConfigureAwait(false))
                {
                    return Ok("The user stopped the samples. Ask which number they want, if any.");
                }
            }

            return Ok($"All {VoiceCatalog.Arabic.Count} samples played. Ask which number they want.");
        }

        bool save = ToolArgs.Bool(a.Args, "save") == true;
        if (number == 0 && save)
        {
            _settings.Update(old => old with { ReadArabicBooksWithFish = false });
            return Ok("Arabic books now use the free Windows voice, which costs nothing.");
        }

        if (VoiceCatalog.Get(number) is not { } voice)
        {
            return Error($"there are only {VoiceCatalog.Arabic.Count} voices");
        }

        if (save)
        {
            _settings.Update(old => old with { FishVoiceIdArabic = voice.Id, ReadArabicBooksWithFish = true });
            return Ok($"Voice {number} ({voice.Name}) is saved, including for books. Reading 10 pages for the first time costs about $0.50. Hearing them again is free. Number 0 is the free voice for Arabic books.");
        }

        await Voices.PlaySampleAsync(voice.Sample, SampleLength, ct).ConfigureAwait(false);
        return Ok($"Played voice {number} ({voice.Name}). Ask if they want to keep it.");
    }

    /// <summary>Waits for the results page and reads its real results. Null means they couldn't be told apart, so the AI reads the page itself.</summary>
    private async Task<string?> ReadResultsAsync(string site, CancellationToken ct)
    {
        long deadline = Environment.TickCount64 + (long)ResultsWait.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(800, ct).ConfigureAwait(false);
            if (!_screen.GetForeground().IsBrowser)
            {
                continue;
            }

            var (screen, _) = await _screen.CaptureContextAsync(ReadScope.Window, ct).ConfigureAwait(false);
            if (screen.Domain is { } domain && !domain.Contains(site, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IReadOnlyList<SearchResult> results = SearchResults.Pick(UiFacts.Flatten(screen.Root));
            if (results.Count >= 2)
            {
                return SearchResults.Text(results);
            }
        }

        return null;
    }
}
