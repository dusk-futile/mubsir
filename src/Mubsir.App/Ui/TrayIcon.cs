using Mubsir.App.Native;
using Mubsir.Core.Common;
using Mubsir.Core.Logging;

namespace Mubsir.App.Ui;

internal sealed record TrayActions(Func<bool> IsPaused, Action<bool> SetPaused, Action OpenSettings, Action SaveReport, Action Exit, Action SessionEnding);

internal sealed unsafe class TrayIcon : IDisposable
{
    private const string ClassName = "MubsirTrayWindow";
    private const uint CallbackMessage = Win32.WM_APP + 20;
    private const uint IconId = 1;
    private const uint CmdPause = 1;
    private const uint CmdSettings = 2;
    private const uint CmdReport = 3;
    private const uint CmdExit = 4;

    private readonly TrayActions _actions;
    private readonly Lang _lang;
    private readonly IEventLog _log;
    private readonly uint _taskbarCreated;
    private nint _hwnd;
    private bool _added;

    public TrayIcon(TrayActions actions, Lang lang, IEventLog log)
    {
        _actions = actions;
        _lang = lang;
        _log = log;
        _taskbarCreated = User32.RegisterWindowMessageW("TaskbarCreated");
    }

    public bool Start()
    {
        _hwnd = NativeWindow.Create(ClassName, "Mubsir", Win32.WS_POPUP, Win32.WS_EX_TOOLWINDOW, 0, 0, 0, 0, 0, OnMessage, out int error);
        if (_hwnd == 0)
        {
            _log.Error("tray_window_" + error);
            return false;
        }

        return AddIcon();
    }

    public void Dispose()
    {
        if (_added)
        {
            NOTIFYICONDATAW data = NewData();
            Shell32.Shell_NotifyIconW(Win32.NIM_DELETE, ref data);
            _added = false;
        }

        NativeWindow.Destroy(_hwnd);
        _hwnd = 0;
    }

    private bool AddIcon()
    {
        NOTIFYICONDATAW data = NewData();
        data.UFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP;
        data.UCallbackMessage = CallbackMessage;
        data.HIcon = User32.LoadIconW(0, Win32.IDI_APPLICATION);
        const string tip = "Mubsir";
        for (int i = 0; i < tip.Length && i < 127; i++)
        {
            data.SzTip[i] = tip[i];
        }

        if (!Shell32.Shell_NotifyIconW(Win32.NIM_ADD, ref data))
        {
            _log.Error("tray_add", LogKind.Error);
            return false;
        }

        data.UTimeoutOrVersion = Win32.NOTIFYICON_VERSION_4;
        Shell32.Shell_NotifyIconW(Win32.NIM_SETVERSION, ref data);
        _added = true;
        return true;
    }

    private NOTIFYICONDATAW NewData() => new()
    {
        CbSize = (uint)sizeof(NOTIFYICONDATAW),
        HWnd = _hwnd,
        UID = IconId,
    };

    private nint? OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            uint ev = (uint)(lParam & 0xFFFF);
            if (ev is Win32.WM_CONTEXTMENU or Win32.WM_RBUTTONUP or Win32.WM_LBUTTONUP)
            {
                ShowMenu(hwnd);
            }

            return 0;
        }

        if (_taskbarCreated != 0 && msg == _taskbarCreated)
        {
            _added = false;
            AddIcon();
            return 0;
        }

        if (msg == Win32.WM_ENDSESSION && wParam != 0)
        {
            _actions.SessionEnding();
            return 0;
        }

        return null;
    }

    private void ShowMenu(nint hwnd)
    {
        nint menu = User32.CreatePopupMenu();
        if (menu == 0)
        {
            _log.Error("tray_menu_" + NativeQueries.LastError());
            return;
        }

        try
        {
            User32.AppendMenuW(menu, Win32.MF_STRING, CmdPause, AppText.Get(_actions.IsPaused() ? AppPhrase.TrayResume : AppPhrase.TrayPause, _lang));
            User32.AppendMenuW(menu, Win32.MF_STRING, CmdSettings, AppText.Get(AppPhrase.TrayOpenSettings, _lang));
            User32.AppendMenuW(menu, Win32.MF_STRING, CmdReport, AppText.Get(AppPhrase.TraySaveReport, _lang));
            User32.AppendMenuW(menu, Win32.MF_SEPARATOR, 0, null);
            User32.AppendMenuW(menu, Win32.MF_STRING, CmdExit, AppText.Get(AppPhrase.TrayExit, _lang));

            User32.GetCursorPos(out POINT pt);

            User32.SetForegroundWindow(hwnd);
            int cmd = User32.TrackPopupMenu(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_NONOTIFY, pt.X, pt.Y, 0, hwnd, 0);
            User32.PostMessageW(hwnd, Win32.WM_NULL, 0, 0);
            Run((uint)cmd);
        }
        finally
        {
            User32.DestroyMenu(menu);
        }
    }

    private void Run(uint cmd)
    {
        try
        {
            switch (cmd)
            {
                case CmdPause:
                    _actions.SetPaused(!_actions.IsPaused());
                    break;
                case CmdSettings:
                    _actions.OpenSettings();
                    break;
                case CmdReport:
                    _actions.SaveReport();
                    break;
                case CmdExit:
                    _actions.Exit();
                    break;
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.Failure("tray_command", e);
        }
    }
}
