using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Mubsir.App.Native;
using Mubsir.Core.Input;
using Mubsir.Core.Logging;

namespace Mubsir.App.Input;

internal sealed unsafe class KeyboardHookService : IDisposable
{
    private const uint WmReinstall = Win32.WM_APP + 1;
    private const uint TickMs = 50;
    private const uint ReinstallMs = 30_000;
    private const uint HeartbeatMs = 1000;

    private static KeyboardHookService? s_instance;

    private readonly InputStateMachine _machine;
    private readonly ChannelWriter<InputAction> _actions;
    private readonly IEventLog _log;
    private readonly HookHeartbeat _heartbeat = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private int _eventsSinceSample;
    private bool _installedOnce;

    public KeyboardHookService(InputStateMachine machine, ChannelWriter<InputAction> actions, IEventLog log)
    {
        _machine = machine;
        _actions = actions;
        _log = log;
    }

    public Action? FastStop { get; set; }

    public bool HeartbeatSuppressed { get; set; }

    public bool IsInstalled => Volatile.Read(ref _hook) != 0;

    public bool Start()
    {
        if (Interlocked.CompareExchange(ref s_instance, this, null) is not null)
        {
            throw new InvalidOperationException("Only one keyboard hook service may run.");
        }

        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "Mubsir keyboard hook",
            Priority = ThreadPriority.Highest,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(3));
        return IsInstalled;
    }

    public void RequestReinstall(HookReinstallReason reason)
    {
        if (_threadId != 0 && !User32.PostThreadMessageW(_threadId, WmReinstall, (nint)reason, 0))
        {
            _log.Error("hook_post_" + NativeQueries.LastError(), LogKind.Hotkey);
        }
    }


    public void Dispose()
    {
        if (_threadId != 0)
        {
            User32.PostThreadMessageW(_threadId, Win32.WM_QUIT, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        Interlocked.CompareExchange(ref s_instance, null, this);
        _ready.Dispose();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode == Win32.HC_ACTION && Volatile.Read(ref s_instance) is { } self)
        {
            try
            {
                if (self.OnHookEvent((uint)wParam, (KBDLLHOOKSTRUCT*)lParam))
                {
                    return 1;
                }
            }
            catch
            {
            }
        }

        return User32.CallNextHookEx(0, nCode, wParam, lParam);
    }

    private bool OnHookEvent(uint message, KBDLLHOOKSTRUCT* data)
    {
        Interlocked.Increment(ref _eventsSinceSample);
        KeyEvent e = KeyEventConverter.FromHook(data->VkCode, data->ScanCode, data->Flags, message, data->Time);
        KeyDecision decision = _machine.OnKey(e);
        if (decision.Actions.Count > 0)
        {
            Publish(decision.Actions);
        }

        return decision.Swallow;
    }

    private void Publish(IReadOnlyList<InputAction> actions)
    {
        foreach (InputAction action in actions)
        {
            if (action is StopSpeech)
            {
                FastStop?.Invoke();
            }

            _actions.TryWrite(action);
        }
    }

    private void ThreadMain()
    {
        nuint tick = 0, reinstall = 0, heartbeat = 0;
        try
        {
            _threadId = Kernel32.GetCurrentThreadId();

            tick = User32.SetTimer(0, 0, TickMs, 0);
            reinstall = User32.SetTimer(0, 0, ReinstallMs, 0);
            heartbeat = User32.SetTimer(0, 0, HeartbeatMs, 0);
            if (tick == 0)
            {
                _log.Error("hook_timer_" + NativeQueries.LastError(), LogKind.Hotkey);
            }

            Reinstall(HookReinstallReason.Startup);
            _ready.Set();

            while (true)
            {
                int r = User32.GetMessageW(out MSG msg, 0, 0, 0);
                if (r == 0)
                {
                    break;
                }

                if (r < 0)
                {
                    _log.Error("hook_getmessage_" + NativeQueries.LastError(), LogKind.Hotkey);
                    Thread.Sleep(50);
                    continue;
                }

                try
                {
                    if (msg.Message == Win32.WM_TIMER && msg.Hwnd == 0)
                    {
                        nuint id = (nuint)msg.WParam;
                        if (id == tick)
                        {
                            IReadOnlyList<InputAction> actions = _machine.OnTick();
                            if (actions.Count > 0)
                            {
                                Publish(actions);
                            }
                        }
                        else if (id == reinstall)
                        {
                            Reinstall(HookReinstallReason.Periodic);
                        }
                        else if (id == heartbeat)
                        {
                            Heartbeat();
                        }

                        continue;
                    }

                    if (msg.Message == WmReinstall && msg.Hwnd == 0)
                    {
                        Reinstall((HookReinstallReason)(int)msg.WParam);
                        continue;
                    }
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    _log.Failure("hook_loop", e);
                }

                User32.TranslateMessage(in msg);
                User32.DispatchMessageW(in msg);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("hook_thread", e);
        }
        finally
        {
            KillTimer(tick);
            KillTimer(reinstall);
            KillTimer(heartbeat);
            nint hook = Interlocked.Exchange(ref _hook, 0);
            if (hook != 0)
            {
                User32.UnhookWindowsHookEx(hook);
            }

            _ready.Set();
        }
    }

    private static void KillTimer(nuint id)
    {
        if (id != 0)
        {
            User32.KillTimer(0, id);
        }
    }

    private void Reinstall(HookReinstallReason reason)
    {
        nint fresh = User32.SetWindowsHookExW(Win32.WH_KEYBOARD_LL, &HookProc, NativeWindow.ModuleHandle, 0);
        if (fresh == 0)
        {
            _log.Error("hook_install_" + NativeQueries.LastError(), LogKind.Hotkey);
            return;
        }

        nint old = Interlocked.Exchange(ref _hook, fresh);
        if (old != 0 && !User32.UnhookWindowsHookEx(old))
        {
            _log.Error("hook_unhook_" + NativeQueries.LastError(), LogKind.Hotkey);
        }

        if (reason != HookReinstallReason.Periodic || !_installedOnce)
        {
            _log.Event(LogKind.Hotkey, tool: "hook_install", errorCode: reason.ToString().ToLowerInvariant());
        }

        _installedOnce = true;
    }

    private void Heartbeat()
    {
        int events = Interlocked.Exchange(ref _eventsSinceSample, 0);
        if (HeartbeatSuppressed)
        {
            _heartbeat.Reset();
            return;
        }

        bool pressed = false;
        for (int vk = 0x08; vk <= 0xFE; vk++)
        {
            short state = User32.GetAsyncKeyState(vk);
            pressed |= _heartbeat.ObserveKey(vk, isDown: (state & 0x8000) != 0, pressedSinceLastQuery: (state & 1) != 0);
        }

        if (_heartbeat.Sample(keysPressed: pressed, hookEvents: events))
        {
            _log.Error("hook_dead", LogKind.Hotkey);
            Reinstall(HookReinstallReason.Dead);
            _machine.ResetKeyState();
        }
    }
}

internal enum HookReinstallReason
{
    Startup,
    Periodic,
    Nvda,
    Dead,
    Unlock,
}

internal static class KeyEventConverter
{
    public static KeyEvent FromHook(uint vkCode, uint scanCode, uint flags, uint message, uint timeMs) =>
        new(
            VirtualKey: (int)vkCode,
            ScanCode: (int)(scanCode & 0xFFFF),
            Extended: (flags & Win32.LLKHF_EXTENDED) != 0,
            IsDown: message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN,
            IsInjected: (flags & (Win32.LLKHF_INJECTED | Win32.LLKHF_LOWER_IL_INJECTED)) != 0,
            TimeMs: timeMs);
}

internal sealed class HookHeartbeat
{
    public const int SuspectSamplesToDeclareDead = 2;

    private readonly bool[] _down = new bool[256];
    private int _suspect;

    public bool ObserveKey(int vk, bool isDown, bool pressedSinceLastQuery)
    {
        bool newPress = pressedSinceLastQuery || (isDown && !_down[vk & 0xFF]);
        _down[vk & 0xFF] = isDown;
        return newPress;
    }

    public bool Sample(bool keysPressed, int hookEvents)
    {
        if (hookEvents > 0 || !keysPressed)
        {
            _suspect = hookEvents > 0 ? 0 : _suspect;
            return false;
        }

        if (++_suspect >= SuspectSamplesToDeclareDead)
        {
            _suspect = 0;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        _suspect = 0;
        Array.Clear(_down);
    }
}
