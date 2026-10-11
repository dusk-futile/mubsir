namespace Mubsir.Core.Config;

public static class BankDomainDefaults
{
    public static IReadOnlyList<string> All { get; } =
    [

        "nbe.com.eg", "banquemisr.com", "cibeg.com", "qnbalahli.com", "qnb.com.eg", "alexbank.com", "aaib.com",
        "banqueducaire.com", "hsbc.com.eg", "faisalbank.com.eg", "adib.eg", "fawry.com", "instapay.eg", "ipn.eg",
        "paymob.com", "digital.gov.eg",

        "alrajhibank.com.sa", "alahli.com", "riyadbank.com", "sab.com", "alinma.com", "bankalbilad.com",
        "bankaljazira.com", "anb.com.sa", "alfransi.com.sa", "stcpay.com.sa", "sadad.com", "absher.sa", "my.gov.sa",

        "cbl.gov.ly", "bcd.ly", "jumhouriabank.ly", "wahdabank.ly", "wahdabank.com.ly", "saharabank.com.ly", "ncb.ly",
        "amanbank.ly", "moamalat.net", "sadad.ly",

        "emiratesnbd.com", "adcb.com", "bankfab.com", "mashreq.com", "dib.ae", "adib.ae", "rakbank.ae", "cbd.ae",
        "uaepass.ae",

        "paypal.com", "wise.com", "payoneer.com", "westernunion.com", "moneygram.com", "skrill.com",
        "checkout.stripe.com",
    ];

    public static List<string> Create() => [.. All];

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
