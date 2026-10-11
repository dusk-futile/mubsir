using System.Diagnostics;
using Basira.App.Abstractions;
using Basira.App.Diagnostics;
using Basira.App.Orchestration;
using Basira.Core.Common;
using Basira.Core.Logging;

namespace Basira.App.Host;

/// <summary>
/// --watch: runs Basira as a child process and restarts it after a crash (non-zero exit) with backoff
/// 1, 2, 5, 10, 30 s. Exit code 0 means an intentional exit and ends the supervisor. After 5 crashes within
/// 10 minutes it says once that Basira stopped (through NVDA, or OneCore) and gives up.
/// </summary>
internal static class Supervisor
{
    private const string MutexName = SingleInstance.MutexName + "-watch";

    public static int Run(IReadOnlyList<string> childArgs)
    {
        AppPaths.EnsureCreated();
        using var log = new FileEventLog(AppPaths.LogsDir, SystemClock.Instance, "watch");
        using var single = SingleInstance.TryAcquire(MutexName);
        if (single is null)
        {
            return 0; // another supervisor already watches Basira
        }

        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            log.Error("watch_no_exe");
            return 1;
        }

        var policy = new CrashPolicy();
        bool recovered = false;
        while (true)
        {
            long started = Environment.TickCount64;
            int code = RunChild(exe, childArgs, recovered, log);
            if (code == 0)
            {
                log.Event(LogKind.Shutdown, tool: "watch_child_exit");
                return 0;
            }

            long now = Environment.TickCount64;
            log.Write(new LogEvent(DateTimeOffset.UtcNow, LogKind.Error, LogOutcome.Failed, Tool: "watch_child_crash", LatencyMs: now - started, ErrorCode: "exit_" + code));
            CrashDecision decision = policy.OnCrash(now, now - started);
            if (decision.GiveUp)
            {
                log.Error("watch_gave_up");
                EmergencyVoice.Speak(AppText.SupervisorGaveUpArabic, Lang.Arabic, log, TimeSpan.FromSeconds(20));
                return code;
            }

            Thread.Sleep(decision.Delay);
            recovered = true;
        }
    }

    private static int RunChild(string exe, IReadOnlyList<string> args, bool recovered, IEventLog log)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (string a in args)
        {
            psi.ArgumentList.Add(a);
        }

        if (recovered)
        {
            psi.ArgumentList.Add(CommandLine.RecoveredFlag);
        }

        try
        {
            using Process? child = Process.Start(psi);
            if (child is null)
            {
                log.Error("watch_start_null");
                return -1;
            }

            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            log.Failure("watch_start", e);
            return -1;
        }
    }
}

internal readonly record struct CrashDecision(bool GiveUp, TimeSpan Delay);

/// <summary>Restart backoff and the give-up rule of the supervisor (pure, testable).</summary>
internal sealed class CrashPolicy
{
    public const int MaxCrashesInWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly Queue<long> _crashes = new();
    private int _consecutive;

    /// <param name="nowMs">Monotonic time of the crash.</param>
    /// <param name="ranForMs">How long the crashed run lasted; a long healthy run resets the backoff.</param>
    public CrashDecision OnCrash(long nowMs, long ranForMs)
    {
        _crashes.Enqueue(nowMs);
        while (_crashes.Count > 0 && nowMs - _crashes.Peek() > (long)Window.TotalMilliseconds)
        {
            _crashes.Dequeue();
        }

        if (_crashes.Count >= MaxCrashesInWindow)
        {
            return new CrashDecision(true, TimeSpan.Zero);
        }

        if (ranForMs > (long)Window.TotalMilliseconds)
        {
            _consecutive = 0;
        }

        TimeSpan delay = Backoff[Math.Min(_consecutive, Backoff.Length - 1)];
        _consecutive++;
        return new CrashDecision(false, delay);
    }
}
