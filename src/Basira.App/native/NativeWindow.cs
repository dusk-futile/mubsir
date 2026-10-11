using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Basira.App.Native;

/// <summary>Handles one window message; null means "not handled" (DefWindowProc runs).</summary>
internal delegate nint? WindowMessageHandler(nint hwnd, uint msg, nint wParam, nint lParam);

/// <summary>
/// Minimal Win32 window plumbing: one shared window procedure that routes messages to managed handlers
/// by window handle. Exceptions never cross back into native code.
/// </summary>
internal static unsafe class NativeWindow
{
    private static readonly ConcurrentDictionary<nint, WindowMessageHandler> Handlers = new();
    private static readonly ConcurrentDictionary<string, bool> RegisteredClasses = new(StringComparer.Ordinal);
    private static readonly object RegisterGate = new();

    /// <summary>Called when a handler throws (the message then gets default processing).</summary>
    public static Action<Exception>? OnHandlerError { get; set; }

    public static nint ModuleHandle { get; } = Kernel32.GetModuleHandleW(null);

    /// <summary>Registers a window class that uses the shared procedure. Returns false (and the Win32 error) on failure.</summary>
    public static bool EnsureClass(string className, out int error)
    {
        error = 0;
        if (RegisteredClasses.ContainsKey(className))
        {
            return true;
        }

        lock (RegisterGate)
        {
            if (RegisteredClasses.ContainsKey(className))
            {
                return true;
            }

            // The class name must stay valid for the life of the process.
            nint name = Marshal.StringToHGlobalUni(className);
            var wc = new WNDCLASSEXW
            {
                CbSize = (uint)sizeof(WNDCLASSEXW),
                LpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
                HInstance = ModuleHandle,
                HbrBackground = 16, // COLOR_BTNFACE + 1
                LpszClassName = name,
            };

            if (User32.RegisterClassExW(in wc) == 0)
            {
                error = Marshal.GetLastPInvokeError();
                const int ErrorClassAlreadyExists = 1410;
                if (error != ErrorClassAlreadyExists)
                {
                    Marshal.FreeHGlobal(name);
                    return false;
                }

                error = 0;
            }

            RegisteredClasses[className] = true;
            return true;
        }
    }

    /// <summary>Creates a window of a registered class and routes its messages to <paramref name="handler"/>.</summary>
    /// <returns>The window handle, or 0 with <paramref name="error"/> set.</returns>
    public static nint Create(
        string className,
        string? title,
        uint style,
        uint exStyle,
        nint parent,
        int x,
        int y,
        int width,
        int height,
        WindowMessageHandler? handler,
        out int error)
    {
        error = 0;
        if (!EnsureClass(className, out error))
        {
            return 0;
        }

        nint hwnd = User32.CreateWindowExW(exStyle, className, title, style, x, y, width, height, parent, 0, ModuleHandle, 0);
        if (hwnd == 0)
        {
            error = Marshal.GetLastPInvokeError();
            return 0;
        }

        if (handler is not null)
        {
            Handlers[hwnd] = handler;
        }

        return hwnd;
    }

    /// <summary>Creates a standard child control (EDIT, STATIC) inside <paramref name="parent"/>.</summary>
    public static nint CreateControl(string systemClass, string? text, uint style, nint parent, int x, int y, int width, int height, int id, out int error)
    {
        error = 0;
        nint hwnd = User32.CreateWindowExW(0, systemClass, text, style, x, y, width, height, parent, id, ModuleHandle, 0);
        if (hwnd == 0)
        {
            error = Marshal.GetLastPInvokeError();
        }

        return hwnd;
    }

    /// <summary>Destroys the window and forgets its handler. Must run on the window's thread.</summary>
    public static void Destroy(nint hwnd)
    {
        if (hwnd == 0)
        {
            return;
        }

        User32.DestroyWindow(hwnd);
        Handlers.TryRemove(hwnd, out _);
    }

    public static void Forget(nint hwnd) => Handlers.TryRemove(hwnd, out _);

    /// <summary>
    /// Pumps messages on the calling thread until WM_QUIT. <paramref name="preprocess"/> sees each message first and
    /// returns true when it consumed it.
    /// </summary>
    /// <returns>The WM_QUIT exit code, or -1 when GetMessage failed.</returns>
    public static int RunMessageLoop(Func<MSG, bool>? preprocess = null)
    {
        while (true)
        {
            int r = User32.GetMessageW(out MSG msg, 0, 0, 0);
            if (r == 0)
            {
                return (int)msg.WParam;
            }

            if (r < 0)
            {
                return -1;
            }

            bool consumed = false;
            if (preprocess is not null)
            {
                try
                {
                    consumed = preprocess(msg);
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    OnHandlerError?.Invoke(e);
                }
            }

            if (!consumed)
            {
                User32.TranslateMessage(in msg);
                User32.DispatchMessageW(in msg);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (Handlers.TryGetValue(hwnd, out WindowMessageHandler? handler))
            {
                nint? handled = handler(hwnd, msg, wParam, lParam);
                if (handled is nint result)
                {
                    return result;
                }
            }

            if (msg == Win32.WM_DESTROY)
            {
                Handlers.TryRemove(hwnd, out _);
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

        return User32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
