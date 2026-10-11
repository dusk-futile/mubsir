using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Basira.App.Native;

/// <summary>
/// Out-of-context WinEvent hooks routed to managed handlers. The thread that installs a hook must pump messages
/// (Windows delivers the events through that thread's queue); Basira installs them on the main STA thread.
/// </summary>
internal sealed unsafe class WinEventHook : IDisposable
{
    private static readonly ConcurrentDictionary<nint, Action<uint, nint, int, int>> Handlers = new();

    private nint _hook;

    private WinEventHook(nint hook)
    {
        _hook = hook;
    }

    /// <summary>Called when a handler throws.</summary>
    public static Action<Exception>? OnHandlerError { get; set; }

    /// <summary>Installs a hook for <paramref name="eventMin"/>..<paramref name="eventMax"/>; null (with the Win32 error) on failure.</summary>
    /// <param name="handler">Receives (event, hwnd, idObject, idChild) on the installing thread.</param>
    public static WinEventHook? Install(uint eventMin, uint eventMax, Action<uint, nint, int, int> handler, out int error)
    {
        nint hook = User32.SetWinEventHook(
            eventMin,
            eventMax,
            0,
            &Callback,
            0,
            0,
            Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
        if (hook == 0)
        {
            error = Marshal.GetLastPInvokeError();
            return null;
        }

        error = 0;
        Handlers[hook] = handler;
        return new WinEventHook(hook);
    }

    /// <summary>Must run on the installing thread.</summary>
    public void Dispose()
    {
        nint hook = Interlocked.Exchange(ref _hook, 0);
        if (hook != 0)
        {
            Handlers.TryRemove(hook, out _);
            User32.UnhookWinEvent(hook);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void Callback(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint threadId, uint timeMs)
    {
        try
        {
            if (Handlers.TryGetValue(hook, out Action<uint, nint, int, int>? handler))
            {
                handler(eventType, hwnd, idObject, idChild);
            }
        }
        catch (Exception e)
        {
            try
            {
                OnHandlerError?.Invoke(e);
            }
            catch
            {
                // Nothing may escape into native code.
            }
        }
    }
}
