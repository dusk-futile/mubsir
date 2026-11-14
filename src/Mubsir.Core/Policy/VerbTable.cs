namespace Mubsir.Core.Policy;

public static class VerbTable
{
    private static readonly ConfirmKind[] Severity =
    [
        ConfirmKind.Transfer, ConfirmKind.Pay, ConfirmKind.Buy, ConfirmKind.Delete, ConfirmKind.Restart,
        ConfirmKind.Send, ConfirmKind.Post, ConfirmKind.Install, ConfirmKind.SignOut, ConfirmKind.Accept,
        ConfirmKind.Submit,
    ];

    private static readonly Dictionary<string, ConfirmKind> Words = Build(
        (ConfirmKind.Send, ["send", "إرسال", "ارسال", "ارسل", "أرسل", "ابعت", "بعت", "ابعث"]),
        (ConfirmKind.Pay, ["pay", "ادفع", "دفع"]),
        (ConfirmKind.Buy, ["buy", "purchase", "checkout", "شراء", "اشتري", "اشترِ", "اطلب"]),
        (ConfirmKind.Transfer, ["transfer", "تحويل"]),
        (ConfirmKind.Delete, ["delete", "remove", "erase", "حذف", "احذف", "امسح", "مسح", "إزالة"]),
        (ConfirmKind.Submit, ["submit", "confirm", "تأكيد", "أكد"]),
        (ConfirmKind.SignOut, ["logout", "signout", "logoff", "خروج"]),
        (ConfirmKind.Restart, ["restart", "reboot", "shutdown"]),
        (ConfirmKind.Install, ["install", "uninstall", "تثبيت"]),
        (ConfirmKind.Post, ["post", "publish", "share", "tweet", "نشر", "انشر", "مشاركة", "شارك"]),
        (ConfirmKind.Accept, ["accept", "agree", "أوافق", "موافق", "قبول", "اقبل"]));

    private static readonly (string[] Phrase, ConfirmKind Kind)[] Phrases = BuildPhrases(
        (ConfirmKind.Transfer, ["send money", "حول الآن", "حول المبلغ", "حول الأموال", "حول الفلوس", "حول الرصيد"]),
        (ConfirmKind.Pay, ["pay now"]),
        (ConfirmKind.Buy, ["buy now", "place order", "place your order", "place my order", "order now", "complete purchase", "check out", "اطلب الآن", "إتمام الشراء"]),
        (ConfirmKind.Delete, ["move to trash", "move to bin", "move to recycle bin"]),
        (ConfirmKind.Submit, ["تأكيد الطلب"]),
        (ConfirmKind.SignOut, ["sign out", "log out", "log off", "sign off", "تسجيل الخروج"]),
        (ConfirmKind.Restart, ["shut down", "power off", "إعادة التشغيل", "إعادة تشغيل", "إيقاف التشغيل", "إيقاف تشغيل"]),
        (ConfirmKind.Install, ["إلغاء التثبيت"]),
        (ConfirmKind.Accept, ["i agree", "accept all"]));

    private static readonly HashSet<string> BenignNames = new(
        new[] { "طريقة الدفع", "طرق الدفع", "خيارات الدفع", "وسائل الدفع", "وسيلة الدفع" }.Select(PolicyText.Fold),
        StringComparer.Ordinal);

    private const string Hawwil = "حول";

    private static readonly HashSet<string> HarmlessWords = new(
        new[]
        {
            "search", "بحث", "ابحث", "next", "التالي", "back", "رجوع", "previous", "السابق", "open", "فتح", "افتح",
            "play", "pause", "تشغيل", "ok", "reply", "رد", "continue", "متابعة", "cancel", "إلغاء", "close", "إغلاق",
            "menu", "القائمة", "more", "المزيد", "login", "signin", "go", "اذهب", "find", "filter", "تصفية",
        }.Select(PolicyText.Fold),
        StringComparer.Ordinal);

    public static ConfirmKind? Classify(string? buttonNameOrId)
    {
        var kinds = Match(buttonNameOrId, lenient: false);
        foreach (var kind in Severity)
        {
            if (kinds.Contains(kind))
            {
                return kind;
            }
        }

        return null;
    }

    internal static bool IsHarmlessName(string? name)
    {
        var tokens = PolicyText.Tokens(name);
        if (tokens.Count is 0 or > 3)
        {
            return false;
        }

        if (tokens.Count == 2 && tokens[0] == "sign" && tokens[1] == "in")
        {
            return true;
        }

        return tokens.Any(t => PolicyText.Variants(t).Any(HarmlessWords.Contains));
    }

    internal static HashSet<ConfirmKind> UtteranceKinds(string? utterance) => Match(utterance, lenient: true);

    private static readonly string[] LenientSendPrefixes = ["ابعت", "بعت", "ارسل", "ابعث", "قول", "اكتب"];

    private static readonly string[] ShortSendStems = ["رد", "قل"];
    private static readonly string[] PronounSuffixes = ["", "ه", "ها", "هم", "ي", "لي", "له", "لها", "لهم", "ك", "لك", "ني", "نا", "لنا"];

    private static readonly HashSet<string> LenientSendWords = new(StringComparer.Ordinal)
    {
        "reply", "respond", "answer", "tell", "write",
    };

    private static readonly HashSet<string> ImperativeSendWords = new(StringComparer.Ordinal) { "email", "message", "text", "mail" };

    private static HashSet<ConfirmKind> Match(string? text, bool lenient)
    {
        var kinds = new HashSet<ConfirmKind>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return kinds;
        }

        var tokens = PolicyText.Tokens(text);
        if (tokens.Count == 0)
        {
            return kinds;
        }

        var joined = string.Join(' ', tokens);
        if (!lenient && BenignNames.Contains(joined))
        {
            return kinds;
        }

        foreach (var (phrase, kind) in Phrases)
        {
            if (PolicyText.ContainsSequence(tokens, phrase))
            {
                kinds.Add(kind);
            }
        }

        foreach (var token in tokens)
        {
            foreach (var v in PolicyText.Variants(token))
            {
                if (Words.TryGetValue(v, out var kind))
                {
                    kinds.Add(kind);
                }

                if (lenient && IsLenientSend(v))
                {
                    kinds.Add(ConfirmKind.Send);
                }
            }
        }

        if (lenient)
        {
            int first = tokens[0] == "please" && tokens.Count > 1 ? 1 : 0;
            if (ImperativeSendWords.Contains(tokens[first]))
            {
                kinds.Add(ConfirmKind.Send);
            }
        }

        if (joined == Hawwil || HasShaddaHawwil(text) || (lenient && tokens.Contains(Hawwil)))
        {
            kinds.Add(ConfirmKind.Transfer);
        }

        return kinds;
    }

    private static bool IsLenientSend(string token) =>
        LenientSendWords.Contains(token)
        || LenientSendPrefixes.Any(p => token.StartsWith(PolicyText.Fold(p), StringComparison.Ordinal))
        || ShortSendStems.Any(stem => PronounSuffixes.Any(s => token == stem + s));

    private static bool HasShaddaHawwil(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in PolicyText.Nfkc(text))
        {
            if (c == 'ّ' || !PolicyText.IsIgnorable(c))
            {
                sb.Append(c);
            }
        }

        var s = sb.ToString();
        int i = s.IndexOf("حوّل", StringComparison.Ordinal);
        while (i >= 0)
        {
            bool startOk = i == 0 || !char.IsLetter(s[i - 1]) || s[i - 1] == 'و' || s[i - 1] == 'ف';
            int end = i + 4;
            bool endOk = end >= s.Length || !char.IsLetter(s[end]) || "يهاكن".Contains(s[end]);
            if (startOk && endOk)
            {
                return true;
            }

            i = s.IndexOf("حوّل", i + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static Dictionary<string, ConfirmKind> Build(params (ConfirmKind Kind, string[] Words)[] groups)
    {
        var map = new Dictionary<string, ConfirmKind>(StringComparer.Ordinal);
        foreach (var (kind, words) in groups)
        {
            foreach (var w in words)
            {
                map[PolicyText.Fold(w)] = kind;
            }
        }

        return map;
    }

    private static (string[], ConfirmKind)[] BuildPhrases(params (ConfirmKind Kind, string[] Phrases)[] groups)
    {
        var list = new List<(string[], ConfirmKind)>();
        foreach (var (kind, phrases) in groups)
        {
            foreach (var p in phrases)
            {
                list.Add((PolicyText.Tokens(p).ToArray(), kind));
            }
        }

        return list.ToArray();
    }
}
