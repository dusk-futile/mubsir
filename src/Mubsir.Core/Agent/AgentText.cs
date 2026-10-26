using Mubsir.Core.Common;
using Mubsir.Core.Policy;

namespace Mubsir.Core.Agent;

public static class AgentText
{
    public static string Blocked(string reasonCode, Lang lang)
    {
        bool ar = lang == Lang.Arabic;
        return reasonCode switch
        {
            ReasonCodes.Shell or ReasonCodes.RunDialog => ar
                ? "لا أكتب في نوافذ الأوامر أو نافذة التشغيل."
                : "I don't type into command windows or the Run box.",
            ReasonCodes.Keys => ar
                ? "لن أضغط هذا الاختصار حفاظًا على أمانك."
                : "I won't press that shortcut, for your safety.",
            ReasonCodes.UrlScheme => ar
                ? "لا أفتح هذا النوع من الروابط."
                : "I don't open that kind of link.",
            ReasonCodes.Executable => ar
                ? "لا أفتح البرامج أو ملفات التثبيت أو ملفات الأوامر."
                : "I don't open programs, installers or script files.",
            ReasonCodes.Path => ar
                ? "هذا المكان خارج مجلداتك المسموح بها."
                : "That location is outside your allowed folders.",
            ReasonCodes.Settings or ReasonCodes.AccountSecurity => ar
                ? "لا أغيّر إعدادات الأمان أو الحساب. يمكنك أن تفعل ذلك بنفسك."
                : "I don't change security or account settings. You can do that yourself.",
            ReasonCodes.PasswordField or ReasonCodes.SensitiveText => ar
                ? "لا أكتب كلمات المرور أو الأرقام السرية. اكتبها بنفسك."
                : "I don't type passwords or secret numbers. Please type them yourself.",
            ReasonCodes.Elevated => ar
                ? "هذه نافذة مسؤول، ولا أستطيع التحكم فيها. سيقرؤها NVDA. اضغط Alt+Tab للخروج منها."
                : "This is an administrator window. NVDA will read it, but I can't control it. Press Alt+Tab to leave it.",
            ReasonCodes.PrivateNoScreenshot => ar
                ? "الوضع الخاص يعمل، فلن ألتقط صورة للشاشة."
                : "Private mode is on, so I won't take a screenshot.",
            ReasonCodes.BankNoTyping => ar
                ? "في صفحات البنوك والدفع لا أكتب شيئًا. اكتب بنفسك."
                : "On bank and payment pages I don't type anything. Please type it yourself.",
            ReasonCodes.BankNoScreenshot => ar
                ? "لا ألتقط صورًا لصفحات البنوك والدفع."
                : "I don't take screenshots of bank and payment pages.",
            ReasonCodes.BankNoSubmit => ar
                ? "في صفحات البنوك والدفع اضغط إدخال بنفسك."
                : "On bank and payment pages, please press Enter yourself.",
            _ => ar
                ? "لا أستطيع تنفيذ هذه الخطوة حفاظًا على أمانك."
                : "I can't do that step, for your safety.",
        };
    }

    public static string Declined(Lang lang) => lang == Lang.Arabic ? "تم الإلغاء." : "Cancelled.";

    public static string StepLimit(Lang lang) => lang == Lang.Arabic
        ? "توقفت لأن المهمة احتاجت خطوات كثيرة. يمكنك أن تطلبها مرة أخرى بطريقة أبسط."
        : "I stopped because the task needed too many steps. You can ask again in a simpler way.";

    public static string TimeLimit(Lang lang) => lang == Lang.Arabic
        ? "توقفت لأن المهمة استغرقت وقتًا طويلًا."
        : "I stopped because the task took too long.";

    public static string PolicyStopped(Lang lang) => lang == Lang.Arabic
        ? "أوقفت المهمة حفاظًا على أمانك."
        : "I stopped the task for your safety.";

    public static string BankStop(Lang lang) => lang == Lang.Arabic
        ? "هذه صفحة بنك أو دفع، فأوقفت الذكاء الاصطناعي ولن يُرسل منها شيء."
        : "This is a bank or payment page, so I stopped the AI and nothing from it is sent.";

    public static string PrivateStop(Lang lang) => lang == Lang.Arabic
        ? "الوضع الخاص يعمل، فلن أرسل شيئًا إلى الإنترنت."
        : "Private mode is on, so nothing is sent online.";
}
