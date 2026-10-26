using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Mubsir.App.Actions.Interop;
using Mubsir.App.Screen.Interop;
using Mubsir.Core.Policy;

namespace Mubsir.App.Actions;

internal sealed partial class WindowsToolExecutor
{
    private static readonly TimeSpan SearchBoxWait = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AppStartWait = TimeSpan.FromSeconds(5);

    private async Task<(string, bool)> ListWindowsAsync(CancellationToken ct)
    {
        var windows = await Task.Run(ListWindows, ct).ConfigureAwait(false);
        if (windows.Count == 0)
        {
            return Ok("No open windows.");
        }

        var sb = new StringBuilder($"{windows.Count} open windows (most recent first):\n");
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            sb.Append(i + 1).Append(". ").Append(ScreenText.Short(w.Title, 100));
            if (w.AppName is { } app && !w.Title.Contains(app, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" - ").Append(app);
            }

            sb.Append('\n');
        }

        return Ok(sb.ToString().TrimEnd());
    }

    private async Task<(string, bool)> SwitchToAsync(AgentAction a, CancellationToken ct)
    {
        var query = Arg(a, "window");
        if (query is null)
        {
            return Error("no window named");
        }

        var windows = await Task.Run(ListWindows, ct).ConfigureAwait(false);
        WindowEntry? target = null;
        if (int.TryParse(query.TrimEnd('.'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
        {
            List<WindowEntry> numbered;
            lock (_windowsGate)
            {
                numbered = _lastWindows;
            }

            target = n >= 1 && n <= numbered.Count ? numbered[n - 1] : null;
        }

        target ??= FuzzyMatch.Best(
            query,
            windows,
            w => [w.Title, w.AppName ?? "", Path.GetFileNameWithoutExtension(w.ProcessName ?? "")],
            threshold: 0.5)?.Item;
        if (target is null)
        {
            return Error("no open window matches; call list_windows");
        }

        bool ok = await Task.Run(() => WindowSwitcher.BringToFront(target.Hwnd), ct).ConfigureAwait(false);
        if (!ok)
        {
            return Error("Windows did not let Mubsir bring that window to the front; the user can press Alt+Tab");
        }

        return Ok($"Switched to \"{ScreenText.Short(target.Title, 100)}\".");
    }

    private async Task<(string, bool)> OpenAppAsync(AgentAction a, CancellationToken ct)
    {
        var name = Arg(a, "name");
        if (name is null)
        {
            return Error("no app named");
        }

        var target = await Task.Run(() => _apps.Resolve(name), ct).ConfigureAwait(false);
        if (target is null)
        {
            return Error("no installed app with that name was found; ask the user for the exact name");
        }

        nint before = ScreenNative.GetForegroundWindow();
        if (AppLauncher.Launch(target) is { } code)
        {
            return Error("the app could not be started (" + code + ")");
        }

        await WaitForegroundChangeAsync(before, AppStartWait, ct).ConfigureAwait(false);
        return Ok($"Opened {target.Display}. " + DescribeForeground());
    }

    private async Task<(string, bool)> OpenUrlAsync(AgentAction a, CancellationToken ct)
    {
        var raw = a.Url ?? Arg(a, "url");
        if (raw is null || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return Error("only http and https addresses can be opened");
        }

        nint before = ScreenNative.GetForegroundWindow();
        if (await Task.Run(() => AppLauncher.ShellOpen(uri.AbsoluteUri), ct).ConfigureAwait(false) is { } code)
        {
            return Error("the browser could not be opened (" + code + ")");
        }

        await WaitForegroundChangeAsync(before, AppStartWait, ct).ConfigureAwait(false);
        return Ok($"Opened {uri.Host} in the browser. Call read_screen when the page has loaded.");
    }

    private async Task<(string, bool)> BrowseSearchAsync(AgentAction a, CancellationToken ct)
    {
        var query = Arg(a, "query");
        if (query is null)
        {
            return Error("no search words");
        }

        bool youtube = Arg(a, "engine") == "youtube";
        var home = youtube ? "https://www.youtube.com/" : "https://www.google.com/";
        var results = youtube
            ? "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(query)
            : "https://www.google.com/search?q=" + Uri.EscapeDataString(query);

        if (await Task.Run(() => AppLauncher.ShellOpen(home), ct).ConfigureAwait(false) is { } code)
        {
            return Error("the browser could not be opened (" + code + ")");
        }

        long deadline = Environment.TickCount64 + (long)SearchBoxWait.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(700, ct).ConfigureAwait(false);
            var fg = _screen.GetForeground();
            if (!fg.IsBrowser || fg.IsElevated)
            {
                continue;
            }

            var (screen, _) = await _screen.CaptureContextAsync(ReadScope.Window, ct).ConfigureAwait(false);
            if (screen.Domain is { } domain && !domain.Contains(youtube ? "youtube." : "google.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var box = UiFacts.Flatten(screen.Root).FirstOrDefault(n => n.IsSearchBox && !n.IsAddressBar && n.IsEnabled && !n.IsPassword);
            if (box is null || _screen.Registry.ElementOf(box) is not { } element)
            {
                continue;
            }

            bool typed = await _screen.RunUiaAsync(
                _ =>
                {
                    if (!UiaActions.SetFocus(element))
                    {
                        return false;
                    }

                    var value = UiaActions.SetValue(element, query);
                    return value is not null && SameText(value, query);
                },
                UiaActionTimeout,
                ct).ConfigureAwait(false);

            if (ScreenNative.GetForegroundWindow() != fg.Hwnd)
            {
                break;
            }

            bool sent = await Task.Run(
                () => (typed || (InputSender.SendStroke(new KeyStroke([KeyChordParser.VkControl], 'A', "ctrl+a")) && InputSender.TypeText(query, ct)))
                    && InputSender.SendStroke(new KeyStroke([], KeyChordParser.VkReturn, "enter")),
                ct).ConfigureAwait(false);
            if (sent)
            {
                return Ok($"Searched {(youtube ? "YouTube" : "Google")} for \"{query}\". Call read_screen to read the results.");
            }

            break;
        }

        if (await Task.Run(() => AppLauncher.ShellOpen(results), ct).ConfigureAwait(false) is { } code2)
        {
            return Error("the search could not be opened (" + code2 + ")");
        }

        return Ok($"Opened the {(youtube ? "YouTube" : "Google")} results for \"{query}\". Call read_screen when the page has loaded.");
    }

    private async Task<(string, bool)> MediaAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var action = Arg(a, "action") ?? "play_pause";
        var keys = action switch
        {
            "next" => "medianext",
            "previous" => "mediaprev",
            "seek_forward" => "right",
            "seek_back" => "left",
            _ => "mediaplaypause",
        };

        if (action is "seek_forward" or "seek_back" && CheckWindow(s) is { } problem)
        {
            return Error(problem);
        }

        var strokes = KeyChordParser.ParseAll(keys)!;
        if (!await Task.Run(() => InputSender.SendStrokes(strokes), ct).ConfigureAwait(false))
        {
            return Error("the media key was blocked by Windows");
        }

        return Ok("Done: " + action.Replace('_', ' ') + ".");
    }

    private (string, bool) SystemStatus(AgentAction a)
    {
        var what = Arg(a, "what") ?? "all";
        var lines = new List<string>();
        if (what is "battery" or "all")
        {
            lines.Add(SystemTools.Battery());
        }

        if (what is "wifi" or "all")
        {
            lines.Add(SystemTools.Network());
        }

        if (what is "volume" or "all")
        {
            lines.Add(SystemTools.Volume());
        }

        if (what is "time" or "all")
        {
            lines.Add(SystemTools.Time());
        }

        return Ok(string.Join('\n', lines));
    }

    private (string, bool) SetVolume(AgentAction a)
    {
        var level = ToolArgs.Int(a.Args, "level");
        var mute = ToolArgs.Bool(a.Args, "mute");
        if (level is null && mute is null)
        {
            return Ok(SystemTools.Volume());
        }

        return SystemVolume.Set(level, mute) ? Ok("Done. " + SystemTools.Volume()) : Error("the volume could not be changed");
    }

    private async Task<(string, bool)> OpenSettingsAsync(AgentAction a, CancellationToken ct)
    {
        var page = (Arg(a, "page") ?? "").Trim().ToLowerInvariant();
        if (page.StartsWith("ms-settings:", StringComparison.Ordinal))
        {
            page = page["ms-settings:".Length..];
        }

        if (!SettingsPage().IsMatch(page))
        {
            return Error("not a Settings page name");
        }

        if (await Task.Run(() => AppLauncher.ShellOpen("ms-settings:" + page), ct).ConfigureAwait(false) is { } code)
        {
            return Error("Settings could not be opened (" + code + ")");
        }

        return Ok("Opened Settings. Call read_screen when it has loaded.");
    }

    private async Task<(string, bool)> WindowsUpdateAsync(CancellationToken ct) =>
        Ok(await SystemTools.WindowsUpdateAsync(ct).ConfigureAwait(false));

    private (string, bool) Restart() =>
        SystemTools.Restart() is { } code
            ? Error("the restart could not be started (" + code + ")")
            : Ok("The computer restarts in 30 seconds. Tell the user to save their work.");

    private (string, bool) Remember(AgentAction a)
    {
        var fact = Arg(a, "fact");
        if (fact is null)
        {
            return Error("nothing to remember");
        }

        if (_memory is null)
        {
            return Error("memory is not available");
        }

        _memory.Append(fact.Length > 500 ? fact[..500] : fact);
        return Ok("Remembered.");
    }

    private async Task<(string, bool)> AskUserAsync(AgentAction a, CancellationToken ct)
    {
        var question = Arg(a, "question");
        if (question is null)
        {
            return Error("no question");
        }

        if (_prompter is null)
        {
            return Error("asking the user is not available");
        }

        var answer = await _prompter.AskAsync(question, ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(answer) ? Ok("The user did not answer.") : Ok("The user answered: " + answer.Trim());
    }

    private List<WindowEntry> ListWindows()
    {
        var windows = WindowSwitcher.List((pid, process) =>
            _screen.FriendlyAppName(new ForegroundInfo(0, pid, process, null, null, false, false)));
        lock (_windowsGate)
        {
            _lastWindows = windows;
        }

        return windows;
    }

    private static async Task WaitForegroundChangeAsync(nint before, TimeSpan wait, CancellationToken ct)
    {
        long until = Environment.TickCount64 + (long)wait.TotalMilliseconds;
        while (Environment.TickCount64 < until)
        {
            await Task.Delay(250, ct).ConfigureAwait(false);
            nint now = ScreenNative.GetForegroundWindow();
            if (now != 0 && now != before)
            {
                await Task.Delay(500, ct).ConfigureAwait(false);
                return;
            }
        }
    }

    private string DescribeForeground()
    {
        var fg = _screen.GetForeground();
        if (fg.Hwnd == 0)
        {
            return "";
        }

        var app = _screen.FriendlyAppName(fg);
        return $"In front: \"{ScreenText.Short(fg.WindowTitle, 100) ?? app}\"{(app is null ? "" : $" ({app})")}.";
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SettingsPage();
}
