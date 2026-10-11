using System.Collections.Frozen;
using Mubsir.Core.Context;

namespace Mubsir.Core.Policy;

internal sealed record UrlInfo(string Scheme, string Host, string Path, string? MailAddress);

internal static class PolicyRules
{
    public static readonly FrozenSet<string> BlockedExtensions = new[]
{
        "exe", "msi", "msix", "msixbundle", "appx", "appxbundle", "bat", "cmd", "ps1", "psm1", "psd1", "ps1xml", "psc1",
        "vbs", "vbe", "js", "jse", "wsf", "wsh", "ws", "hta", "scr", "com", "cpl", "reg", "lnk", "jar", "iso", "img",
        "vhd", "vhdx", "pif", "application", "appref-ms", "msp", "mst", "inf", "url", "sct", "chm", "msc", "gadget",
        "settingcontent-ms", "library-ms", "search-ms", "searchconnector-ms", "xll", "scf",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> WebAmbiguousExtensions = new[] { "com", "js" }.ToFrozenSet(StringComparer.Ordinal);

    public static string? ExtensionOf(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        var t = s.Trim().Trim('"', '\'').TrimEnd(' ', '.');
        int slash = t.LastIndexOfAny(['\\', '/']);
        var name = slash >= 0 ? t[(slash + 1)..] : t;
        int dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return null;
        }

        return name[(dot + 1)..].ToLowerInvariant();
    }

    public static bool HasBlockedExtension(string? s) => ExtensionOf(s) is { } ext && BlockedExtensions.Contains(ext);

    public static bool LooksLikeExecutableName(string? s, bool browser)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var t = s.Trim();
        var ext = ExtensionOf(t);
        if (ext is null || !BlockedExtensions.Contains(ext))
        {
            return false;
        }

        if (!browser)
        {
            return true;
        }

        return !WebAmbiguousExtensions.Contains(ext) && !t.Any(char.IsWhiteSpace);
    }

    public static bool TryParseUrl(string? raw, bool allowSchemeless, out UrlInfo url)
    {
        url = new UrlInfo("", "", "", null);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var s = raw.Trim();
        if (s.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return false;
        }

        int colon = s.IndexOf(':');
        bool hasScheme = colon > 0 && char.IsAsciiLetter(s[0])
            && s[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
        if (hasScheme && s[..colon].Contains('.') && colon + 1 < s.Length && char.IsAsciiDigit(s[colon + 1]))
        {
            hasScheme = false;
        }

        if (hasScheme && colon == 1)
        {
            s = "file:///" + s.Replace('\\', '/');
        }
        else if (!hasScheme)
        {
            if (s.StartsWith('\\') || s.StartsWith('/'))
            {
                url = new UrlInfo("file", "", s, null);
                return true;
            }

            if (!allowSchemeless)
            {
                return false;
            }

            s = "https://" + s;
        }

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
        {
            if (hasScheme)
            {
                url = new UrlInfo(s[..colon].ToLowerInvariant(), "", "", null);
                return true;
            }

            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        string host = "";
        try
        {
            host = SensitiveDetector.NormalizeHost(uri.IdnHost);
        }
        catch (UriFormatException)
        {
        }

        string path;
        try
        {
            path = Uri.UnescapeDataString(uri.AbsolutePath);
        }
        catch (Exception e) when (e is UriFormatException or InvalidOperationException)
        {
            path = uri.OriginalString;
        }

        string? mail = null;
        if (scheme == "mailto")
        {
            var rest = s[(s.IndexOf(':') + 1)..];
            int q = rest.IndexOf('?');
            mail = Uri.UnescapeDataString(q >= 0 ? rest[..q] : rest);
        }

        url = new UrlInfo(scheme, host, path, mail);
        return true;
    }

    public static string? LooseScheme(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = new string(raw.Where(c => c is not ('\t' or '\n' or '\r')).ToArray()).Trim().Trim('\0');
        int colon = s.IndexOf(':');
        if (colon < 2 || !char.IsAsciiLetter(s[0]) || !s[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
        {
            return null;
        }

        return s[..colon].ToLowerInvariant();
    }

    private static readonly FrozenSet<string> DeniedSegments = new[]
    {
        "appdata", "startup", "nvda", "mubsir", "$recycle.bin", "system volume information",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DeniedTopFolders = new[]
    {
        "windows", "program files", "program files (x86)", "programdata", "recovery", "boot", "$windows.~bt",
        "$windows.~ws", "windows.old", "perflogs",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim().Trim('"').Trim();
        if (p.Contains('%') || p.Contains('\0') || p.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        p = p.Replace('/', '\\');
        if (p.StartsWith(@"\\?\", StringComparison.Ordinal) || p.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return null;
        }

        bool unc = p.StartsWith(@"\\", StringComparison.Ordinal);
        string prefix;
        string rest;
        if (unc)
        {
            prefix = @"\\";
            rest = p[2..];
        }
        else if (p.Length >= 2 && char.IsAsciiLetter(p[0]) && p[1] == ':')
        {
            if (p.Length > 2 && p[2] != '\\')
            {
                return null;
            }

            prefix = char.ToLowerInvariant(p[0]) + @":\";
            rest = p.Length > 3 ? p[3..] : "";
        }
        else
        {
            return null;
        }

        if (rest.Contains(':'))
        {
            return null;
        }

        var segments = new List<string>();
        foreach (var raw in rest.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw == ".")
            {
                continue;
            }

            if (raw == "..")
            {
                if (segments.Count == 0 || (unc && segments.Count <= 2))
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            var s = raw.TrimEnd(' ', '.');
            if (s.Length == 0 || s.IndexOfAny(['<', '>', '|', '"', '?', '*']) >= 0)
            {
                return null;
            }

            segments.Add(s.ToLowerInvariant());
        }

        if (unc && segments.Count < 2)
        {
            return null;
        }

        return prefix + string.Join('\\', segments);
    }

    public static bool IsPathAllowed(string normalized, IReadOnlyList<string> roots)
    {
        bool inside = false;
        foreach (var root in roots)
        {
            var r = NormalizePath(root);
            if (r is null)
            {
                continue;
            }

            var withSlash = r.EndsWith('\\') ? r : r + "\\";
            if (normalized == r || normalized.StartsWith(withSlash, StringComparison.Ordinal))
            {
                inside = true;
                break;
            }
        }

        if (!inside)
        {
            return false;
        }

        var segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(DeniedSegments.Contains))
        {
            return false;
        }

        bool drive = normalized.Length >= 2 && normalized[1] == ':';
        return !(drive && segments.Length > 1 && DeniedTopFolders.Contains(segments[1]));
    }

    private static readonly FrozenSet<string> ShellProcesses = new[]
    {
        "cmd", "powershell", "pwsh", "powershell_ise", "windowsterminal", "wt", "openconsole", "conhost", "regedit",
        "regedt32", "mshta", "wscript", "cscript", "wsl", "bash", "mmc",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> RunLikeProcesses = new[]
    {
        "searchapp", "searchhost", "searchui", "startmenuexperiencehost",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UntrustedProcesses = new[]
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore", "arc", "msedgewebview2",
        "outlook", "olk", "hxoutlook", "thunderbird", "whatsapp", "whatsapp.root", "telegram", "signal", "discord",
        "slack", "teams", "ms-teams", "skype", "acrobat", "acrord32", "foxitpdfreader", "foxitreader",
        "foxitphantompdf", "sumatrapdf", "pdfxedit", "pdfxcview", "winword", "wordpad", "notepad", "notepad++",
        "soffice", "soffice.bin", "swriter", "excel", "powerpnt", "mspub", "onenote", "kindle", "ebook-viewer",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MessagingProcesses = new[]
    {
        "whatsapp", "whatsapp.root", "telegram", "signal", "outlook", "olk", "hxoutlook", "thunderbird", "teams",
        "ms-teams", "slack", "discord", "skype", "messenger",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly string[] MessagingHosts =
    [
        "web.whatsapp.com", "mail.google.com", "outlook.live.com", "outlook.office.com", "outlook.office365.com",
        "web.telegram.org", "messenger.com", "teams.microsoft.com", "teams.live.com", "discord.com", "app.slack.com",
        "mail.yahoo.com",
    ];

    private static readonly string[] AccountSecurityHosts =
    [
        "accounts.google.com", "myaccount.google.com", "passwords.google.com", "security.google.com",
        "account.microsoft.com", "account.live.com", "login.live.com", "mysignins.microsoft.com",
        "mysecurityinfo.microsoft.com", "appleid.apple.com", "account.apple.com", "iforgot.apple.com",
        "accountscenter.facebook.com", "accountscenter.instagram.com", "accountscenter.meta.com",
    ];

    private static readonly (string Host, string[] Paths)[] AccountSecurityPaths =
    [
        ("facebook.com", ["/settings", "/security", "/privacy"]),
        ("instagram.com", ["/accounts"]),
        ("x.com", ["/settings"]),
        ("twitter.com", ["/settings"]),
    ];

    private static readonly string[] WhatsAppSecurityMarkers = FoldAll(
    [
        "security", "الأمان", "privacy", "الخصوصية", "two-step verification", "التحقق بخطوتين", "linked devices",
        "الأجهزة المرتبطة", "security notifications", "إشعارات الأمان", "passkeys", "مفاتيح المرور", "account",
        "الحساب", "change number", "تغيير الرقم", "delete my account", "حذف حسابي",
    ]);

    public static string? ProcessOf(ScreenContext? s)
    {
        var p = s?.ProcessName?.Trim();
        if (string.IsNullOrEmpty(p))
        {
            return null;
        }

        int slash = p.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            p = p[(slash + 1)..];
        }

        p = p.ToLowerInvariant();
        return p.EndsWith(".exe", StringComparison.Ordinal) ? p[..^4] : p;
    }

    public static string? ShellReason(ScreenContext? s)
    {
        if (s is null)
        {
            return null;
        }

        var proc = ProcessOf(s);
        if (proc is not null && ShellProcesses.Contains(proc))
        {
            return ReasonCodes.Shell;
        }

        var title = PolicyText.Fold(s.WindowTitle);
        if (s.WindowClass == "#32770" && title is "run" or "تشغيل")
        {
            return ReasonCodes.RunDialog;
        }

        if (proc is null && (title.Contains("command prompt") || title.Contains("windows powershell")
            || title.Contains("موجه الاوامر") || title.Contains("regedit") || title.Contains("registry editor")))
        {
            return ReasonCodes.Shell;
        }

        return null;
    }

    public static bool IsRunLike(ScreenContext? s) => ProcessOf(s) is { } p && RunLikeProcesses.Contains(p);

    public static bool IsExplorer(ScreenContext? s) => ProcessOf(s) == "explorer";

    public static bool IsDesktop(ScreenContext? s) => s?.WindowClass is "Progman" or "WorkerW" or "Shell_TrayWnd";

    public static bool IsShutdownDialog(ScreenContext? s)
    {
        var title = PolicyText.Fold(s?.WindowTitle);
        return title.Contains("shut down windows") || title.Contains(PolicyText.Fold("إيقاف تشغيل Windows"));
    }

    public static bool IsUntrustedSource(ScreenContext? s)
    {
        if (s is null)
        {
            return true;
        }

        if (s.IsBrowser || !string.IsNullOrWhiteSpace(s.Domain) || !string.IsNullOrWhiteSpace(s.Url))
        {
            return true;
        }

        if (ProcessOf(s) is { } p && UntrustedProcesses.Contains(p))
        {
            return true;
        }

        var app = PolicyText.Fold(s.AppName);
        return app.Contains("whatsapp") || app.Contains("telegram") || app.Contains("outlook") || app.Contains("mail")
            || app.Contains("thunderbird") || app.Contains("pdf") || app.Contains("acrobat") || app.Contains("word")
            || app.Contains("واتساب") || app.Contains("بريد");
    }

    public static bool IsMessagingContext(ScreenContext? s)
    {
        if (s is null)
        {
            return false;
        }

        if (ProcessOf(s) is { } p && MessagingProcesses.Contains(p))
        {
            return true;
        }

        var host = ContextBuilder.SiteOf(s);
        if (host is null)
        {
            return false;
        }

        if (MessagingHosts.Any(h => HostMatches(host, h)))
        {
            return true;
        }

        var path = PathOf(s);
        return (HostMatches(host, "facebook.com") && path.StartsWith("/messages", StringComparison.Ordinal))
            || (HostMatches(host, "instagram.com") && path.StartsWith("/direct", StringComparison.Ordinal))
            || ((HostMatches(host, "x.com") || HostMatches(host, "twitter.com")) && path.StartsWith("/messages", StringComparison.Ordinal));
    }

    public static bool IsAccountSecurityPage(ScreenContext? s)
    {
        if (s is null)
        {
            return false;
        }

        var host = ContextBuilder.SiteOf(s);
        if (host is not null)
        {
            if (AccountSecurityHosts.Any(h => HostMatches(host, h)))
            {
                return true;
            }

            var path = PathOf(s);
            foreach (var (h, paths) in AccountSecurityPaths)
            {
                if (HostMatches(host, h) && paths.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        bool whatsApp = (host is not null && HostMatches(host, "web.whatsapp.com"))
            || (ProcessOf(s)?.Contains("whatsapp", StringComparison.Ordinal) ?? false)
            || PolicyText.Fold(s.AppName).Contains("whatsapp");
        return whatsApp && WhatsAppSecurityVisible(s);
    }

    private static bool WhatsAppSecurityVisible(ScreenContext s)
    {
        var title = PolicyText.Fold(s.WindowTitle);
        if (WhatsAppSecurityMarkers.Any(m => title.Contains(m, StringComparison.Ordinal)))
        {
            return true;
        }

        foreach (var node in Walk(s.Root, 3000))
        {
            var name = PolicyText.Fold(node.Name);
            if (name.Length == 0)
            {
                continue;
            }

            bool heading = node.Role == "heading";
            if (WhatsAppSecurityMarkers.Any(m => heading ? name.Contains(m, StringComparison.Ordinal) : name == m))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsWindowsUpdateScreen(ScreenContext? s)
    {
        if (s is null)
        {
            return false;
        }

        var proc = ProcessOf(s);
        var app = PolicyText.Fold(s.AppName);
        var title = PolicyText.Fold(s.WindowTitle);
        bool settings = proc is "systemsettings" or "applicationframehost"
            || app is "settings" or "الاعدادات" || title.Contains("settings") || title.Contains("الاعدادات");
        if (!settings)
        {
            return false;
        }

        string[] markers = ["windows update", "تحديث windows", "تحديثات windows"];
        if (markers.Any(m => title.Contains(m, StringComparison.Ordinal)))
        {
            return true;
        }

        return Walk(s.Root, 2000).Any(n => markers.Contains(PolicyText.Fold(n.Name)));
    }

    private static readonly string[] BlockedSettingsKeywords =
    [
        "signin", "yourinfo", "account", "otheruser", "family", "recovery", "defender", "security", "privacy",
        "developer", "workplace", "sync", "assignedaccess", "proxy", "vpn", "insider", "activation", "password",
        "حساب", "تسجيلالدخول", "خصوصيه", "امان", "استرداد", "كلمهالمرور", "العائله",
    ];

    public static bool IsBlockedSettingsPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return false;
        }

        var p = page.Trim().ToLowerInvariant();
        if (p.StartsWith("ms-settings:", StringComparison.Ordinal))
        {
            p = p["ms-settings:".Length..];
        }
        else if (p.Contains(':'))
        {
            return true;
        }

        var compact = PolicyText.Compact(p);
        return BlockedSettingsKeywords.Any(k => compact.Contains(PolicyText.Compact(k), StringComparison.Ordinal));
    }

    public static bool IsWindowsUpdatePage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return false;
        }

        var p = page.Trim().ToLowerInvariant();
        if (p.StartsWith("ms-settings:", StringComparison.Ordinal))
        {
            p = p["ms-settings:".Length..];
        }

        return PolicyText.Compact(p).StartsWith("windowsupdate", StringComparison.Ordinal);
    }

    private static readonly string[] UpdateButtonWords = FoldAll(
    [
        "check for updates", "download", "install", "resume updates", "retry", "try again",
        "التحقق من وجود تحديثات", "تحقق من وجود تحديثات", "البحث عن تحديثات", "تنزيل", "تثبيت", "استئناف التحديثات",
        "إعادة المحاولة",
    ]);

    public static bool IsUpdateButton(string? name)
    {
        var n = PolicyText.Fold(name);
        return n.Length > 0 && UpdateButtonWords.Any(w => n.Contains(w, StringComparison.Ordinal));
    }

    private static readonly FrozenSet<string> UpdateWords = FoldAll(
    [
        "update", "updates", "updating", "upgrade", "تحديث", "تحديثات", "حدث", "حدثلي", "حدثه", "حدثها", "حدّث",
    ]).ToFrozenSet(StringComparer.Ordinal);

    public static bool UserAskedForUpdate(string? utterance) =>
        PolicyText.Tokens(utterance).Any(t => PolicyText.Variants(t).Any(UpdateWords.Contains));

    private static readonly FrozenSet<string> StopWords = FoldAll(
    [
        "the", "a", "an", "to", "of", "and", "or", "in", "on", "for", "with", "my", "your", "me", "this", "that", "is",
        "are", "it", "please", "open", "click", "press", "go", "find", "show", "play", "read", "can", "you", "i",
        "من", "إلى", "الى", "على", "في", "عن", "هذا", "هذه", "ده", "دي", "اللي", "الذي", "التي", "يا", "لو", "افتح",
        "اضغط", "روح", "شغل", "اقرأ", "لي", "عايز", "عاوز", "أريد", "ابغى", "بدي", "ممكن", "انا", "أنا",
    ]).ToFrozenSet(StringComparer.Ordinal);

    private static readonly (string Alias, string[] Labels)[] SiteAliases = FoldAliases(
    [
        ("يوتيوب", ["youtube"]), ("جوجل", ["google"]), ("غوغل", ["google"]), ("قوقل", ["google"]),
        ("فيسبوك", ["facebook"]), ("فيس بوك", ["facebook"]), ("واتساب", ["whatsapp"]), ("واتس", ["whatsapp"]),
        ("جيميل", ["gmail", "google"]), ("تويتر", ["twitter", "x"]), ("انستجرام", ["instagram"]),
        ("انستغرام", ["instagram"]), ("انستا", ["instagram"]), ("ويكيبيديا", ["wikipedia"]), ("أمازون", ["amazon"]),
        ("نتفليكس", ["netflix"]), ("تيك توك", ["tiktok"]), ("تيكتوك", ["tiktok"]), ("لينكد ان", ["linkedin"]),
        ("أوتلوك", ["outlook", "live", "office"]), ("هوتميل", ["hotmail", "live", "outlook"]),
        ("مايكروسوفت", ["microsoft"]), ("ياهو", ["yahoo"]), ("تليجرام", ["telegram"]), ("تلغرام", ["telegram"]),
    ]);

    private static readonly FrozenSet<string> SecondLevelLabels = new[]
    {
        "co", "com", "org", "net", "gov", "edu", "ac", "gob", "mil", "sch",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool NamedInUtterance(string? name, string? utterance)
    {
        var n = PolicyText.Fold(name);
        if (n.Length == 0)
        {
            return false;
        }

        var u = PolicyText.Fold(utterance);
        if (n.Length >= 2 && u.Contains(n, StringComparison.Ordinal))
        {
            return true;
        }

        var nameTokens = Significant(name);
        if (nameTokens.Count == 0)
        {
            return false;
        }

        var utteranceTokens = Significant(utterance);
        int matched = nameTokens.Count(t => utteranceTokens.Any(v => TokensMatch(t, v)));
        return matched >= Math.Max(1, (nameTokens.Count + 1) / 2);
    }

    public static bool FactFromUtterance(string? fact, string? utterance)
    {
        var f = PolicyText.Fold(fact);
        if (f.Length == 0)
        {
            return false;
        }

        if (PolicyText.Fold(utterance).Contains(f, StringComparison.Ordinal))
        {
            return true;
        }

        var factTokens = Significant(fact);
        if (factTokens.Count == 0)
        {
            return false;
        }

        var utteranceTokens = Significant(utterance);
        int matched = factTokens.Count(t => utteranceTokens.Any(v => TokensMatch(t, v)));
        return matched * 10 >= factTokens.Count * 6;
    }

    public static bool IsUserNamedDomain(string host, TaskContext task)
    {
        var h = SensitiveDetector.NormalizeHost(host);
        if (h.Length == 0)
        {
            return false;
        }

        foreach (var d in task.UserNamedDomains)
        {
            var nd = SensitiveDetector.NormalizeHost(d);
            if (nd.Length > 0 && HostMatches(h, nd))
            {
                return true;
            }
        }

        var label = MainLabel(h);
        if (label.Length < 2)
        {
            return false;
        }

        var tokens = PolicyText.Tokens(task.UserUtterance);
        if (label.Length >= 3 && tokens.Contains(label))
        {
            return true;
        }

        var u = PolicyText.Fold(task.UserUtterance);
        return SiteAliases.Any(a => a.Labels.Contains(label) && u.Contains(a.Alias, StringComparison.Ordinal));
    }

    public static bool HostMatches(string host, string domain) =>
        host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    public static string PathOf(ScreenContext? s)
    {
        if (s?.Url is { } url && TryParseUrl(url, allowSchemeless: true, out var u))
        {
            return u.Path.ToLowerInvariant();
        }

        return "";
    }

    public static IEnumerable<UiNode> Walk(UiNode? root, int max)
    {
        if (root is null)
        {
            yield break;
        }

        var stack = new Stack<UiNode>();
        stack.Push(root);
        int seen = 0;
        while (stack.Count > 0 && seen++ < max)
        {
            var n = stack.Pop();
            yield return n;
            for (int i = n.Children.Count - 1; i >= 0; i--)
            {
                if (n.Children[i] is { } c)
                {
                    stack.Push(c);
                }
            }
        }
    }

    private static string MainLabel(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "";
        }

        if (parts.Length == 1)
        {
            return parts[0];
        }

        return parts.Length >= 3 && SecondLevelLabels.Contains(parts[^2]) ? parts[^3] : parts[^2];
    }

    private static List<string> Significant(string? text)
    {
        var list = new List<string>();
        foreach (var t in PolicyText.Tokens(text))
        {
            var stem = PolicyText.Variants(t).OrderBy(v => v.Length).First();
            if (StopWords.Contains(t) || StopWords.Contains(stem) || stem.Length < 2)
            {
                continue;
            }

            list.Add(stem);
        }

        return list;
    }

    private static bool TokensMatch(string a, string b) =>
        a == b || (Math.Min(a.Length, b.Length) >= 4 && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)));

    private static string[] FoldAll(string[] items) => items.Select(PolicyText.Fold).ToArray();

    private static (string, string[])[] FoldAliases((string Alias, string[] Labels)[] items) =>
        items.Select(i => (PolicyText.Fold(i.Alias), i.Labels)).ToArray();
}
