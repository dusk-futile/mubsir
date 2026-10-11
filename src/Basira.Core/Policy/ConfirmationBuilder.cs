using System.Text;
using Basira.Core.Common;
using Basira.Core.Context;

namespace Basira.Core.Policy;

/// <summary>
/// Builds the spoken confirmation sentence from <see cref="ConfirmationFacts"/> only (UIA and file system facts),
/// never from the model's words. Plain text for the speech layer.
/// </summary>
public static class ConfirmationBuilder
{
    public const int PreviewWords = 15;
    public const string ArabicSuggestedWarning = "هذا اقتراح من الصفحة وليس منك.";
    public const string EnglishSuggestedWarning = "This was suggested by the page, not by you.";
    public const string ArabicKeysHint = "اضغط إدخال للتأكيد أو Escape للإلغاء.";
    public const string EnglishKeysHint = "Press Enter to confirm or Escape to cancel.";

    private const int MaxFieldChars = 60;
    private const int MaxSiteChars = 80;
    private const int MaxFilesListed = 3;

    public static string Build(ConfirmationFacts facts, Lang lang)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var f = new Clean(facts);
        var core = lang == Lang.Arabic ? Arabic(f) : English(f);
        var sb = new StringBuilder();
        if (facts.SuggestedByPage)
        {
            sb.Append(lang == Lang.Arabic ? ArabicSuggestedWarning : EnglishSuggestedWarning).Append(' ');
        }

        sb.Append(core).Append(' ').Append(lang == Lang.Arabic ? ArabicKeysHint : EnglishKeysHint);
        return sb.ToString();
    }

    /// <summary>Facts cleaned for speech: single-line, length-capped, preview cut to <see cref="PreviewWords"/> words.</summary>
    private sealed class Clean
    {
        public Clean(ConfirmationFacts f)
        {
            Kind = f.Kind;
            Button = Field(f.ButtonName, MaxFieldChars);
            Recipient = Field(f.Recipient, MaxFieldChars);
            Amount = Field(f.Amount, MaxFieldChars);
            Site = Field(f.Site, MaxSiteChars);
            Preview = PreviewOf(f.BodyPreview);
            Tool = f.ToolName?.Trim().ToLowerInvariant();
            Key = Field(f.KeyChord, 30);
            Files = f.FileNames.Select(n => Field(FileNameOf(n), MaxFieldChars)).Where(n => n is not null).Cast<string>().ToList();
            FileCount = Math.Max(f.FileCount, f.FileNames.Count);
        }

        public ConfirmKind Kind { get; }

        public string? Button { get; }

        public string? Recipient { get; }

        public string? Amount { get; }

        public string? Site { get; }

        public string? Preview { get; }

        public string? Tool { get; }

        public string? Key { get; }

        public List<string> Files { get; }

        public int FileCount { get; }
    }

    private static string Arabic(Clean f)
    {
        string site = f.Site is null ? "" : " في موقع " + f.Site;
        string q(string s) => "«" + s + "»";
        switch (f.Kind)
        {
            case ConfirmKind.Send:
                if (f.Tool == "open_url")
                {
                    return f.Recipient is null ? "سأفتح رسالة بريد جديدة." : $"سأفتح رسالة بريد جديدة إلى {f.Recipient}.";
                }

                if (f.Recipient is not null && f.Preview is not null)
                {
                    return $"سأرسل رسالة إلى {f.Recipient}{site}: {q(f.Preview)}.";
                }

                if (f.Recipient is not null)
                {
                    return $"سأرسل الرسالة إلى {f.Recipient}{site}.";
                }

                if (f.Preview is not null)
                {
                    return $"سأرسل هذه الرسالة{site}: {q(f.Preview)}.";
                }

                return f.Button is not null ? $"سأضغط {q(f.Button)} للإرسال{site}." : $"سأرسل الرسالة{site}.";

            case ConfirmKind.Pay:
                return Money("سأدفع", " إلى ", f, site, "للدفع", ar: true);

            case ConfirmKind.Transfer:
                return Money("سأحوّل", " إلى ", f, site, "للتحويل", ar: true);

            case ConfirmKind.Buy:
                if (f.Amount is null && f.Site is null && f.Button is not null)
                {
                    return $"سأضغط {q(f.Button)} لإتمام الشراء.";
                }

                return "سأكمل عملية الشراء" + (f.Amount is null ? "" : " بمبلغ " + f.Amount) + site + ".";

            case ConfirmKind.Delete:
                if (f.FileCount > 0)
                {
                    if (f.FileCount == 1 && f.Files.Count == 1)
                    {
                        return $"سأنقل الملف {q(f.Files[0])} إلى سلة المحذوفات.";
                    }

                    var list = f.Files.Count > 0 ? "، منها: " + string.Join("، ", f.Files.Take(MaxFilesListed)) : "";
                    return $"سأنقل {ArabicFiles(f.FileCount)} إلى سلة المحذوفات{list}.";
                }

                return f.Button is not null ? $"سأضغط {q(f.Button)} للحذف{site}." : $"سأحذف العنصر المحدد{site}.";

            case ConfirmKind.Submit:
                return f.Button is not null ? $"سأضغط {q(f.Button)} للتأكيد{site}." : $"سأؤكد الطلب{site}.";

            case ConfirmKind.SignOut:
                return f.Site is null ? "سأسجل الخروج." : $"سأسجل الخروج من موقع {f.Site}.";

            case ConfirmKind.Restart:
                return "سأعيد تشغيل الجهاز الآن. احفظ عملك أولًا.";

            case ConfirmKind.Install:
                return f.Button is not null ? $"سأضغط {q(f.Button)} للتثبيت{site}." : $"سأبدأ التثبيت{site}.";

            case ConfirmKind.Post:
                if (f.Preview is not null)
                {
                    return $"سأنشر{site}: {q(f.Preview)}.";
                }

                return f.Button is not null ? $"سأضغط {q(f.Button)} للنشر{site}." : $"سأنشر{site}.";

            case ConfirmKind.Accept:
                return f.Button is not null ? $"سأضغط {q(f.Button)} للموافقة{site}." : $"سأوافق على الشروط{site}.";

            default:
                return ArabicGeneric(f, site, q);
        }
    }

    private static string ArabicGeneric(Clean f, string site, Func<string, string> q)
    {
        switch (f.Tool)
        {
            case "remember":
                return f.Preview is null ? "سأحفظ معلومة جديدة في ذاكرتي." : $"سأحفظ هذه المعلومة: {q(f.Preview)}.";
            case "open_url":
                return f.Site is null ? "سأفتح هذا الرابط." : $"سأفتح موقع {f.Site}.";
            case "press_keys":
                var key = f.Key is null or "enter" ? "إدخال" : f.Key;
                return f.Button is not null ? $"سأضغط {key} في {q(f.Button)}{site}." : $"سأضغط {key}{site}.";
        }

        return f.Button is not null ? $"سأضغط {q(f.Button)}{site}." : $"سأنفذ هذه الخطوة{site}.";
    }

    private static string English(Clean f)
    {
        string site = f.Site is null ? "" : " on " + f.Site;
        string q(string s) => "\"" + s + "\"";
        switch (f.Kind)
        {
            case ConfirmKind.Send:
                if (f.Tool == "open_url")
                {
                    return f.Recipient is null ? "I will open a new email." : $"I will open a new email to {f.Recipient}.";
                }

                if (f.Recipient is not null && f.Preview is not null)
                {
                    return $"I will send a message to {f.Recipient}{site}: {q(f.Preview)}.";
                }

                if (f.Recipient is not null)
                {
                    return $"I will send the message to {f.Recipient}{site}.";
                }

                if (f.Preview is not null)
                {
                    return $"I will send this message{site}: {q(f.Preview)}.";
                }

                return f.Button is not null ? $"I will press {q(f.Button)} to send{site}." : $"I will send the message{site}.";

            case ConfirmKind.Pay:
                return Money("I will pay", " to ", f, site, "to pay", ar: false);

            case ConfirmKind.Transfer:
                return Money("I will transfer", " to ", f, site, "to transfer", ar: false);

            case ConfirmKind.Buy:
                if (f.Amount is null && f.Site is null && f.Button is not null)
                {
                    return $"I will press {q(f.Button)} to complete the purchase.";
                }

                return "I will complete the purchase" + (f.Amount is null ? "" : " for " + f.Amount) + site + ".";

            case ConfirmKind.Delete:
                if (f.FileCount > 0)
                {
                    if (f.FileCount == 1 && f.Files.Count == 1)
                    {
                        return $"I will move the file {q(f.Files[0])} to the Recycle Bin.";
                    }

                    var list = f.Files.Count > 0 ? ", including: " + string.Join(", ", f.Files.Take(MaxFilesListed)) : "";
                    var files = f.FileCount == 1 ? "1 file" : f.FileCount + " files";
                    return $"I will move {files} to the Recycle Bin{list}.";
                }

                return f.Button is not null ? $"I will press {q(f.Button)} to delete{site}." : $"I will delete the selected item{site}.";

            case ConfirmKind.Submit:
                return f.Button is not null ? $"I will press {q(f.Button)} to confirm{site}." : $"I will confirm the order{site}.";

            case ConfirmKind.SignOut:
                return f.Site is null ? "I will sign out." : $"I will sign out of {f.Site}.";

            case ConfirmKind.Restart:
                return "I will restart the computer now. Save your work first.";

            case ConfirmKind.Install:
                return f.Button is not null ? $"I will press {q(f.Button)} to install{site}." : $"I will start the installation{site}.";

            case ConfirmKind.Post:
                if (f.Preview is not null)
                {
                    return $"I will post{site}: {q(f.Preview)}.";
                }

                return f.Button is not null ? $"I will press {q(f.Button)} to post{site}." : $"I will post{site}.";

            case ConfirmKind.Accept:
                return f.Button is not null ? $"I will press {q(f.Button)} to agree{site}." : $"I will accept the terms{site}.";

            default:
                return EnglishGeneric(f, site, q);
        }
    }

    private static string EnglishGeneric(Clean f, string site, Func<string, string> q)
    {
        switch (f.Tool)
        {
            case "remember":
                return f.Preview is null ? "I will save a new fact to my memory." : $"I will remember: {q(f.Preview)}.";
            case "open_url":
                return f.Site is null ? "I will open this link." : $"I will open {f.Site}.";
            case "press_keys":
                var key = f.Key is null or "enter" ? "Enter" : f.Key;
                return f.Button is not null ? $"I will press {key} in {q(f.Button)}{site}." : $"I will press {key}{site}.";
        }

        return f.Button is not null ? $"I will press {q(f.Button)}{site}." : $"I will do this step{site}.";
    }

    private static string Money(string verb, string toWord, Clean f, string site, string purpose, bool ar)
    {
        if (f.Amount is null && f.Recipient is null && f.Site is null && f.Button is not null)
        {
            return ar ? $"سأضغط «{f.Button}» {purpose}." : $"I will press \"{f.Button}\" {purpose}.";
        }

        var sb = new StringBuilder(verb);
        if (f.Amount is not null)
        {
            sb.Append(' ').Append(f.Amount);
        }

        if (f.Recipient is not null)
        {
            sb.Append(toWord).Append(f.Recipient);
        }

        return sb.Append(site).Append('.').ToString();
    }

    /// <summary>Arabic counted noun for files: ملفين، ٣ ملفات، ١٢ ملفًا، ١٠٠ ملف.</summary>
    internal static string ArabicFiles(int n)
    {
        var digits = PolicyText.ToArabicDigits(n.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (n == 1)
        {
            return "ملفًا واحدًا";
        }

        if (n == 2)
        {
            return "ملفين";
        }

        int r = n % 100;
        if (n >= 100 && r is 0 or 1 or 2)
        {
            return digits + " ملف";
        }

        return r is >= 3 and <= 10 ? digits + " ملفات" : digits + " ملفًا";
    }

    private static string? Field(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        // Single line, no quote marks that could fake the end of the sentence.
        var t = ContextBuilder.CollapseWhitespace(s).Replace('«', '"').Replace('»', '"').Trim().TrimEnd('.', '،', ',');
        return t.Length == 0 ? null : ContextBuilder.Clip(t, max);
    }

    private static string? PreviewOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var words = ContextBuilder.CollapseWhitespace(body).Replace('«', '"').Replace('»', '"')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= PreviewWords)
        {
            return string.Join(' ', words);
        }

        return string.Join(' ', words.Take(PreviewWords)) + "…";
    }

    private static string FileNameOf(string path)
    {
        var p = path.TrimEnd('\\', '/');
        int cut = p.LastIndexOfAny(['\\', '/']);
        return cut >= 0 ? p[(cut + 1)..] : p;
    }
}
