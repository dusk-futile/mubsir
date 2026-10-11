using System.Globalization;
using System.Text.RegularExpressions;
using Mubsir.Core.Common;

namespace Mubsir.Core.Localization;

public static partial class Strings
{
    private static readonly Dictionary<StringKey, (string Ar, string En)> Table = new()
    {
        [StringKey.Ready] = ("مُبصِر جاهز", "Mubsir is ready."),
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
        [StringKey.Paused] = ("مُبصِر متوقف مؤقتًا.", "Mubsir is paused."),
        [StringKey.PausedPressEnter] = ("مُبصِر متوقف مؤقتًا. اضغط إدخال للمتابعة.", "Mubsir is paused. Press Enter to resume."),
        [StringKey.Resumed] = ("عاد مُبصِر إلى العمل.", "Mubsir is back on."),
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
        [StringKey.ReportSaved] = ("حفظت تقريرًا على سطح المكتب.", "I saved a report on the desktop."),
        [StringKey.UpdateReady] = (
            "يوجد تحديث جديد لمُبصِر، الإصدار {0}. اضغط إدخال لتثبيته.",
            "A Mubsir update, version {0}, is ready. Press Enter to install it."),
        [StringKey.UpdateRolledBack] = (
            "لم ينجح التحديث، فعدت إلى الإصدار السابق.",
            "The update didn't work, so I went back to the previous version."),
        [StringKey.CrashRecovered] = ("حدث خطأ، ومُبصِر جاهز مرة أخرى.", "Something went wrong, and Mubsir is ready again."),
        [StringKey.UacDuringTask] = (
            "ظهرت نافذة أذونات من Windows. إذا لم تطلب هذا، فاختر لا.",
            "A Windows permission prompt appeared. If you didn't ask for this, choose No."),
        [StringKey.LayerHelp] = (
            "لفتح طبقة الأوامر اضغط Control الأيمن مرتين بسرعة، ثم: "
            + "المسافة للقراءة من هنا. "
            + "السهم الأيسر للجملة السابقة، والسهم الأيمن للجملة التالية. "
            + "السهم لأعلى للفقرة السابقة، والسهم لأسفل للفقرة التالية. "
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
        [StringKey.KeyExplorerNoAction] = ("ليس لهذا المفتاح أمر في مُبصِر.", "This key has no Mubsir command."),
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
        [StringKey.Unmuted] = ("أعدت الصوت.", "Sound is back on."),
        [StringKey.VolumeLevel] = ("مستوى الصوت {0} بالمئة.", "Volume {0} percent."),
        [StringKey.SettingsMenu] = (
            "الإعدادات: سرعة الكلام، ومستوى التفصيل، ومفتاح التحدث، والميزانية، والإيقاف المؤقت. قل ما تريد تغييره.",
            "Settings: speech speed, detail level, talk key, budget, and pause. Say what you want to change."),
        [StringKey.BudgetStatus] = (
            "استخدمت هذا الشهر {0:0.##} دولار من {1:0.##} دولار.",
            "This month you have used {0:0.##} of {1:0.##} dollars."),
        [StringKey.NothingToRead] = ("لا يوجد نص لأقرأه هنا.", "There is no text to read here."),
        [StringKey.Done] = ("تم.", "Done."),
        [StringKey.KeyHelpTalkAndLayer] = (
            "مفتاح التحدث: اضغطه مطولًا وتكلم ثم اتركه، واضغطه مرتين بسرعة لفتح طبقة الأوامر.",
            "Talk key: hold it and speak, then let go. Press it twice quickly to open the command layer."),
        [StringKey.KeyHelpTalk] = ("مفتاح التحدث: اضغطه مطولًا وتكلم ثم اتركه.", "Talk key: hold it and speak, then let go."),
        [StringKey.KeyHelpLayer] = (
            "مفتاح طبقة الأوامر: اضغطه مرتين بسرعة لفتح طبقة الأوامر.",
            "Command layer key: press it twice quickly to open the command layer."),
        [StringKey.KeyHelpControl] = ("Control: يوقف الكلام.", "Control: stops speech."),
        [StringKey.KeyHelpSpace] = ("المسافة: في طبقة الأوامر للقراءة من هنا.", "Space: in the command layer, reads from here."),
        [StringKey.KeyHelpLeftArrow] = ("السهم الأيسر: في طبقة الأوامر للجملة السابقة.", "Left arrow: in the command layer, the previous sentence."),
        [StringKey.KeyHelpRightArrow] = ("السهم الأيمن: في طبقة الأوامر للجملة التالية.", "Right arrow: in the command layer, the next sentence."),
        [StringKey.KeyHelpUpArrow] = ("السهم لأعلى: في طبقة الأوامر للفقرة السابقة.", "Up arrow: in the command layer, the previous paragraph."),
        [StringKey.KeyHelpDownArrow] = ("السهم لأسفل: في طبقة الأوامر للفقرة التالية.", "Down arrow: in the command layer, the next paragraph."),
        [StringKey.KeyHelpPeriod] = (
            "النقطة: في طبقة الأوامر لإعادة آخر جملة، ومرتين لإعادة آخر إجابة كاملة.",
            "Period: in the command layer, repeats the last sentence; press it twice for the whole last answer."),
        [StringKey.KeyHelpEnter] = (
            "إدخال: في طبقة الأوامر لكتابة طلب، وعندما أطلب التأكيد فهو للموافقة.",
            "Enter: in the command layer, type a request. When I ask you to confirm, it means yes."),
        [StringKey.KeyHelpF1] = ("F1: في طبقة الأوامر للمساعدة ومستكشف المفاتيح.", "F1: in the command layer, help and the key explorer."),
        [StringKey.KeyHelpNvdaKey] = ("مفتاح NVDA، ولا يستخدمه مُبصِر.", "The NVDA key. Mubsir does not use it."),
    };

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

    public static bool Has(StringKey key) => Table.ContainsKey(key);

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
