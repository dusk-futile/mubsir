using Mubsir.App.Actions.Interop;
using Mubsir.App.Screen.Interop;
using Mubsir.Core.Chat;
using Mubsir.Core.Policy;

namespace Mubsir.App.Actions;

internal enum ChatFailure
{
    None,
    NoBrowser,
    NeedsSignIn,
    NoBox,
    NoAnswer,
}

/// <summary>Asks the person's own AI chat in their browser, the way they'd type it themselves. The reply is only read aloud, and nothing acts on it.</summary>
internal sealed partial class WindowsToolExecutor
{
    private static readonly TimeSpan ChatBoxWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ChatAnswerWait = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ChatPoll = TimeSpan.FromMilliseconds(1500);

    public async Task<(string? Answer, ChatFailure Failure)> AskChatAsync(ChatRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await Task.Run(() => AppLauncher.ShellOpen(ChatRouting.Url(request.Site)), ct).ConfigureAwait(false) is not null)
        {
            return (null, ChatFailure.NoBrowser);
        }

        string domain = ChatRouting.Domain(request.Site);
        long deadline = Environment.TickCount64 + (long)ChatBoxWait.TotalMilliseconds;
        bool signIn = false;
        Mubsir.Core.Context.UiNode? box = null;
        nint window = 0;
        while (box is null && Environment.TickCount64 < deadline)
        {
            await Task.Delay(800, ct).ConfigureAwait(false);
            var fg = _screen.GetForeground();
            if (!fg.IsBrowser || fg.IsElevated)
            {
                continue;
            }

            var (screen, _) = await _screen.CaptureContextAsync(ReadScope.Window, ct).ConfigureAwait(false);
            if (screen.Domain is { } site && !site.Contains(domain, StringComparison.OrdinalIgnoreCase))
            {
                signIn |= site.Contains("auth", StringComparison.OrdinalIgnoreCase) || site.Contains("accounts.", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            signIn = ChatAnswer.WantsSignIn(screen.VisibleText);
            box = UiFacts.Flatten(screen.Root).FirstOrDefault(n => n.IsEditable && n.IsEnabled && !n.IsPassword && !n.IsAddressBar);
            window = fg.Hwnd;
        }

        if (box is null || _screen.Registry.ElementOf(box) is not { } element)
        {
            return (null, signIn ? ChatFailure.NeedsSignIn : ChatFailure.NoBox);
        }

        bool focused = await _screen.RunUiaAsync(_ => UiaActions.SetFocus(element), UiaActionTimeout, ct).ConfigureAwait(false);
        if (!focused || ScreenNative.GetForegroundWindow() != window)
        {
            return (null, ChatFailure.NoBox);
        }

        bool sent = await Task.Run(
            () => InputSender.TypeText(request.Question, ct) && InputSender.SendStroke(new KeyStroke([], KeyChordParser.VkReturn, "enter")),
            ct).ConfigureAwait(false);
        if (!sent)
        {
            return (null, ChatFailure.NoBox);
        }

        // It's done when the stop button has gone and the words have stopped changing.
        string? last = null;
        deadline = Environment.TickCount64 + (long)ChatAnswerWait.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(ChatPoll, ct).ConfigureAwait(false);
            if (ScreenNative.GetForegroundWindow() != window)
            {
                continue;
            }

            var (screen, _) = await _screen.CaptureContextAsync(ReadScope.Document, ct).ConfigureAwait(false);
            bool writing = UiFacts.Flatten(screen.Root).Any(n => n.Role == "button" && n.Name is { } name
                && (name.Contains("stop", StringComparison.OrdinalIgnoreCase) || name.Contains("إيقاف", StringComparison.Ordinal)));
            string? answer = ChatAnswer.Extract(screen.VisibleText, request.Question);
            if (!writing && answer is not null && answer == last)
            {
                return (answer, ChatFailure.None);
            }

            last = answer;
        }

        return (null, ChatFailure.NoAnswer);
    }
}
