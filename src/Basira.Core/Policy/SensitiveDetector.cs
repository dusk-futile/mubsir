using Basira.Core.Context;

namespace Basira.Core.Policy;

/// <summary>
/// Detects bank mode: a bank/payment/government domain from the user's list, a card/CVV/expiry/OTP/PIN/IBAN field,
/// or a Luhn-valid card number on screen. Reasons are short codes, never screen content.
/// </summary>
public static class SensitiveDetector
{
    public const string ReasonBankDomain = "bank-domain";
    public const string ReasonCardField = "card-field";
    public const string ReasonCvvField = "cvv-field";
    public const string ReasonExpiryField = "expiry-field";
    public const string ReasonOtpField = "otp-field";
    public const string ReasonPinField = "pin-field";
    public const string ReasonIbanField = "iban-field";
    public const string ReasonCardNumber = "card-number-visible";

    private const int MaxNodes = 5000;

    // Matched against the compact form (letters and digits only, folded): "Card Number" -> "cardnumber".
    private static readonly (string Key, string Reason)[] CompactKeys =
    [
        ("cardnumber", ReasonCardField), ("cardno", ReasonCardField), ("creditcard", ReasonCardField),
        ("debitcard", ReasonCardField), ("ccnumber", ReasonCardField), ("ccnum", ReasonCardField),
        ("securitycode", ReasonCvvField), ("cardverification", ReasonCvvField), ("cardcode", ReasonCvvField),
        ("expirydate", ReasonExpiryField), ("expirationdate", ReasonExpiryField), ("expdate", ReasonExpiryField),
        ("ccexp", ReasonExpiryField), ("validthru", ReasonExpiryField), ("validthrough", ReasonExpiryField),
        ("onetimecode", ReasonOtpField), ("onetimepassword", ReasonOtpField), ("onetimepin", ReasonOtpField),
        ("verificationcode", ReasonOtpField), ("smscode", ReasonOtpField), ("authcode", ReasonOtpField),
        ("authenticationcode", ReasonOtpField), ("otpcode", ReasonOtpField), ("twofactor", ReasonOtpField),
        ("pincode", ReasonPinField), ("accountnumber", ReasonIbanField), ("iban", ReasonIbanField),
    ];

    // Matched as whole words, because inside other words they are common ("shipping" contains "pin").
    private static readonly Dictionary<string, string> TokenKeys = new(StringComparer.Ordinal)
    {
        ["cvv"] = ReasonCvvField, ["cvc"] = ReasonCvvField, ["cvv2"] = ReasonCvvField, ["cvc2"] = ReasonCvvField,
        ["csc"] = ReasonCvvField, ["otp"] = ReasonOtpField, ["2fa"] = ReasonOtpField, ["mfa"] = ReasonOtpField,
        ["passcode"] = ReasonOtpField, ["pin"] = ReasonPinField, ["iban"] = ReasonIbanField,
        ["expiry"] = ReasonExpiryField, ["expiration"] = ReasonExpiryField,
    };

    // Matched against the folded text (Arabic letters unified, no diacritics).
    private static readonly (string Phrase, string Reason)[] ArabicKeys = Fold(
    [
        ("رقم البطاقة", ReasonCardField), ("رقم الكارت", ReasonCardField), ("بطاقة الائتمان", ReasonCardField),
        ("بطاقة ائتمان", ReasonCardField), ("رقم بطاقة", ReasonCardField),
        ("رمز الأمان", ReasonCvvField), ("رمز الحماية", ReasonCvvField), ("كود الأمان", ReasonCvvField),
        ("تاريخ الانتهاء", ReasonExpiryField), ("تاريخ انتهاء", ReasonExpiryField), ("صالحة حتى", ReasonExpiryField),
        ("رمز التحقق", ReasonOtpField), ("كود التحقق", ReasonOtpField), ("كود التفعيل", ReasonOtpField),
        ("رمز التفعيل", ReasonOtpField), ("كلمة المرور لمرة واحدة", ReasonOtpField), ("رمز لمرة واحدة", ReasonOtpField),
        ("الرقم السري", ReasonPinField), ("رقم سري", ReasonPinField), ("الرقم التعريفي الشخصي", ReasonPinField),
        ("آيبان", ReasonIbanField), ("رقم الحساب", ReasonIbanField), ("رقم حساب", ReasonIbanField),
    ]);

    private static readonly (string Prefix, string Reason)[] AutocompleteKeys =
    [
        ("cc-number", ReasonCardField), ("cc-csc", ReasonCvvField), ("cc-exp", ReasonExpiryField),
        ("one-time-code", ReasonOtpField),
    ];

    public static (bool Sensitive, IReadOnlyList<string> Reasons) Evaluate(ScreenContext ctx, IReadOnlyList<string> bankDomains)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var reasons = new List<string>();

        var host = ContextBuilder.SiteOf(ctx);
        if (IsBankDomain(host, bankDomains ?? []))
        {
            reasons.Add(ReasonBankDomain);
        }

        bool cardSeen = ContainsCardNumber(ctx.WindowTitle) || ContainsCardNumber(ctx.VisibleText) || ContainsCardNumber(ctx.Selection);
        int visited = 0;
        var stack = new Stack<UiNode>();
        if (ctx.Root is not null)
        {
            stack.Push(ctx.Root);
        }

        if (ctx.Focused is not null)
        {
            stack.Push(ctx.Focused);
        }

        while (stack.Count > 0 && visited < MaxNodes)
        {
            var node = stack.Pop();
            visited++;
            if (IsFieldLike(node) && FieldReason(node) is { } reason && !reasons.Contains(reason))
            {
                reasons.Add(reason);
            }

            if (!cardSeen && (ContainsCardNumber(node.Name) || (!node.IsPassword && ContainsCardNumber(node.Value))))
            {
                cardSeen = true;
            }

            foreach (var child in node.Children)
            {
                if (child is not null)
                {
                    stack.Push(child);
                }
            }
        }

        if (cardSeen)
        {
            reasons.Add(ReasonCardNumber);
        }

        return (reasons.Count > 0, reasons);
    }

    /// <summary>
    /// True when the node is an input field (editable, password, edit or combobox) that asks for a card number,
    /// CVV, expiry, one-time code, PIN or account number. Buttons such as "Pin chat" are not fields.
    /// </summary>
    public static bool IsSensitiveField(UiNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return IsFieldLike(node) && FieldReason(node) is not null;
    }

    /// <summary>Luhn checksum over the digits; spaces and dashes are ignored, Arabic-Indic digits accepted.</summary>
    public static bool IsLuhnValid(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return false;
        }

        Span<char> buffer = stackalloc char[Math.Min(digits.Length, 64)];
        int n = 0;
        foreach (var raw in digits)
        {
            var c = PolicyText.NormalizeDigit(raw);
            if (c is ' ' or '-')
            {
                continue;
            }

            if (!char.IsAsciiDigit(c) || n == buffer.Length)
            {
                return false;
            }

            buffer[n++] = c;
        }

        return Redactor.IsLuhnValid(buffer[..n]);
    }

    internal static string? FieldReason(UiNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.AutocompleteHint))
        {
            var hint = node.AutocompleteHint.ToLowerInvariant();
            foreach (var (prefix, reason) in AutocompleteKeys)
            {
                foreach (var part in hint.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return reason;
                    }
                }
            }
        }

        foreach (var text in new[] { node.Name, node.AutomationId, node.HelpText })
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var compact = PolicyText.Compact(text);
            foreach (var (key, reason) in CompactKeys)
            {
                if (compact.Contains(key, StringComparison.Ordinal))
                {
                    return reason;
                }
            }

            var tokens = PolicyText.Tokens(text);
            foreach (var t in tokens)
            {
                if (TokenKeys.TryGetValue(t, out var reason))
                {
                    return reason;
                }
            }

            if (tokens.Contains("mm") && tokens.Contains("yy"))
            {
                return ReasonExpiryField;
            }

            var folded = PolicyText.Fold(text);
            foreach (var (phrase, reason) in ArabicKeys)
            {
                if (folded.Contains(phrase, StringComparison.Ordinal))
                {
                    return reason;
                }
            }
        }

        return null;
    }

    internal static bool IsBankDomain(string? host, IReadOnlyList<string> bankDomains)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var h = NormalizeHost(host);
        foreach (var raw in bankDomains)
        {
            var d = NormalizeHost(raw);
            if (d.Length > 0 && (h == d || h.EndsWith("." + d, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeHost(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "";
        }

        var s = raw.Trim().ToLowerInvariant();
        int scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            s = s[(scheme + 3)..];
        }

        int cut = s.IndexOfAny(['/', '?', '#', ':']);
        if (cut >= 0)
        {
            s = s[..cut];
        }

        s = s.TrimStart('*').Trim('.');
        return s.StartsWith("www.", StringComparison.Ordinal) ? s[4..] : s;
    }

    private static bool IsFieldLike(UiNode node) =>
        node.IsEditable || node.IsPassword || node.Role is "edit" or "combobox";

    private static bool ContainsCardNumber(string? text) => Redactor.ContainsCard(text);

    private static (string, string)[] Fold((string Phrase, string Reason)[] items) =>
        items.Select(i => (PolicyText.Fold(i.Phrase), i.Reason)).ToArray();
}
