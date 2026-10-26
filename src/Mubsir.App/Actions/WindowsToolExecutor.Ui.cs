using Mubsir.App.Screen.Interop;
using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.App.Actions;

internal sealed partial class WindowsToolExecutor
{
    private const string NotFound = "element not found; call read_screen again";
    private const int MaxTypedChars = 5000;

    private async Task<(string, bool)> ReadScreenAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var scope = Arg(a, "scope") switch
        {
            "focus" => ReadScope.Focus,
            "document" => ReadScope.Document,
            _ => ReadScope.Window,
        };
        var (screen, built) = await _screen.CaptureContextAsync(scope, ct).ConfigureAwait(false);
        MarkSensitive(s.Task, screen);
        return string.IsNullOrWhiteSpace(built.Text) ? Error("the screen could not be read") : Ok(built.Text);
    }

    private async Task<(string, bool)> InvokeAsync(ExecutorState s, CancellationToken ct)
    {
        if (CheckWindow(s) is { } problem)
        {
            return Error(problem);
        }

        if (s.TargetMissing || s.Element is not { } element)
        {
            return Error(NotFound);
        }

        string? method;
        try
        {
            method = await _screen.RunUiaAsync(
                _ => UiaActions.IsAlive(element, s.TargetRuntimeId) ? UiaActions.Activate(element) ?? "" : null,
                UiaActionTimeout,
                ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return Ok("Pressed, but the app is busy (it may have opened a dialog). Call read_screen.");
        }

        if (method is null)
        {
            return Error(NotFound);
        }

        if (method.Length == 0)
        {
            return Error("this element cannot be activated; try focus and press_keys enter, or another element");
        }

        await Task.Delay(SettleDelay, ct).ConfigureAwait(false);
        return Ok($"Done ({method}). " + await AfterActionAsync(s, ct).ConfigureAwait(false));
    }

    private async Task<(string, bool)> SetTextAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var text = a.Args.TryGetValue("text", out var t) ? t ?? "" : "";
        if (a.Target?.IsPassword == true)
        {
            return Error("password fields are never filled by Mubsir; the user types passwords personally");
        }

        if (CheckWindow(s) is { } problem)
        {
            return Error(problem);
        }

        if (s.TargetMissing || s.Element is not { } element)
        {
            return Error(NotFound);
        }

        var outcome = await _screen.RunUiaAsync<(string State, string? Value)>(
            _ =>
            {
                if (!UiaActions.IsAlive(element, s.TargetRuntimeId))
                {
                    return ("missing", null);
                }

                if (UiaActions.IsPassword(element))
                {
                    return ("password", null);
                }

                var value = UiaActions.SetValue(element, text);
                if (value is not null && SameText(value, text))
                {
                    return ("set", value);
                }

                return UiaActions.SetFocus(element) ? ("focused", null) : ("no_focus", null);
            },
            UiaActionTimeout,
            ct).ConfigureAwait(false);

        switch (outcome.State)
        {
            case "missing":
                return Error(NotFound);
            case "password":
                return Error("password fields are never filled by Mubsir; the user types passwords personally");
            case "no_focus":
                return Error("the field could not be focused; try invoke on it first");
            case "set":
                return Ok("The field now contains: " + Quote(outcome.Value));
        }

        await Task.Delay(150, ct).ConfigureAwait(false);
        if (CheckWindow(s) is { } changed)
        {
            return Error(changed);
        }

        bool typed = await Task.Run(
            () => InputSender.SendStroke(new KeyStroke([KeyChordParser.VkControl], 'A', "ctrl+a")) && InputSender.TypeText(Cap(text), ct),
            ct).ConfigureAwait(false);
        if (!typed)
        {
            return Error("typing was blocked by Windows");
        }

        await Task.Delay(200, ct).ConfigureAwait(false);
        var readBack = await _screen.RunUiaAsync(_ => UiaActions.ReadValue(element), UiaActionTimeout, ct).ConfigureAwait(false);
        return Ok(readBack is null
            ? "Typed the text (the field does not report its contents)."
            : "The field now contains: " + Quote(readBack));
    }

    private async Task<(string, bool)> TypeTextAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var text = a.Args.TryGetValue("text", out var t) ? t ?? "" : "";
        if (text.Length == 0)
        {
            return Error("no text to type");
        }

        if (a.Target?.IsPassword == true)
        {
            return Error("the focus is on a password field; the user types passwords personally");
        }

        if (await CheckFocusAsync(s, ct).ConfigureAwait(false) is { } problem)
        {
            return Error(problem);
        }

        if (!await Task.Run(() => InputSender.TypeText(Cap(text), ct), ct).ConfigureAwait(false))
        {
            return Error("typing was blocked by Windows");
        }

        return Ok(text.Length > MaxTypedChars ? $"Typed the first {MaxTypedChars} characters." : "Typed.");
    }

    private async Task<(string, bool)> PressKeysAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var strokes = KeyChordParser.ParseAll(a.KeyChord ?? Arg(a, "keys"));
        if (strokes is null)
        {
            return Error("unknown key name; use names like enter, tab, escape, ctrl+t, alt+left, pagedown, f5");
        }

        if (await CheckFocusAsync(s, ct).ConfigureAwait(false) is { } problem)
        {
            return Error(problem);
        }

        if (!await Task.Run(() => InputSender.SendStrokes(strokes), ct).ConfigureAwait(false))
        {
            return Error("the key press was blocked by Windows");
        }

        await Task.Delay(SettleDelay, ct).ConfigureAwait(false);
        return Ok($"Pressed {string.Join(' ', strokes.Select(k => k.Chord))}. " + await AfterActionAsync(s, ct).ConfigureAwait(false));
    }

    private async Task<(string, bool)> ScrollAsync(AgentAction a, ExecutorState s, CancellationToken ct)
    {
        var direction = Arg(a, "direction") ?? "down";
        var amount = Arg(a, "amount") ?? "page";
        if (CheckWindow(s) is { } problem)
        {
            return Error(problem);
        }

        if (s.TargetMissing)
        {
            return Error(NotFound);
        }

        var start = s.Element;
        nint hwnd = s.Hwnd;
        double? position = await _screen.RunUiaAsync(
            uia =>
            {
                var e = start is not null && UiaActions.IsAlive(start, s.TargetRuntimeId) ? start : null;
                e ??= uia.GetFocusedElement() ?? (hwnd != 0 ? uia.ElementFromHandle(hwnd) : null);
                return e is null ? null : UiaActions.Scroll(uia, e, direction, amount);
            },
            UiaActionTimeout,
            ct).ConfigureAwait(false);

        if (position is { } p)
        {
            return Ok($"Scrolled {direction}; position {UiaActions.Percent(p)}.");
        }

        var keys = (direction, amount) switch
        {
            (_, "top") => "ctrl+home",
            (_, "bottom") => "ctrl+end",
            ("up", "page") => "pageup",
            ("down", "page") => "pagedown",
            ("up", _) => "up up up",
            ("down", _) => "down down down",
            ("left", _) => "left",
            _ => "right",
        };
        var strokes = KeyChordParser.ParseAll(keys)!;
        if (!await Task.Run(() => InputSender.SendStrokes(strokes), ct).ConfigureAwait(false))
        {
            return Error("scrolling was blocked by Windows");
        }

        return Ok($"Scrolled {direction} with the keyboard.");
    }

    private async Task<(string, bool)> FocusAsync(ExecutorState s, CancellationToken ct)
    {
        if (CheckWindow(s) is { } problem)
        {
            return Error(problem);
        }

        if (s.TargetMissing || s.Element is not { } element)
        {
            return Error(NotFound);
        }

        bool? ok = await _screen.RunUiaAsync(
            _ => UiaActions.IsAlive(element, s.TargetRuntimeId) ? UiaActions.SetFocus(element) : (bool?)null,
            UiaActionTimeout,
            ct).ConfigureAwait(false);
        if (ok is null)
        {
            return Error(NotFound);
        }

        if (ok == false)
        {
            return Error("this element cannot take the keyboard focus");
        }

        await Task.Delay(150, ct).ConfigureAwait(false);
        return Ok(await AfterActionAsync(s, ct).ConfigureAwait(false));
    }

    private string? CheckWindow(ExecutorState s)
    {
        nint fg = ScreenNative.GetForegroundWindow();
        if (fg == 0)
        {
            return "no window is in front";
        }

        if (s.Hwnd != 0 && fg != s.Hwnd)
        {
            return "the window in front changed; call read_screen again";
        }

        if (_screen.GetForeground().IsElevated)
        {
            return "this is an administrator window; Mubsir cannot control it (NVDA can read it)";
        }

        return null;
    }

    private async Task<string?> CheckFocusAsync(ExecutorState s, CancellationToken ct)
    {
        if (CheckWindow(s) is { } problem)
        {
            return problem;
        }

        if (s.TargetRuntimeId is null)
        {
            return null;
        }

        string? now = null;
        try
        {
            now = await _screen.RunFastAsync(UiaActions.FocusedRuntimeId, FastTimeout, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return "the app is not responding; try again in a moment";
        }

        return now is not null && now != s.TargetRuntimeId ? "the keyboard focus moved; call read_screen again" : null;
    }

    private async Task<string> AfterActionAsync(ExecutorState s, CancellationToken ct)
    {
        var fg = _screen.GetForeground();
        var parts = new List<string>();
        if (fg.Hwnd != s.Hwnd && fg.Hwnd != 0)
        {
            var app = _screen.FriendlyAppName(fg);
            parts.Add($"The window in front is now \"{ScreenText.Short(fg.WindowTitle, 100) ?? app}\"{(app is null ? "" : $" ({app})")}.");
        }

        var focused = await FocusedNodeAsync(fg, ct).ConfigureAwait(false);
        if (focused is not null)
        {
            parts.Add("Focus: " + ScreenText.Describe(focused, Lang.English) + ".");
        }

        return parts.Count == 0 ? "" : string.Join(' ', parts);
    }

    private static bool SameText(string actual, string expected) =>
        string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string Cap(string text) => text.Length <= MaxTypedChars ? text : text[..MaxTypedChars];

    private static string Quote(string? value)
    {
        var v = value ?? "";
        return "\"" + (v.Length > 300 ? v[..300] + "…" : v) + "\"";
    }
}
