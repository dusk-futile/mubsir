using Mubsir.App.Input;
using Mubsir.App.Native;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;

namespace Mubsir.App.Orchestration;

internal sealed class SessionWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly AppServices _app;
    private readonly TaskRunner _tasks;
    private readonly KeyboardHookService _hook;
    private WinEventHook? _desktopSwitch;
    private nint _registeredHwnd;
    private bool _locked;
    private bool _secureDesktop;
    private bool _suspended;

    public SessionWatcher(AppServices app, TaskRunner tasks, KeyboardHookService hook)
    {
        _app = app;
        _tasks = tasks;
        _hook = hook;
    }

    public void Start(nint hostHwnd)
    {
        if (hostHwnd != 0)
        {
            if (WtsApi32.WTSRegisterSessionNotification(hostHwnd, Win32.NOTIFY_FOR_THIS_SESSION))
            {
                _registeredHwnd = hostHwnd;
            }
            else
            {
                _app.Log.Error("wts_register_" + NativeQueries.LastError());
            }
        }

        _desktopSwitch = WinEventHook.Install(Win32.EVENT_SYSTEM_DESKTOPSWITCH, Win32.EVENT_SYSTEM_DESKTOPSWITCH, (_, _, _, _) => OnDesktopSwitch(), out int error);
        if (_desktopSwitch is null)
        {
            _app.Log.Error("desktop_hook_" + error);
        }
    }

    public void OnSessionChange(int code)
    {
        switch (code)
        {
            case Win32.WTS_SESSION_LOCK:
            case Win32.WTS_CONSOLE_DISCONNECT:
            case Win32.WTS_REMOTE_DISCONNECT:
                lock (_gate)
                {
                    _locked = true;
                }

                Update("lock");
                break;
            case Win32.WTS_SESSION_UNLOCK:
            case Win32.WTS_CONSOLE_CONNECT:
            case Win32.WTS_REMOTE_CONNECT:
                lock (_gate)
                {
                    _locked = false;
                    _secureDesktop = !NativeQueries.IsInputDesktopDefault();
                }

                Update("unlock");
                break;
        }
    }

    public void Dispose()
    {
        _desktopSwitch?.Dispose();
        if (_registeredHwnd != 0)
        {
            WtsApi32.WTSUnRegisterSessionNotification(_registeredHwnd);
            _registeredHwnd = 0;
        }
    }

    private void OnDesktopSwitch()
    {
        bool secure = !NativeQueries.IsInputDesktopDefault();
        lock (_gate)
        {
            _secureDesktop = secure;
        }

        Update(secure ? "secure_desktop" : "default_desktop");
    }

    private void Update(string reason)
    {
        bool suspend;
        bool changed;
        lock (_gate)
        {
            suspend = _locked || _secureDesktop;
            changed = suspend != _suspended;
            _suspended = suspend;
        }

        if (!changed)
        {
            return;
        }

        try
        {
            _app.Log.Event(LogKind.Policy, suspend ? LogOutcome.Cancelled : LogOutcome.Ok, tool: "session_" + reason);
            if (suspend)
            {
                _tasks.Pause();
                _app.Speech.SetSuspended(true);
                _app.Recorder.Cancel();
                _app.Input.ResetKeyState();
                return;
            }

            _app.Speech.SetSuspended(false);
            _app.Input.ResetKeyState();
            _hook.RequestReinstall(HookReinstallReason.Unlock);
            if (_tasks.IsRunning)
            {
                _app.Say(StringKey.TaskPaused);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("session_update", e);
        }
    }
}
