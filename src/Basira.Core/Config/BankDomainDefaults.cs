namespace Basira.Core.Config;

/// <summary>
/// Starter list of bank, payment, wallet and government sites that switch on bank mode.
/// Entries are domain suffixes ("nbe.com.eg" also covers "ebank.nbe.com.eg"). The family edits
/// this list for the user's own banks; an entry that never matches is harmless.
/// </summary>
public static class BankDomainDefaults
{
    public static IReadOnlyList<string> All { get; } =
    [
        // Egypt
        "nbe.com.eg", "banquemisr.com", "cibeg.com", "qnbalahli.com", "qnb.com.eg", "alexbank.com", "aaib.com",
        "banqueducaire.com", "hsbc.com.eg", "faisalbank.com.eg", "adib.eg", "fawry.com", "instapay.eg", "ipn.eg",
        "paymob.com", "digital.gov.eg",

        // Saudi Arabia
        "alrajhibank.com.sa", "alahli.com", "riyadbank.com", "sab.com", "alinma.com", "bankalbilad.com",
        "bankaljazira.com", "anb.com.sa", "alfransi.com.sa", "stcpay.com.sa", "sadad.com", "absher.sa",

        // Libya
        "cbl.gov.ly", "bcd.ly", "jumhouriabank.ly", "wahdabank.ly", "saharabank.com.ly", "ncb.ly", "amanbank.ly",

        // United Arab Emirates
        "emiratesnbd.com", "adcb.com", "bankfab.com", "mashreq.com", "dib.ae", "adib.ae", "rakbank.ae", "cbd.ae",

        // International payments
        "paypal.com", "wise.com", "payoneer.com", "westernunion.com", "moneygram.com", "skrill.com",
        "checkout.stripe.com",
    ];

    /// <summary>A fresh, editable copy of <see cref="All"/>.</summary>
    public static List<string> Create() => [.. All];

    /// <summary>Whether <paramref name="host"/> equals or is a subdomain of an entry in <paramref name="domains"/>.</summary>
    public static bool Matches(IEnumerable<string> domains, string? host)
    {
        ArgumentNullException.ThrowIfNull(domains);
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        string h = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (string entry in domains)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            string d = entry.Trim().TrimEnd('.').ToLowerInvariant();
            if (h == d || (h.Length > d.Length && h.EndsWith(d, StringComparison.Ordinal) && h[h.Length - d.Length - 1] == '.'))
            {
                return true;
            }
        }

        return false;
    }
}
