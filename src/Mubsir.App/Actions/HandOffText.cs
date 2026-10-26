using Mubsir.Core.Common;

namespace Mubsir.App.Actions;

internal static class HandOffText
{
    public static string Build(string? buttonName, string? amount, string? payee, bool isPayment, bool focused, Lang lang)
    {
        bool ar = lang == Lang.Arabic;
        var parts = new List<string>();
        if (buttonName is not null)
        {
            parts.Add(ar ? $"«{buttonName}»" : $"\"{buttonName}\"");
        }

        if (amount is not null && (buttonName is null || !buttonName.Contains(amount, StringComparison.Ordinal)))
        {
            parts.Add(ar ? $"المبلغ {amount}" : $"amount {amount}");
        }

        if (payee is not null)
        {
            parts.Add(ar ? $"إلى {payee}" : $"to {payee}");
        }

        var facts = parts.Count > 0 ? string.Join(ar ? "، " : ", ", parts) + " … " : "";
        if (!focused)
        {
            return facts + (ar
                ? "هذه صفحة بنك أو دفع. لن أضغط هنا، أكمل بنفسك."
                : "This is a bank or payment page. I won't press anything here; please finish it yourself.");
        }

        return facts + (ar
            ? $"الزر جاهز. اضغط إدخال بنفسك {(isPayment ? "للدفع" : "للمتابعة")}."
            : $"The button is ready. Press Enter yourself {(isPayment ? "to pay" : "to continue")}.");
    }
}
