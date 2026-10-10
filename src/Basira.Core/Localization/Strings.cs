using System.Globalization;
using System.Text.RegularExpressions;
using Basira.Core.Common;

namespace Basira.Core.Localization;

/// <summary>
/// Basira's own spoken sentences in simple Modern Standard Arabic and English: short and kind.
/// Key names (Enter, Escape, Control, Alt, Tab, F1) stay as NVDA and keyboards name them.
/// </summary>
public static partial class Strings
{
    private static readonly Dictionary<StringKey, (string Ar, string En)> Table = new()
    {
        [StringKey.Ready] = ("بصيرة جاهزة", "Basira is ready."),
        [StringKey.NoInternet] = ("لا يوجد اتصال بالإنترنت.", "No internet connection."),
        [StringKey.AiBusyRetrying] = ("الذكاء الاصطناعي مشغول الآن، سأحاول مرة أخرى.", "The AI is busy. I'll try again."),
        [StringKey.AiUnavailable] = ("الذكاء الاصطناعي غير متاح الآن.", "The AI is not available right now."),
        [StringKey.BudgetWarning80] = (
            "وصلتَ إلى ثمانين بالمئة من ميزانية الذكاء الاصطناعي لهذا الشهر: {0:0.##} دولار.",
            "You have used 80 percent of this month's AI budget: {0:0.##} dollars."),
        [StringKey.BudgetReached] = (
            "انتهت ميزانية الذكاء الاصطناعي لهذا الشهر. اضغط إدخال للسماح بدولار إضافي.",
            "This month's AI budget is used up. Press Enter to allow one more dollar."),
        [StringKey.BudgetExtraAllowed] = ("أضفت دولارًا واحدًا إلى ميزانية هذا الشهر.", "I added one more dollar to this month's budget."),
        [StringKey.MicSilent] = (
            "لم أسمع شيئًا. تأكد من إعداد خصوصية الميكروفون أو زر كتم الصوت.",
            "I heard nothing. Check the microphone privacy setting or the mute key."),
        [StringKey.HeardNothing] = ("لم أسمع شيئًا، حاول مرة أخرى.", "I didn't hear anything. Please try again."),
        [StringKey.NvdaStopped] = ("توقف NVDA، وسأعيد تشغيله الآن.", "NVDA stopped. I'm restarting it now."),
        [StringKey.NvdaRestarted] = ("عاد NVDA إلى العمل.", "NVDA is running again."),
        [StringKey.NvdaRestartHint] = ("إذا لم يعد NVDA، فاضغط Control مع Alt مع N.", "If NVDA doesn't come back, press Control+Alt+N."),
        [StringKey.Paused] = ("بصيرة متوقفة مؤقتًا.", "Basira is paused."),
        [StringKey.PausedPressEnter] = ("بصيرة متوقفة مؤقتًا. اضغط إدخال للمتابعة.", "Basira is paused. Press Enter to resume."),
        [StringKey.Resumed] = ("عادت بصيرة إلى العمل.", "Basira is back on."),
        [StringKey.TaskStopped] = ("أوقفت المهمة.", "I stopped the task."),
        [StringKey.TaskPaused] = (
            "توقفت مؤقتًا. اضغط إدخال للمتابعة أو Escape للإلغاء.",
            "Paused. Press Enter to continue or Escape to cancel."),
        [StringKey.TaskStepLimit] = (
            "توقفت لأن المهمة احتاجت خطوات كثيرة. أخبرني ماذا أفعل بعد ذلك.",
            "I stopped because the task needed too many steps. Tell me what to do next."),
        [StringKey.TaskTimeLimit] = (
            "توقفت لأن المهمة استغرقت وقتًا طويلًا. أخبرني ماذا أفعل بعد ذلك.",
            "I stopped because the task took too long. Tell me what to do next."),
        [StringKey.AdminWindow] = (
            "هذه نافذة مسؤول. سيقرؤها NVDA، ولا أستطيع التحكم فيها. اضغط Alt مع Tab للخروج منها.",
            "This is an administrator window. NVDA will read it, but I can't control it. Press Alt+Tab to leave it."),
        [StringKey.PartialRead] = ("قرأت جزءًا من النافذة فقط لأنها كبيرة.", "I read only part of this window because it is very large."),
        [StringKey.BankModeOn] = ("وضع البنك: الذكاء الاصطناعي متوقف في هذه الصفحة.", "Bank mode: the AI is off on this page."),
        [StringKey.BankModeOff] = ("انتهى وضع البنك.", "Bank mode is off."),
        [StringKey.AiLabel] = ("حسب الذكاء الاصطناعي:", "According to the AI:"),
        [StringKey.ConfirmSuffix] = ("اضغط إدخال للتأكيد أو Escape للإلغاء.", "Press Enter to confirm or Escape to cancel."),
        [StringKey.ConfirmCancelled] = ("تم الإلغاء.", "Cancelled."),
        [StringKey.ConfirmTimedOut] = ("لم يصل رد، فألغيت الأمر.", "No answer, so I cancelled it."),
        [StringKey.SuggestedByPage] = ("هذا اقتراح من الصفحة وليس منك.", "This was suggested by the page, not by you."),
        [StringKey.PasswordField] = ("حقل كلمة مرور: اكتب كلمة المرور ثم اضغط إدخال.", "Password field: type your password, then press Enter."),
        [StringKey.OtpLocal] = (
            "رمز التحقق هو {0}. قرأته على جهازك فقط ولم أرسله.",
            "The verification code is {0}. I read it on this computer only and did not send it."),
        [StringKey.PrivateModeOn] = (
            "الوضع الخاص مفعّل: لن أرسل شيئًا إلى الإنترنت. القراءة المحلية تعمل.",
            "Private mode is on: I won't send anything to the internet. Local reading still works."),
        [StringKey.PrivateModeOff] = ("انتهى الوضع الخاص.", "Private mode is off."),
        [StringKey.PrivateModeBlocked] = (
            "الوضع الخاص مفعّل، لذلك لا أستخدم الذكاء الاصطناعي الآن.",
            "Private mode is on, so I'm not using the AI now."),
        [StringKey.ReportSaved] = ("حفظت تقريرًا لعائلتك على سطح المكتب.", "I saved a report for your family on the desktop."),
        [StringKey.UpdateReady] = (
            "يوجد تحديث جديد لبصيرة، الإصدار {0}. اضغط إدخال لتثبيته.",
            "A Basira update, version {0}, is ready. Press Enter to install it."),
        [StringKey.UpdateRolledBack] = (
            "لم ينجح التحديث، فعدت إلى الإصدار السابق.",
            "The update didn't work, so I went back to the previous version."),
        [StringKey.CrashRecovered] = ("حدث خطأ، وبصيرة جاهزة مرة أخرى.", "Something went wrong, and Basira is ready again."),
        [StringKey.UacDuringTask] = (
            "ظهرت نافذة أذونات من Windows. إذا لم تطلب هذا، فاختر لا.",
            "A Windows permission prompt appeared. If you didn't ask for this, choose No."),
        [StringKey.LayerHelp] = (
            "لفتح طبقة الأوامر اضغط Control الأيمن مرتين بسرعة، ثم: "
            + "المسافة للقراءة من هنا. "
            + "السهم الأيسر للجملة السابقة، والسهم الأيمن للجملة التالية. "
            + "السهم الأعلى للفقرة السابقة، والسهم الأسفل للفقرة التالية. "
            + "النقطة لإعادة آخر جملة، ومرتين لإعادة آخر إجابة كاملة. "
            + "إدخال لكتابة طلب. "
            + "Escape لإيقاف المهمة الحالية. "
            + "F1 للمساعدة ومستكشف المفاتيح.",
            "To open the command layer, press Right Control twice quickly, then: "
            + "Space to read from here. "
            + "Left arrow for the previous sentence, Right arrow for the next sentence. "
            + "Up arrow for the previous paragraph, Down arrow for the next paragraph. "
            + "Period to repeat the last sentence; press it twice for the whole last answer. "
            + "Enter to type a request. "
            + "Escape to stop the current task. "
            + "F1 for help and the key explorer."),
        [StringKey.KeyExplorerIntro] = (
            "مستكشف المفاتيح: اضغط أي مفتاح لتسمع وظيفته. اضغط Escape للخروج.",
            "Key explorer: press any key to hear what it does. Press Escape to leave."),
        [StringKey.KeyExplorerExit] = ("خرجت من مستكشف المفاتيح.", "Key explorer closed."),
        [StringKey.KeyExplorerNoAction] = ("ليس لهذا المفتاح أمر في بصيرة.", "This key has no Basira command."),
        [StringKey.Thinking] = ("لحظة من فضلك.", "One moment."),
        [StringKey.NotUnderstood] = ("لم أفهم. هل يمكنك أن تقولها بطريقة أخرى؟", "I didn't understand. Could you say it another way?"),
        [StringKey.NoApiKey] = (
            "لم تتم إضافة مفتاح الذكاء الاصطناعي بعد. القراءة المحلية تعمل.",
            "No AI key has been added yet. Local reading works."),
        [StringKey.LocalStillWorks] = ("القراءة المحلية تعمل.", "Local reading still works."),
        [StringKey.TypeRequestHint] = (
            "يمكنك كتابة طلبك: اضغط Control الأيمن مرتين ثم إدخال.",
            "You can type your request: press Right Control twice, then Enter."),
        [StringKey.NothingToRepeat] = ("لا يوجد شيء لأعيده.", "There is nothing to repeat."),
        [StringKey.OpeningApp] = ("أفتح {0}.", "Opening {0}."),
        [StringKey.AppNotFound] = ("لم أجد برنامجًا باسم {0}.", "I couldn't find an app called {0}."),
        [StringKey.WhereAmI] = ("أنت في {0}، {1}.", "You are in {0}, {1}."),
        [StringKey.TimeNow] = ("الساعة الآن {0}.", "It's {0}."),
        [StringKey.DateToday] = ("اليوم {0}.", "Today is {0}."),
        [StringKey.BatteryLevel] = ("البطارية {0} بالمئة.", "The battery is at {0} percent."),
        [StringKey.BatteryCharging] = ("الجهاز يشحن الآن.", "The computer is charging."),
        [StringKey.NoBattery] = ("هذا الجهاز لا يعمل بالبطارية.", "This computer has no battery."),
        [StringKey.Muted] = ("كتمت الصوت.", "Sound muted."),
        [StringKey.SpeakingFaster] = ("أسرع.", "Faster."),
        [StringKey.SpeakingSlower] = ("أبطأ.", "Slower."),
    };

    /// <summary>
    /// The sentence for <paramref name="key"/> in <paramref name="lang"/>, formatted with
    /// <paramref name="args"/> (invariant culture). Never throws: missing args become empty.
    /// </summary>
    public static string Get(StringKey key, Lang lang, params object[] args)
    {
        if (!Table.TryGetValue(key, out (string Ar, string En) entry))
        {
            return key.ToString();
        }

        string template = lang == Lang.Arabic ? entry.Ar : entry.En;
        int needed = PlaceholderCount(template);
        if (needed == 0)
        {
            return template;
        }

        object[] values = args ?? [];
        if (values.Length < needed)
        {
            var padded = new object[needed];
            Array.Fill(padded, string.Empty);
            values.CopyTo(padded, 0);
            values = padded;
        }

        try
        {
            string text = string.Format(CultureInfo.InvariantCulture, template, values);
            return values == args ? text : Tidy(text);
        }
        catch (FormatException)
        {
            return Tidy(Placeholder().Replace(template, string.Empty));
        }
    }

    /// <summary>Whether a text exists for <paramref name="key"/>.</summary>
    public static bool Has(StringKey key) => Table.ContainsKey(key);

    /// <summary>Number of format arguments the text uses (highest index + 1).</summary>
    public static int PlaceholderCount(string template)
    {
        int max = -1;
        foreach (Match m in Placeholder().Matches(template))
        {
            max = Math.Max(max, int.Parse(m.Groups[1].ValueSpan, CultureInfo.InvariantCulture));
        }

        return max + 1;
    }

    private static string Tidy(string text) => SpaceBeforePunctuation().Replace(MultiSpace().Replace(text, " "), "$1").Trim();

    [GeneratedRegex(@"\{(\d+)(?::[^}]*)?\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"[ ]{2,}")]
    private static partial Regex MultiSpace();

    [GeneratedRegex(@" ([.,:،؛?؟])")]
    private static partial Regex SpaceBeforePunctuation();
}
