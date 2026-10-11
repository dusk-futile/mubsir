using Mubsir.App.Input;
using Mubsir.App.Native;
using Mubsir.Core.Localization;
using Mubsir.Core.Logging;

namespace Mubsir.App.Orchestration;

internal sealed class ForegroundWatcher : IDisposable
{
    private readonly AppServices _app;
    private readonly TaskRunner _tasks;
    private readonly KeyboardHookService _hook;
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly bool _selfElevated;
    private WinEventHook? _winEvent;
    private nint _lastAnnounced;

    public ForegroundWatcher(AppServices app, TaskRunner tasks, KeyboardHookService hook)
    {
        _app = app;
        _tasks = tasks;
        _hook = hook;
        _selfElevated = IsSelfElevated();
    }

    public void Start()
    {
        _winEvent = WinEventHook.Install(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, OnEvent, out int error);
        if (_winEvent is null)
        {
            _app.Log.Error("foreground_hook_" + error);
        }
    }

    public void Dispose() => _winEvent?.Dispose();

    private static bool IsSelfElevated()
    {
        try
        {
            return NativeQueries.IsProcessElevated((uint)Environment.ProcessId);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return false;
        }
    }

    private void OnEvent(uint eventType, nint hwnd, int idObject, int idChild)
    {
        if (hwnd == 0 || idObject != Win32.OBJID_WINDOW || _selfElevated)
        {
            return;
        }

        ThreadPool.QueueUserWorkItem(_ => Check(hwnd));
    }

    private void Check(nint hwnd)
    {
        try
        {
            User32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0 || pid == _ownPid)
            {
                return;
            }

            bool elevated = NativeQueries.IsProcessElevated(pid);
            _hook.HeartbeatSuppressed = elevated;
            if (!elevated)
            {
                return;
            }

            bool stoppedTask = _tasks.Cancel();
            if (stoppedTask || Interlocked.Exchange(ref _lastAnnounced, hwnd) != hwnd)
            {
                Interlocked.Exchange(ref _lastAnnounced, hwnd);
                _app.Log.Event(LogKind.Policy, stoppedTask ? LogOutcome.Cancelled : LogOutcome.Blocked, tool: "admin_window");
                _app.SayNow(StringKey.AdminWindow);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("foreground_check", e);
        }
    }
}
