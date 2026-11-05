using Mubsir.App.Native;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;

namespace Mubsir.App.Ui;

internal enum RequestBoxStatus
{
    Submitted,
    Cancelled,

    Unavailable,
}

internal sealed record RequestBoxResult(RequestBoxStatus Status, string? Text);

internal sealed class RequestBox
{
    private const string ClassName = "MubsirRequestBox";
    private const int EditId = 100;
    private const int LabelId = 101;
    private const nuint VerifyTimerId = 1;
    private const nuint ExpireTimerId = 2;
    private const uint VerifyIntervalMs = 50;
    private const int VerifyTicks = 10;
    private const uint ExpireMs = 3 * 60 * 1000;
    private const int MaxChars = 2000;

    private readonly Lang _lang;
    private readonly IEventLog _log;
    private readonly TaskCompletionSource<RequestBoxResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private nint _hwnd;
    private nint _edit;
    private int _ticks;
    private bool _verified;
    private bool _finished;

    private RequestBox(Lang lang, IEventLog log)
    {
        _lang = lang;
        _log = log;
    }

    public static async Task<RequestBoxResult> ShowAsync(Lang lang, IEventLog log, CancellationToken ct)
    {
        var box = new RequestBox(lang, log);
        var thread = new Thread(box.ThreadMain)
        {
            IsBackground = true,
            Name = "Mubsir request box",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        using (ct.Register(static b => ((RequestBox)b!).RequestClose(), box))
        {
            try
            {
                return await box._result.Task.WaitAsync(TimeSpan.FromMilliseconds(ExpireMs + 10_000)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                log.Error("request_box_stuck", LogKind.Error, LogOutcome.TimedOut);
                box.RequestClose();
                return new RequestBoxResult(RequestBoxStatus.Cancelled, null);
            }
        }
    }

    private void RequestClose()
    {
        nint hwnd = Volatile.Read(ref _hwnd);
        if (hwnd != 0)
        {
            User32.PostMessageW(hwnd, Win32.WM_CLOSE, 0, 0);
        }
    }

    private void ThreadMain()
    {
        try
        {
            string title = AppText.Get(AppPhrase.RequestBoxTitle, Lang.Arabic);
            nint hwnd = NativeWindow.Create(
                ClassName,
                title,
                Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU,
                Win32.WS_EX_TOPMOST | Win32.WS_EX_APPWINDOW,
                0,
                Win32.CW_USEDEFAULT,
                Win32.CW_USEDEFAULT,
                540,
                150,
                OnMessage,
                out int error);
            if (hwnd == 0)
            {
                _log.Error("request_box_create_" + error);
                Complete(RequestBoxStatus.Unavailable, null);
                return;
            }

            Volatile.Write(ref _hwnd, hwnd);

            NativeWindow.CreateControl("STATIC", AppText.Get(AppPhrase.RequestBoxLabel, _lang), Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_LEFT, hwnd, 12, 10, 500, 24, LabelId, out _);
            _edit = NativeWindow.CreateControl("EDIT", string.Empty, Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP | Win32.ES_AUTOHSCROLL, hwnd, 12, 42, 500, 28, EditId, out error);
            if (_edit == 0)
            {
                _log.Error("request_box_edit_" + error);
                Finish(RequestBoxStatus.Unavailable, null);
                NativeWindow.RunMessageLoop();
                return;
            }

            User32.SendMessageW(_edit, Win32.EM_LIMITTEXT, MaxChars, 0);
            User32.ShowWindow(hwnd, Win32.SW_SHOWNORMAL);
            ForceForeground(hwnd, _edit);
            if (User32.SetTimer(hwnd, VerifyTimerId, VerifyIntervalMs, 0) == 0 || User32.SetTimer(hwnd, ExpireTimerId, ExpireMs, 0) == 0)
            {
                _log.Error("request_box_timer_" + NativeQueries.LastError());
                Finish(RequestBoxStatus.Unavailable, null);
            }

            NativeWindow.RunMessageLoop(Preprocess);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("request_box", e);
        }
        finally
        {
            Complete(RequestBoxStatus.Cancelled, null);
        }
    }

    private bool Preprocess(MSG msg)
    {
        if (msg.Message != Win32.WM_KEYDOWN || msg.Hwnd != _edit || _finished)
        {
            return false;
        }

        switch ((int)msg.WParam)
        {
            case Win32.VK_RETURN:
                if (!_verified)
                {
                    return true;
                }

                string text = ReadEditText();
                Finish(string.IsNullOrWhiteSpace(text) ? RequestBoxStatus.Cancelled : RequestBoxStatus.Submitted, text.Trim());
                return true;
            case Win32.VK_ESCAPE:
                Finish(RequestBoxStatus.Cancelled, null);
                return true;
            default:
                return false;
        }
    }

    private nint? OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_TIMER when (nuint)wParam == VerifyTimerId:
                VerifyForeground(hwnd);
                return 0;
            case Win32.WM_TIMER when (nuint)wParam == ExpireTimerId:
                Finish(RequestBoxStatus.Cancelled, null);
                return 0;
            case Win32.WM_ACTIVATE when _verified && (wParam & 0xFFFF) == Win32.WA_INACTIVE:

                Finish(RequestBoxStatus.Cancelled, null);
                return 0;
            case Win32.WM_SETFOCUS when _edit != 0:
                User32.SetFocus(_edit);
                return 0;
            case Win32.WM_CLOSE:
                Finish(RequestBoxStatus.Cancelled, null);
                return 0;
            case Win32.WM_DESTROY:
                User32.PostQuitMessage(0);
                return 0;
            default:
                return null;
        }
    }

    private void VerifyForeground(nint hwnd)
    {
        if (User32.GetForegroundWindow() == hwnd)
        {
            _verified = true;
            User32.KillTimer(hwnd, VerifyTimerId);
            User32.SetFocus(_edit);
            _log.Event(LogKind.Hotkey, tool: "request_box_open");
            return;
        }

        _ticks++;
        if (_ticks == VerifyTicks / 2)
        {
            ForceForeground(hwnd, _edit);
        }

        if (_ticks >= VerifyTicks)
        {
            User32.KillTimer(hwnd, VerifyTimerId);
            _log.Error("request_box_not_foreground", LogKind.Hotkey);
            Finish(RequestBoxStatus.Unavailable, null);
        }
    }

    private void ForceForeground(nint hwnd, nint focus)
    {
        var (_, _, fgThread) = NativeQueries.Foreground();
        uint me = Kernel32.GetCurrentThreadId();
        bool attached = fgThread != 0 && fgThread != me && User32.AttachThreadInput(me, fgThread, true);
        try
        {
            User32.AllowSetForegroundWindow(Win32.ASFW_ANY);
            User32.BringWindowToTop(hwnd);
            if (!User32.SetForegroundWindow(hwnd))
            {
                _log.Error("request_box_setforeground", LogKind.Hotkey);
            }

            if (focus != 0)
            {
                User32.SetFocus(focus);
            }
        }
        finally
        {
            if (attached)
            {
                User32.AttachThreadInput(me, fgThread, false);
            }
        }
    }

    private unsafe string ReadEditText()
    {
        int length = User32.GetWindowTextLengthW(_edit);
        if (length <= 0)
        {
            return string.Empty;
        }

        length = Math.Min(length, MaxChars);
        char[] buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            int n = User32.GetWindowTextW(_edit, p, buffer.Length);
            return n > 0 ? new string(p, 0, n) : string.Empty;
        }
    }

    private void Finish(RequestBoxStatus status, string? text)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        Complete(status, text);
        nint hwnd = Interlocked.Exchange(ref _hwnd, 0);
        if (hwnd != 0)
        {
            NativeWindow.Destroy(hwnd);
        }
    }

    private void Complete(RequestBoxStatus status, string? text) =>
        _result.TrySetResult(new RequestBoxResult(status, status == RequestBoxStatus.Submitted ? text : null));
}
