using System.Text.RegularExpressions;
using Basira.Core.Context;
using Basira.Core.Text;

namespace Basira.App.Actions;

/// <summary>
/// Pure, best-effort heuristics over a UIA snapshot that pull the facts a confirmation needs (recipient, message
/// preview, amount) from the screen itself, never from the model. Every result may be null.
/// </summary>
internal static partial class UiFacts
{
    public const int BodyWords = 15;

    private static readonly string[] SendWords =
    [
        "send", "pay", "confirm", "submit", "buy", "purchase", "transfer", "post", "place order", "checkout", "reply",
        "ارسال", "ارسل", "دفع", "ادفع", "تاكيد", "اكد", "شراء", "اشتر", "تحويل", "حول", "نشر", "انشر", "اتمام", "متابعه الدفع", "رد",
    ];

    private static readonly string[] PayWords =
    [
        "pay", "buy", "purchase", "transfer", "checkout", "place order", "دفع", "ادفع", "شراء", "اشتر", "تحويل", "حول", "سداد",
    ];

    private static readonly string[] RecipientLabels =
    [
        "to", "recipients", "recipient", "to recipients", "الى", "المستلم", "المستلمون", "المرسل اليه",
    ];

    private static readonly string[] HeaderIds = ["title", "header", "conversation", "chatname", "contact"];

    /// <summary>A button-like element whose name sends, pays, confirms or submits.</summary>
    public static bool IsSendLike(UiNode? node)
    {
        if (node is null || node.Role is not ("button" or "menuitem" or "link" or "other"))
        {
            return false;
        }

        return ContainsAny(node.Name, SendWords) || ContainsAny(node.AutomationId, SendWords) || ContainsAny(node.HelpText, SendWords);
    }

    public static bool IsPayLike(UiNode? node) =>
        node is not null && (ContainsAny(node.Name, PayWords) || ContainsAny(node.AutomationId, PayWords));

    /// <summary>An editable message or form box (not search, address bar or password).</summary>
    public static bool IsComposeBox(UiNode? node) =>
        node is { IsEditable: true, IsPassword: false, IsSearchBox: false, IsAddressBar: false };

    /// <summary>Fills "recipient", "body" and "amount" from the snapshot for a send/pay target or a compose box.</summary>
    public static void AddFacts(IDictionary<string, string?> args, UiNode? target, ScreenContext? screen)
    {
        if (screen is null || target is null || !(IsSendLike(target) || IsComposeBox(target)))
        {
            return;
        }

        var flat = Flatten(screen.Root);
        if (Recipient(screen, flat) is { } recipient)
        {
            args["recipient"] = recipient;
        }

        var compose = IsComposeBox(target) ? target : NearestCompose(flat, target) ?? (IsComposeBox(screen.Focused) ? screen.Focused : null);
        if (Body(compose) is { } body)
        {
            args["body"] = body;
        }

        if (IsPayLike(target) || ContainsCurrency(target.Name))
        {
            if (Amount(flat, target) is { } amount)
            {
                args["amount"] = amount;
            }
        }
    }

    /// <summary>Who a message goes to: a "To" field's value or chips, else a chat header.</summary>
    public static string? Recipient(ScreenContext screen, IReadOnlyList<UiNode> flat)
    {
        foreach (var n in flat)
        {
            if (n.IsPassword || !IsLabel(n.Name, RecipientLabels))
            {
                continue;
            }

            if (Clean(n.Value) is { } v)
            {
                return Short(v, 80);
            }

            var chips = n.Children.Where(c => c.Role is "listitem" or "button" or "text").Select(c => Clean(c.Name)).OfType<string>().Take(3).ToList();
            if (chips.Count > 0)
            {
                return Short(string.Join(", ", chips), 80);
            }
        }

        // Chat apps (WhatsApp, Telegram, Messenger): the conversation header near the top.
        foreach (var n in flat)
        {
            if (n.IsPassword || n.Role is "edit" || Clean(n.Name) is not { } name)
            {
                continue;
            }

            var id = (n.AutomationId ?? "").ToLowerInvariant();
            if (HeaderIds.Any(h => id.Contains(h, StringComparison.Ordinal)) && !IsLabel(name, SendWords))
            {
                return Short(name, 80);
            }
        }

        return null;
    }

    /// <summary>The first <see cref="BodyWords"/> words of a compose box (never a password).</summary>
    public static string? Body(UiNode? compose)
    {
        if (compose is null || compose.IsPassword || Clean(compose.Value) is not { } text)
        {
            return null;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var preview = string.Join(' ', words.Take(BodyWords));
        return words.Length > BodyWords ? preview + " …" : preview;
    }

    /// <summary>A money amount: in the target's own name, else the currency text closest to it in reading order.</summary>
    public static string? Amount(IReadOnlyList<UiNode> flat, UiNode target)
    {
        if (FindCurrency(target.Name) is { } own)
        {
            return own;
        }

        int index = IndexOf(flat, target);
        string? best = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < flat.Count; i++)
        {
            var n = flat[i];
            if (n.IsPassword)
            {
                continue;
            }

            var found = FindCurrency(n.Name) ?? FindCurrency(n.Value);
            if (found is null)
            {
                continue;
            }

            int d = index < 0 ? i : Math.Abs(i - index);
            if (d < bestDistance)
            {
                best = found;
                bestDistance = d;
            }
        }

        return best;
    }

    public static bool ContainsCurrency(string? s) => FindCurrency(s) is not null;

    internal static string? FindCurrency(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 400)
        {
            return null;
        }

        try
        {
            var m = CurrencyAfter().Match(s);
            if (!m.Success)
            {
                m = CurrencyBefore().Match(s);
            }

            return m.Success ? Short(m.Value.Trim(), 40) : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>Pre-order list of the snapshot, i.e. reading order.</summary>
    public static List<UiNode> Flatten(UiNode? root)
    {
        var list = new List<UiNode>();
        if (root is null)
        {
            return list;
        }

        var stack = new Stack<UiNode>();
        stack.Push(root);
        while (stack.Count > 0 && list.Count < 5000)
        {
            var n = stack.Pop();
            list.Add(n);
            for (int i = n.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(n.Children[i]);
            }
        }

        return list;
    }

    private static UiNode? NearestCompose(IReadOnlyList<UiNode> flat, UiNode target)
    {
        int index = IndexOf(flat, target);
        UiNode? best = null;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < flat.Count; i++)
        {
            if (!IsComposeBox(flat[i]))
            {
                continue;
            }

            int d = index < 0 ? i : Math.Abs(i - index);
            if (d < bestDistance)
            {
                best = flat[i];
                bestDistance = d;
            }
        }

        return best;
    }

    private static int IndexOf(IReadOnlyList<UiNode> flat, UiNode target)
    {
        for (int i = 0; i < flat.Count; i++)
        {
            if (ReferenceEquals(flat[i], target) || (flat[i].Id == target.Id && target.Id != 0))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsAny(string? text, string[] words)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var folded = " " + FuzzyMatch.Fold(text) + " ";
        foreach (var w in words)
        {
            var fw = FuzzyMatch.Fold(w);
            if (fw.Length > 0 && (folded.Contains(" " + fw + " ", StringComparison.Ordinal)
                || (fw.Length >= 4 && folded.Contains(fw, StringComparison.Ordinal))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLabel(string? name, string[] labels)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var folded = ArabicFolding.Fold(name).TrimEnd(':', ' ');
        return labels.Any(l => folded == ArabicFolding.Fold(l));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Short(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";

    // Digits (Latin or Arabic-Indic) with separators, then a currency word or symbol: "250 EGP", "٢٥٠ جنيه", "99.5 ر.س".
    [GeneratedRegex(@"[0-9٠-٩][0-9٠-٩,.٫٬ ]*\s?(EGP|USD|SAR|AED|KWD|LYD|QAR|BHD|OMR|JOD|MAD|TND|DZD|EUR|GBP|LE|L\.E\.?|جنيه(ا|ات)?|ج\.م\.?|ج\.م|ريال|ر\.س\.?|درهم|د\.إ\.?|دينار|د\.ل\.?|دولار|يورو|\$|€|£)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex CurrencyAfter();

    // Symbol or code first: "$25", "EGP 250", "SAR 99.50".
    [GeneratedRegex(@"(EGP|USD|SAR|AED|KWD|LYD|EUR|GBP|\$|€|£)\s?[0-9٠-٩][0-9٠-٩,.٫٬]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex CurrencyBefore();
}
