namespace Basira.Core.Policy;

/// <summary>
/// Bilingual (English, Arabic MSA and common dialect) table of button verbs whose action is hard to undo.
/// Matching is by whole words after folding, so "Search", "Reply", "Next" or "Sent items" never match.
/// </summary>
public static class VerbTable
{
    // Most severe first: when a name holds several verbs ("Confirm and pay") the first listed kind wins.
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

    // Whole names that contain a verb but only choose or describe something.
    private static readonly HashSet<string> BenignNames = new(
        new[] { "طريقة الدفع", "طرق الدفع", "خيارات الدفع", "وسائل الدفع", "وسيلة الدفع" }.Select(PolicyText.Fold),
        StringComparer.Ordinal);

    // "حول" alone also means "about"; it counts only as the whole name or with a shadda ("حوّل").
    private const string Hawwil = "حول";

    // Words that clearly name a harmless control. When the visible name is one of these,
    // the automation id is not consulted (an id like "submit" on a search button is common).
    private static readonly HashSet<string> HarmlessWords = new(
        new[]
        {
            "search", "بحث", "ابحث", "next", "التالي", "back", "رجوع", "previous", "السابق", "open", "فتح", "افتح",
            "play", "pause", "تشغيل", "ok", "reply", "رد", "continue", "متابعة", "cancel", "إلغاء", "close", "إغلاق",
            "menu", "القائمة", "more", "المزيد", "login", "signin", "go", "اذهب", "find", "filter", "تصفية",
        }.Select(PolicyText.Fold),
        StringComparer.Ordinal);

    /// <summary>The kind of irreversible action a button name or automation id stands for, or null.</summary>
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

    /// <summary>True when the name is a plainly harmless control such as "Search" or "Next".</summary>
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

    /// <summary>
    /// Every kind mentioned in the user's own words. Lenient: dialect suffixes ("ابعتله") and
    /// reply-like verbs count as Send. Used to decide whether a verb button was the user's idea.
    /// </summary>
    internal static HashSet<ConfirmKind> UtteranceKinds(string? utterance) => Match(utterance, lenient: true);

    // Verb stems that take dialect suffixes freely ("ابعتله", "اكتبلها", "قوله").
    private static readonly string[] LenientSendPrefixes = ["ابعت", "بعت", "ارسل", "ابعث", "قول", "اكتب"];

    // Short stems that only count alone or with a pronoun suffix ("رد", "ردي", "قله"), not inside other words ("قليل").
    private static readonly string[] ShortSendStems = ["رد", "قل"];
    private static readonly string[] PronounSuffixes = ["", "ه", "ها", "هم", "ي", "لي", "له", "لها", "لهم", "ك", "لك", "ني", "نا", "لنا"];

    // Verbs only. Nouns such as "message" or "email" do not count: "read the message" is not a request to send.
    private static readonly HashSet<string> LenientSendWords = new(StringComparer.Ordinal)
    {
        "reply", "respond", "answer", "tell", "write",
    };

    // Nouns that are verbs at the start of a request: "email Sara that I am late", "message Ahmed".
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
        // Keep only the shadda among the diacritics, then look for the word حوّل.
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
