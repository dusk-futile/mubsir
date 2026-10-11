using System.Globalization;
using Mubsir.Core.Common;

namespace Mubsir.App.Orchestration;

internal enum AppPhrase
{
    VoiceNeedsInternet,
    TypingUnavailable,
    CannotHelp,
    ReplacingTask,
    FeatureUnavailable,
    NvdaSpeedHint,
    ConsentSummary,
    ConsentGiven,
    ConsentDeclined,
    ConsentPressEnter,
    SayRequestAgain,
    BankOfferLocal,
    ReportIncludes,
    ReportFailed,
    NoWindows,
    AlreadyRunning,
    AnswerYesOrNo,
    RequestBoxTitle,
    RequestBoxLabel,
    ActionFailed,
    SpeedAtLimit,

    SettingsSummary,
    VerbosityBrief,
    VerbosityNormal,
    VerbosityDetailed,
    On,
    Off,
    PrivateModeAlreadyOn,
    TrayPause,
    TrayResume,
    TrayOpenSettings,
    TraySaveReport,
    TrayExit,
}

internal static class AppText
{
    public const string SupervisorGaveUpArabic = "مُبصِر توقف عن العمل. اضغط Ctrl+Alt+N لإعادة تشغيل NVDA أو اطلب المساعدة.";

    private static readonly Dictionary<AppPhrase, (string Ar, string En)> Table = new()
    {
        [AppPhrase.VoiceNeedsInternet] = (
            "الوضع الخاص مفعّل، والكلام بالصوت يحتاج إلى الإنترنت. المفاتيح والقراءة المحلية ما زالت تعمل. "
            + "لإيقاف الوضع الخاص اضغط Control الأيمن مرتين ثم إدخال، واكتب: أوقف الوضع الخاص.",
            "Private mode is on, and voice requests need the internet. The keys and local reading still work. "
            + "To turn private mode off, press Right Control twice, then Enter, and type: private mode off."),
        [AppPhrase.TypingUnavailable] = ("لا يمكن فتح مربع الكتابة الآن.", "Typing a request isn't available right now."),
        [AppPhrase.CannotHelp] = ("لا أستطيع المساعدة في هذا الطلب.", "I can't help with that one."),
        [AppPhrase.ReplacingTask] = ("أوقفت المهمة السابقة، وأبدأ طلبك الجديد.", "I stopped the previous task and I'm starting your new request."),
        [AppPhrase.FeatureUnavailable] = ("هذه الميزة غير متاحة الآن.", "This feature isn't available right now."),
        [AppPhrase.NvdaSpeedHint] = (
            "لتغيير سرعة NVDA نفسه اضغط NVDA مع Control مع السهم لأعلى أو لأسفل.",
            "To change NVDA's own speed, press NVDA+Control+Up or Down arrow."),
        [AppPhrase.ConsentSummary] = (
            "قبل أن أستخدم الذكاء الاصطناعي، هذا ملخص الخصوصية. "
            + "عندما تضغط مفتاح التحدث يذهب صوتك، مع اسم البرنامج واسم العنصر المحدد، إلى خدمة Gemini من Google لتحويله إلى نص. "
            + "ويذهب طلبك، مع نص الشاشة اللازم، إلى Claude من Anthropic ليجيب أو ينفذ. "
            + "كلمات المرور وأرقام البطاقات ورموز التحقق لا تخرج من جهازك أبدًا، وفي صفحات البنوك يتوقف الذكاء الاصطناعي. "
            + "ويمكنك أن تقول: الوضع الخاص، فيتوقف كل إرسال إلى الإنترنت. "
            + "اضغط إدخال للموافقة، أو Escape ليبقى الذكاء الاصطناعي متوقفًا.",
            "Before I use the AI, here is the privacy summary. "
            + "When you hold the talk key, your voice, with the app name and the focused item's name, goes to Google's Gemini to turn it into text. "
            + "Your request, with the screen text it needs, goes to Anthropic's Claude to answer or act. "
            + "Passwords, card numbers and verification codes never leave this computer, and the AI is off on bank pages. "
            + "You can say: private mode, to stop everything going to the internet. "
            + "Press Enter to agree, or Escape to keep the AI off."),
        [AppPhrase.ConsentGiven] = ("شكرًا. الذكاء الاصطناعي جاهز.", "Thank you. The AI is ready."),
        [AppPhrase.ConsentDeclined] = (
            "سيبقى الذكاء الاصطناعي متوقفًا. القراءة المحلية والمفاتيح تعمل، ويمكنك الموافقة لاحقًا.",
            "The AI stays off. Local reading and the keys work, and you can agree later."),
        [AppPhrase.ConsentPressEnter] = (
            "لم ترسل شيئًا بعد. اضغط إدخال للموافقة على استخدام الذكاء الاصطناعي، أو Escape للرفض.",
            "Nothing was sent. Press Enter to agree to using the AI, or Escape to decline."),
        [AppPhrase.SayRequestAgain] = ("قل طلبك مرة أخرى.", "Please say your request again."),
        [AppPhrase.BankOfferLocal] = (
            "أستطيع قراءة الصفحة على جهازك فقط. قل: اقرأ الشاشة.",
            "I can read this page on this computer only. Say: read the screen."),
        [AppPhrase.ReportIncludes] = (
            "يحتوي التقرير على سجلات بلا أي نصوص، والإعدادات بلا مفاتيح، وملخصًا قصيرًا عن الجهاز.",
            "The report has logs with no text in them, the settings without keys, and a short summary of the computer."),
        [AppPhrase.ReportFailed] = ("لم أستطع حفظ التقرير.", "I couldn't save the report."),
        [AppPhrase.NoWindows] = ("لم أجد نوافذ مفتوحة.", "I didn't find any open windows."),
        [AppPhrase.AlreadyRunning] = ("مُبصِر يعمل بالفعل.", "Mubsir is already running."),
        [AppPhrase.AnswerYesOrNo] = ("قل نعم أو لا، أو اضغط إدخال أو Escape.", "Say yes or no, or press Enter or Escape."),
        [AppPhrase.RequestBoxTitle] = ("مُبصِر: اكتب طلبك", "Mubsir: type your request"),
        [AppPhrase.RequestBoxLabel] = (
            "اكتب طلبك ثم اضغط إدخال، أو Escape للإلغاء",
            "Type your request, then press Enter, or Escape to cancel"),
        [AppPhrase.ActionFailed] = ("لم أنجح في ذلك.", "That didn't work."),
        [AppPhrase.SpeedAtLimit] = ("هذه أقصى سرعة متاحة.", "That's as far as the speed goes."),
        [AppPhrase.SettingsSummary] = (
            "الإعدادات: سرعة الكلام {0} من مئة، ومستوى التفصيل {1}، ومفتاح التحدث {2}، "
            + "واستخدمت هذا الشهر {3} دولار من {4}، والوضع الخاص {5}. "
            + "لتغيير السرعة قل: أسرع أو أبطأ. للوضع الخاص قل: الوضع الخاص أو أوقف الوضع الخاص. للإيقاف المؤقت قل: توقف مؤقتًا. "
            + "ويمكن تغيير مستوى التفصيل ومفتاح التحدث والميزانية من أيقونة مُبصِر بجانب الساعة.",
            "Settings: speech speed {0} of 100, detail level {1}, talk key {2}, "
            + "this month {3} of {4} dollars used, private mode {5}. "
            + "To change the speed say faster or slower. For private mode say private mode on or off. To pause say pause. "
            + "Detail level, talk key and budget can be changed from the Mubsir icon next to the clock."),
        [AppPhrase.VerbosityBrief] = ("مختصر", "brief"),
        [AppPhrase.VerbosityNormal] = ("عادي", "normal"),
        [AppPhrase.VerbosityDetailed] = ("مفصّل", "detailed"),
        [AppPhrase.On] = ("مفعّل", "on"),
        [AppPhrase.Off] = ("متوقف", "off"),
        [AppPhrase.PrivateModeAlreadyOn] = ("الوضع الخاص مفعّل بالفعل.", "Private mode is already on."),
        [AppPhrase.TrayPause] = ("إيقاف مؤقت / Pause", "Pause / إيقاف مؤقت"),
        [AppPhrase.TrayResume] = ("متابعة / Resume", "Resume / متابعة"),
        [AppPhrase.TrayOpenSettings] = ("فتح ملف الإعدادات / Open settings file", "Open settings file / فتح ملف الإعدادات"),
        [AppPhrase.TraySaveReport] = ("حفظ تقرير / Save report", "Save report / حفظ تقرير"),
        [AppPhrase.TrayExit] = ("خروج / Exit", "Exit / خروج"),
    };

    public static string Get(AppPhrase phrase, Lang lang, params object[] args)
    {
        if (!Table.TryGetValue(phrase, out (string Ar, string En) entry))
        {
            return string.Empty;
        }

        string template = lang == Lang.Arabic ? entry.Ar : entry.En;
        if (args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public static bool Has(AppPhrase phrase) => Table.ContainsKey(phrase);

    public static AppPhrase VerbosityName(Verbosity v) => v switch
    {
        Verbosity.Detailed => AppPhrase.VerbosityDetailed,
        Verbosity.Normal => AppPhrase.VerbosityNormal,
        _ => AppPhrase.VerbosityBrief,
    };
}
