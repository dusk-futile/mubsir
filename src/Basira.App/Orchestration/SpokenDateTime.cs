using System.Globalization;
using Basira.Core.Common;

namespace Basira.App.Orchestration;

/// <summary>Time and date phrased for speech ("الثالثة وعشر دقائق مساءً", "الأحد 11 أكتوبر 2026"), pure and testable.</summary>
internal static class SpokenDateTime
{
    private static readonly string[] ArabicHours =
    [
        "الثانية عشرة", "الواحدة", "الثانية", "الثالثة", "الرابعة", "الخامسة", "السادسة",
        "السابعة", "الثامنة", "التاسعة", "العاشرة", "الحادية عشرة",
    ];

    // Gregorian month names as used in Egypt and the Gulf (Levantine names differ; voices read these well).
    private static readonly string[] ArabicMonths =
    [
        "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر",
    ];

    private static readonly string[] ArabicDays = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

    public static string Time(DateTime local, Lang lang)
    {
        if (lang == Lang.English)
        {
            return local.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
        }

        string hour = ArabicHours[local.Hour % 12];
        string minutes = local.Minute switch
        {
            0 => " تمامًا",
            15 => " والربع",
            30 => " والنصف",
            1 => " ودقيقة",
            2 => " ودقيقتان",
            >= 3 and <= 10 => " و" + local.Minute.ToString(CultureInfo.InvariantCulture) + " دقائق",
            _ => " و" + local.Minute.ToString(CultureInfo.InvariantCulture) + " دقيقة",
        };
        string period = local.Hour < 12 ? "صباحًا" : "مساءً";
        return hour + minutes + " " + period;
    }

    public static string Date(DateTime local, Lang lang)
    {
        if (lang == Lang.English)
        {
            return local.ToString("dddd, MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US"));
        }

        return ArabicDays[(int)local.DayOfWeek] + " "
            + local.Day.ToString(CultureInfo.InvariantCulture) + " "
            + ArabicMonths[local.Month - 1] + " "
            + local.Year.ToString(CultureInfo.InvariantCulture);
    }
}
