using System.Diagnostics;
using Basira.App.Diagnostics;
using Basira.App.Input;
using Basira.Core.Localization;
using Basira.Core.Logging;

namespace Basira.App.Orchestration;

/// <summary>
/// Checks NVDA every 5 s. When it disappears Basira says so through OneCore and tries once to start it again; when
/// it returns Basira says so. A new NVDA process re-installs Basira's keyboard hook so it stays ahead of NVDA's.
/// </summary>
internal sealed class NvdaWatcher : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly AppServices _app;
    private readonly KeyboardHookService _hook;
    private Timer? _timer;
    private int _busy;
    private bool? _wasRunning;
    private uint _lastPid;
    private bool _announcedStop;
    private bool _restartTried;

    public NvdaWatcher(AppServices app, KeyboardHookService hook)
    {
        _app = app;
        _hook = hook;
    }

    public void Start() => _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(1), Interval);

    public void Dispose() => _timer?.Dispose();

    private void Tick()
    {
        // NVDA's RPC can stall; never stack ticks.
        if (Interlocked.Exchange(ref _busy, 1) != 0)
        {
            return;
        }

        try
        {
            if (_app.Speech.Suspended || !_app.Nvda.DllAvailable)
            {
                return;
            }

            bool running = _app.Nvda.IsRunning();
            _app.Speech.SetNvdaRunning(running);
            uint pid = running ? _app.Nvda.GetProcessId() : 0;
            if (pid != 0 && _lastPid != 0 && pid != _lastPid)
            {
                _hook.RequestReinstall(HookReinstallReason.Nvda);
            }

            if (pid != 0)
            {
                _lastPid = pid;
            }

            bool useNvda = _app.Settings.Settings.UseNvda;
            if (_wasRunning == true && !running && useNvda)
            {
                OnStopped();
            }
            else if (_wasRunning == false && running)
            {
                OnStarted();
            }

            _wasRunning = running;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _app.Log.Failure("nvda_watch", e);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    private void OnStopped()
    {
        _app.Log.Error("nvda_stopped", LogKind.Speech);
        _announcedStop = true;
        _app.SayFailure(FailureState.NvdaStopped); // NVDA is marked not running, so this goes through OneCore
        if (!_restartTried)
        {
            _restartTried = true;
            TryStartNvda();
        }
    }

    private void OnStarted()
    {
        _hook.RequestReinstall(HookReinstallReason.Nvda);
        _restartTried = false;
        if (_announcedStop)
        {
            _announcedStop = false;
            _app.Log.Event(LogKind.Speech, tool: "nvda_restarted");
            _app.Say(StringKey.NvdaRestarted);
        }
    }

    private void TryStartNvda()
    {
        foreach (string? root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        })
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            string exe = Path.Combine(root, "NVDA", "nvda.exe");
            if (!File.Exists(exe))
            {
                continue;
            }

            try
            {
                using Process? started = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                _app.Log.Event(LogKind.Speech, tool: "nvda_start");
                return;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
            {
                _app.Log.Failure("nvda_start", e);
                return;
            }
        }

        _app.Log.Error("nvda_not_installed", LogKind.Speech);
    }
}
