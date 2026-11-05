using Mubsir.App.Actions.Interop;

namespace Mubsir.App.Actions;

internal enum AppTargetKind
{
    Shell,

    Website,

    Shortcut,
}

internal sealed record AppTarget(string Display, string Target, AppTargetKind Kind);

internal static class AppAliases
{
    private static readonly (string[] Names, AppTarget Target)[] Table =
    [
        (["chrome", "google chrome", "كروم", "جوجل كروم", "قوقل كروم", "متصفح جوجل"], new("Google Chrome", "chrome.exe", AppTargetKind.Shell)),
        (["edge", "microsoft edge", "إيدج", "ايدج", "مايكروسوفت إيدج", "متصفح إيدج"], new("Microsoft Edge", "msedge.exe", AppTargetKind.Shell)),
        (["firefox", "فايرفوكس", "فاير فوكس"], new("Firefox", "firefox.exe", AppTargetKind.Shell)),
        (["notepad", "المفكرة", "مفكرة", "نوت باد", "النوت باد"], new("Notepad", "notepad.exe", AppTargetKind.Shell)),
        (["word", "microsoft word", "وورد", "ورد", "مايكروسوفت وورد"], new("Microsoft Word", "winword.exe", AppTargetKind.Shell)),
        (["excel", "microsoft excel", "إكسل", "اكسل", "إكسيل"], new("Microsoft Excel", "excel.exe", AppTargetKind.Shell)),
        (["powerpoint", "باوربوينت", "باور بوينت"], new("PowerPoint", "powerpnt.exe", AppTargetKind.Shell)),
        (["outlook", "أوتلوك", "اوتلوك"], new("Outlook", "outlook.exe", AppTargetKind.Shell)),
        (["whatsapp", "واتساب", "واتس اب", "واتس", "الواتس"], new("WhatsApp", @"shell:AppsFolder\5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", AppTargetKind.Shell)),
        (["calculator", "calc", "الآلة الحاسبة", "الحاسبة", "آلة حاسبة", "حاسبة"], new("Calculator", "calc.exe", AppTargetKind.Shell)),
        (["file explorer", "explorer", "files", "الملفات", "مستكشف الملفات", "ملفاتي"], new("File Explorer", "explorer.exe", AppTargetKind.Shell)),
        (["settings", "windows settings", "الإعدادات", "الاعدادات", "إعدادات ويندوز"], new("Settings", "ms-settings:", AppTargetKind.Shell)),
        (["youtube", "يوتيوب", "اليوتيوب"], new("YouTube", "https://www.youtube.com/", AppTargetKind.Website)),
        (["google", "جوجل", "قوقل", "غوغل"], new("Google", "https://www.google.com/", AppTargetKind.Website)),
        (["gmail", "جيميل", "جي ميل"], new("Gmail", "https://mail.google.com/", AppTargetKind.Website)),
    ];

    public static (AppTarget Target, double Score)? Match(string name)
    {
        var best = FuzzyMatch.Best(name, Table, row => row.Names);
        return best is { } b ? (b.Item.Target, b.Score) : null;
    }
}

internal sealed class AppLauncher
{
    private static readonly TimeSpan IndexLifetime = TimeSpan.FromMinutes(5);
    private const int MaxShortcuts = 3000;

    private readonly object _gate = new();
    private List<(string Name, string Path)>? _shortcuts;
    private long _indexedAt;

    public AppTarget? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var alias = AppAliases.Match(name);
        if (alias is { Score: >= 0.99 })
        {
            return alias.Value.Target;
        }

        var shortcut = FuzzyMatch.Best(name, Shortcuts(), s => [s.Name]);
        if (shortcut is { } s && (alias is null || s.Score > alias.Value.Score))
        {
            return new AppTarget(s.Item.Name, s.Item.Path, AppTargetKind.Shortcut);
        }

        return alias?.Target;
    }

    public static string? Launch(AppTarget target)
    {
        if (target.Kind == AppTargetKind.Website
            && !(Uri.TryCreate(target.Target, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
        {
            return "bad_url";
        }

        return ShellOpen(target.Target);
    }

    public static string? ShellOpen(string target, string? arguments = null)
    {
        try
        {
            nint r = StaRunner.Run(() => ActionsNative.ShellExecute(0, "open", target, arguments, null, ActionsNative.SwShowNormal), TimeSpan.FromSeconds(15), (nint)(-1));
            return r > 32 ? null : r == -1 ? "shell_timeout" : "shell_error_" + r;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return "shell_unavailable";
        }
    }

    private List<(string Name, string Path)> Shortcuts()
    {
        lock (_gate)
        {
            if (_shortcuts is not null && Environment.TickCount64 - _indexedAt < IndexLifetime.TotalMilliseconds)
            {
                return _shortcuts;
            }
        }

        var list = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        })
        {
            try
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 4 };
                foreach (var file in Directory.EnumerateFiles(root, "*.lnk", options))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (IsNoise(name) || !seen.Add(name))
                    {
                        continue;
                    }

                    list.Add((name, file));
                    if (list.Count >= MaxShortcuts)
                    {
                        break;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
            }
        }

        lock (_gate)
        {
            _shortcuts = list;
            _indexedAt = Environment.TickCount64;
            return list;
        }
    }

    private static bool IsNoise(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("uninstall", StringComparison.Ordinal) || n.Contains("إلغاء التثبيت", StringComparison.Ordinal)
            || n.Contains("readme", StringComparison.Ordinal) || n.Contains("help", StringComparison.Ordinal)
            || n.Contains("license", StringComparison.Ordinal);
    }
}
