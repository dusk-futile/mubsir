using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Mubsir.App.Actions.Interop;

namespace Mubsir.App.Actions;

internal static unsafe class SystemTools
{
    private static readonly TimeSpan UpdateSearchTimeout = TimeSpan.FromSeconds(90);

    public static string Battery()
    {
        if (!ActionsNative.GetSystemPowerStatus(out var s))
        {
            return "Battery: unknown.";
        }

        if (s.BatteryFlag == 128)
        {
            return "Battery: this PC has no battery; it runs on mains power.";
        }

        var sb = new StringBuilder("Battery: ");
        sb.Append(s.BatteryLifePercent <= 100 ? $"{s.BatteryLifePercent}%" : "unknown level");
        sb.Append(s.AcLineStatus switch
        {
            1 => (s.BatteryFlag & 8) != 0 ? ", charging." : ", plugged in.",
            0 => ", on battery",
            _ => ".",
        });
        if (s.AcLineStatus == 0)
        {
            sb.Append(s.BatteryLifeTime != uint.MaxValue ? $", about {TimeSpan.FromSeconds(s.BatteryLifeTime).TotalMinutes:0} minutes left." : ".");
        }

        return sb.ToString();
    }

    public static string Network()
    {
        try
        {
            var up = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                .ToList();
            bool wifi = up.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            bool wired = up.Any(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet);
            if (up.Count == 0 || !NetworkInterface.GetIsNetworkAvailable())
            {
                return "Network: not connected.";
            }

            return wifi ? "Network: connected by Wi-Fi." : wired ? "Network: connected by cable." : "Network: connected.";
        }
        catch (NetworkInformationException)
        {
            return "Network: unknown.";
        }
    }

    public static string Volume()
    {
        var v = SystemVolume.Get();
        return v is { } x ? $"Volume: {x.Level}%{(x.Muted ? ", muted" : "")}." : "Volume: unknown.";
    }

    public static string Time()
    {
        var now = DateTime.Now;
        return "Time: " + now.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture) + ".";
    }

    public static Task<string> WindowsUpdateAsync(CancellationToken ct) =>
    StaRunner.RunAsync(WindowsUpdate, UpdateSearchTimeout, "Windows Update: the check took too long; try again later or open Settings, Windows Update.", ct);

    public static string? Recycle(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return "no_paths";
        }

        var from = string.Join('\0', paths) + "\0\0";
        return StaRunner.Run(
            () =>
            {
                fixed (char* p = from)
                {
                    var op = new ActionsNative.ShFileOpStruct
                    {
                        Func = ActionsNative.FoDelete,
                        From = p,
                        Flags = (ushort)(ActionsNative.FofAllowUndo | ActionsNative.FofNoConfirmation | ActionsNative.FofSilent | ActionsNative.FofNoErrorUi),
                    };
                    int rc = ActionsNative.ShFileOperation(&op);
                    if (rc != 0)
                    {
                        return "shfileop_" + rc.ToString("x", CultureInfo.InvariantCulture);
                    }

                    return op.AnyOperationsAborted != 0 ? "aborted" : null;
                }
            },
            TimeSpan.FromSeconds(60),
            "timeout");
    }

    public static string? Restart()
    {
        try
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in new[] { "/r", "/t", "30", "/c", "Mubsir: restarting in 30 seconds as you asked." })
            {
                psi.ArgumentList.Add(a);
            }

            using var p = Process.Start(psi);
            return p is null ? "not_started" : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return "not_started";
        }
    }

    private static string WindowsUpdate()
    {
        object? session = null;
        try
        {
            var sb = new StringBuilder("Windows Update: ");
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType is null)
            {
                return "Windows Update: not available on this PC.";
            }

            session = Activator.CreateInstance(sessionType);
            var searcher = Call(session, "CreateUpdateSearcher");
            var result = Call(searcher, "Search", "IsInstalled=0 and IsHidden=0");
            var updates = Get(result, "Updates");
            int count = Convert.ToInt32(Get(updates, "Count"), CultureInfo.InvariantCulture);
            if (count == 0)
            {
                sb.Append("no pending updates.");
            }
            else
            {
                sb.Append(count).Append(count == 1 ? " update is" : " updates are").Append(" available: ");
                var titles = new List<string>();
                for (int i = 0; i < Math.Min(5, count); i++)
                {
                    var item = GetIndexed(updates, i);
                    if (Get(item, "Title") is string t && t.Length > 0)
                    {
                        titles.Add(t.Length > 120 ? t[..120] : t);
                    }
                }

                sb.Append(string.Join("; ", titles)).Append('.');
            }

            sb.Append(RebootRequired() ? " A restart is needed to finish installing updates." : " No restart is needed.");
            return sb.ToString();
        }
        catch (Exception e) when (e is COMException or TargetInvocationException or InvalidCastException or MissingMethodException or FormatException)
        {
            return "Windows Update: could not check (" + (e is COMException c ? "0x" + ((uint)c.HResult).ToString("x8", CultureInfo.InvariantCulture) : e.GetType().Name) + "). Open Settings, Windows Update, to check by hand.";
        }
        finally
        {
            if (session is not null && Marshal.IsComObject(session))
            {
                Marshal.ReleaseComObject(session);
            }
        }
    }

    private static bool RebootRequired()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Microsoft.Update.SystemInfo");
            if (type is null)
            {
                return false;
            }

            var info = Activator.CreateInstance(type);
            return Get(info, "RebootRequired") is true;
        }
        catch (Exception e) when (e is COMException or TargetInvocationException or MissingMethodException)
        {
            return false;
        }
    }

    private static object? Call(object? target, string method, params object?[] args) =>
        target?.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args, CultureInfo.InvariantCulture);

    private static object? Get(object? target, string property) =>
        target?.GetType().InvokeMember(property, BindingFlags.GetProperty, null, target, null, CultureInfo.InvariantCulture);

    private static object? GetIndexed(object? collection, int index) =>
        collection?.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, collection, [index], CultureInfo.InvariantCulture);
}
