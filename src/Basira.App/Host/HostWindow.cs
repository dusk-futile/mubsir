using Basira.App.Diagnostics;
using Basira.App.Native;
using Basira.Core.Logging;

namespace Basira.App.Host;

/// <summary>
/// The host's message-only window on the main STA thread: receives the always-on stop hotkey (RegisterHotKey) and
/// WTS session notifications.
/// </summary>
internal sealed class HostWindow : IDisposable
{
    private const string ClassName = "BasiraHostWindow";
    private const int StopHotkeyId = 1;

    private readonly IEventLog _log;
    private nint _hwnd;
    private bool _hotkeyRegistered;

    public HostWindow(IEventLog log)
    {
        _log = log;
    }

    public nint Handle => _hwnd;

    /// <summary>The always-on stop combination was pressed.</summary>
    public event Action? StopHotkey;

    /// <summary>WM_WTSSESSION_CHANGE with its WTS_* code.</summary>
    public event Action<int>? SessionChanged;

    public bool Create()
    {
        _hwnd = NativeWindow.Create(ClassName, "Basira", 0, 0, Win32.HWND_MESSAGE, 0, 0, 0, 0, OnMessage, out int error);
        if (_hwnd == 0)
        {
            _log.Error("host_window_" + error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Registers the stop hotkey (default Win+Shift+Escape). Failure (e.g. taken by another app) is logged and the
    /// keyboard hook's own stop keys keep working.
    /// </summary>
    public bool RegisterStopHotkey(uint modifiers = Win32.MOD_WIN | Win32.MOD_SHIFT, uint vk = Win32.VK_ESCAPE)
    {
        if (_hwnd == 0)
        {
            return false;
        }

        _hotkeyRegistered = User32.RegisterHotKey(_hwnd, StopHotkeyId, modifiers | Win32.MOD_NOREPEAT, vk);
        if (!_hotkeyRegistered)
        {
            _log.Error("hotkey_register_" + NativeQueries.LastError(), LogKind.Hotkey);
        }

        return _hotkeyRegistered;
    }

    public void Dispose()
    {
        if (_hotkeyRegistered)
        {
            User32.UnregisterHotKey(_hwnd, StopHotkeyId);
            _hotkeyRegistered = false;
        }

        NativeWindow.Destroy(_hwnd);
        _hwnd = 0;
    }

    private nint? OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Win32.WM_HOTKEY when (int)wParam == StopHotkeyId:
                StopHotkey?.Invoke();
                return 0;
            case Win32.WM_WTSSESSION_CHANGE:
                SessionChanged?.Invoke((int)wParam);
                return 0;
            default:
                return null;
        }
    }
}
