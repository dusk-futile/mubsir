using Basira.App.Screen;
using Basira.Core.Brain;
using Basira.Core.Common;
using Basira.Core.Policy;

namespace Basira.App.Actions;

/// <summary>One short spoken phrase per step (simple MSA / English), with the target's name when it helps. Pure.</summary>
internal static class ActionNarration
{
    public static string For(AgentAction action, Lang lang)
    {
        bool ar = lang == Lang.Arabic;
        var name = ScreenText.Short(action.Target?.Name, 40);
        string Q(string s) => ar ? $"«{s}»" : $"\"{s}\"";
        string? Arg(string key) => action.Args.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        switch (action.Tool)
        {
            case ToolNames.ReadScreen:
                return ar ? "أقرأ الشاشة" : "Reading the screen";
            case ToolNames.Invoke:
                if (action.Target is { } t)
                {
                    var role = ScreenText.RoleName(t, lang);
                    return name is null ? (ar ? $"أضغط {role}" : $"Pressing the {role}") : (ar ? $"أضغط {role} {Q(name)}" : $"Pressing {role} {Q(name)}");
                }

                return ar ? "أضغط" : "Pressing";
            case ToolNames.SetText:
                return name is null ? (ar ? "أكتب في الحقل" : "Typing in the field") : (ar ? $"أكتب في {Q(name)}" : $"Typing in {Q(name)}");
            case ToolNames.TypeText:
                return ar ? "أكتب" : "Typing";
            case ToolNames.PressKeys:
                return ar ? $"أضغط {KeyName(action.KeyChord, lang)}" : $"Pressing {KeyName(action.KeyChord, lang)}";
            case ToolNames.Scroll:
                return (Arg("direction"), Arg("amount")) switch
                {
                    (_, "top") => ar ? "أذهب إلى الأعلى" : "Going to the top",
                    (_, "bottom") => ar ? "أذهب إلى الأسفل" : "Going to the bottom",
                    ("up", _) => ar ? "أصعد للأعلى" : "Scrolling up",
                    ("left", _) => ar ? "أمرر لليسار" : "Scrolling left",
                    ("right", _) => ar ? "أمرر لليمين" : "Scrolling right",
                    _ => ar ? "أنزل للأسفل" : "Scrolling down",
                };
            case ToolNames.Focus:
                return name is null ? (ar ? "أنقل التركيز" : "Moving focus") : (ar ? $"أنتقل إلى {Q(name)}" : $"Moving to {Q(name)}");
            case ToolNames.ListWindows:
                return ar ? "أعدّ النوافذ المفتوحة" : "Listing open windows";
            case ToolNames.SwitchTo:
                return Arg("window") is { } w ? (ar ? $"أنتقل إلى {Q(Short(w))}" : $"Switching to {Q(Short(w))}") : (ar ? "أنتقل إلى النافذة" : "Switching window");
            case ToolNames.OpenApp:
                return Arg("name") is { } app ? (ar ? $"أفتح {Short(app)}" : $"Opening {Short(app)}") : (ar ? "أفتح البرنامج" : "Opening the app");
            case ToolNames.OpenUrl:
                return action.Url is { } u && Uri.TryCreate(u, UriKind.Absolute, out var uri)
                    ? (ar ? $"أفتح {uri.Host}" : $"Opening {uri.Host}")
                    : (ar ? "أفتح الموقع" : "Opening the site");
            case ToolNames.BrowseSearch:
                var engine = Arg("engine") == "youtube" ? (ar ? "يوتيوب" : "YouTube") : (ar ? "جوجل" : "Google");
                return Arg("query") is { } q ? (ar ? $"أفتح {engine} وأبحث عن {Q(Short(q))}" : $"Opening {engine}, searching for {Q(Short(q))}") : (ar ? $"أفتح {engine}" : $"Opening {engine}");
            case ToolNames.DescribeScreen:
                return ar ? "ألتقط صورة للشاشة وأصفها" : "Taking a picture of the screen to describe it";
            case ToolNames.OcrWindow:
                return ar ? "أقرأ النص من صورة الشاشة" : "Reading the text from the screen image";
            case ToolNames.ReadDocument:
                return ar ? "أقرأ المستند" : "Reading the document";
            case ToolNames.Media:
                return Arg("action") switch
                {
                    "next" => ar ? "التالي" : "Next",
                    "previous" => ar ? "السابق" : "Previous",
                    "seek_forward" => ar ? "تقديم" : "Skipping ahead",
                    "seek_back" => ar ? "ترجيع" : "Going back",
                    _ => ar ? "تشغيل أو إيقاف" : "Play or pause",
                };
            case ToolNames.ListFolder:
                return ar ? "أعرض محتويات المجلد" : "Listing the folder";
            case ToolNames.FindFiles:
                return ar ? "أبحث عن الملفات" : "Searching for files";
            case ToolNames.MoveFile:
                return ar ? "أنقل الملف" : "Moving the file";
            case ToolNames.CopyFile:
                return ar ? "أنسخ الملف" : "Copying the file";
            case ToolNames.RenameFile:
                return ar ? "أغيّر اسم الملف" : "Renaming the file";
            case ToolNames.Recycle:
                return ar ? "أنقل إلى سلة المحذوفات" : "Moving to the Recycle Bin";
            case ToolNames.SystemStatus:
                return ar ? "أفحص حالة الجهاز" : "Checking the computer";
            case ToolNames.SetVolume:
                return ar ? "أضبط الصوت" : "Setting the volume";
            case ToolNames.OpenSettings:
                return ar ? "أفتح الإعدادات" : "Opening Settings";
            case ToolNames.WindowsUpdateStatus:
                return ar ? "أفحص تحديثات ويندوز، قد يستغرق ذلك دقيقة" : "Checking Windows Update; this can take a minute";
            case ToolNames.RestartPc:
                return ar ? "سيُعاد تشغيل الجهاز بعد ثلاثين ثانية" : "The computer restarts in thirty seconds";
            case ToolNames.Remember:
                return ar ? "سأتذكر ذلك" : "I'll remember that";
            case ToolNames.AskUser:
                return "";
            default:
                return "";
        }
    }

    /// <summary>Spoken key names: "Enter" → "إدخال", "ctrl+t" stays as is.</summary>
    public static string KeyName(string? chord, Lang lang)
    {
        if (string.IsNullOrWhiteSpace(chord))
        {
            return lang == Lang.Arabic ? "مفتاحاً" : "a key";
        }

        if (lang != Lang.Arabic)
        {
            return chord;
        }

        return chord switch
        {
            "enter" => "إدخال",
            "escape" => "هروب",
            "tab" => "تاب",
            "shift+tab" => "شفت تاب",
            "space" => "مسافة",
            "backspace" => "حذف للخلف",
            "delete" => "حذف",
            "pagedown" => "صفحة للأسفل",
            "pageup" => "صفحة للأعلى",
            "up" => "السهم للأعلى",
            "down" => "السهم للأسفل",
            "left" => "السهم الأيسر",
            "right" => "السهم الأيمن",
            "home" => "بداية",
            "end" => "نهاية",
            _ => chord,
        };
    }

    private static string Short(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
