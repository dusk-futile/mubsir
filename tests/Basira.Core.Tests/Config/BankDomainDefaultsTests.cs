using Basira.Core.Config;

namespace Basira.Core.Tests.Config;

public class BankDomainDefaultsTests
{
    [Fact]
    public void StarterListCoversTheFamilysRegionsAndPaymentSites()
    {
        IReadOnlyList<string> all = BankDomainDefaults.All;

        Assert.Contains("nbe.com.eg", all);       // Egypt
        Assert.Contains("alrajhibank.com.sa", all); // Saudi Arabia
        Assert.Contains("jumhouriabank.ly", all); // Libya
        Assert.Contains("emiratesnbd.com", all);  // UAE
        Assert.Contains("paypal.com", all);
        Assert.Contains("wise.com", all);
        Assert.Contains("fawry.com", all);
        Assert.Contains("instapay.eg", all);
    }

    [Fact]
    public void EntriesAreCleanLowercaseDomains_WithoutDuplicates()
    {
        foreach (string d in BankDomainDefaults.All)
        {
            Assert.Equal(d.Trim().ToLowerInvariant(), d);
            Assert.Contains('.', d);
            Assert.DoesNotContain("/", d);
            Assert.DoesNotContain(":", d);
            Assert.False(d.StartsWith("www.", StringComparison.Ordinal), d);
        }

        Assert.Equal(BankDomainDefaults.All.Count, BankDomainDefaults.All.Distinct().Count());
    }

    [Theory]
    [InlineData("nbe.com.eg")]
    [InlineData("www.nbe.com.eg")]
    [InlineData("ebank.nbe.com.eg")]
    [InlineData("WWW.PayPal.com")]
    [InlineData("paypal.com.")]
    [InlineData(" wise.com ")]
    public void Matches_DomainAndSubdomains(string host)
    {
        Assert.True(BankDomainDefaults.Matches(BankDomainDefaults.All, host));
    }

    [Theory]
    [InlineData("notpaypal.com")]
    [InlineData("paypal.com.evil.example")]
    [InlineData("google.com")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("com")]
    public void Matches_RejectsLookalikesAndOtherSites(string? host)
    {
        Assert.False(BankDomainDefaults.Matches(BankDomainDefaults.All, host));
    }

    [Fact]
    public void Matches_UsesTheEditedList()
    {
        List<string> mine = ["mybank.example", " ", ""];

        Assert.True(BankDomainDefaults.Matches(mine, "online.mybank.example"));
        Assert.False(BankDomainDefaults.Matches(mine, "paypal.com"));
        Assert.Throws<ArgumentNullException>(() => BankDomainDefaults.Matches(null!, "x.com"));
    }

    [Fact]
    public void Create_ReturnsAnIndependentCopy()
    {
        List<string> a = BankDomainDefaults.Create();
        a.Clear();

        Assert.NotEmpty(BankDomainDefaults.Create());
        Assert.NotEmpty(BankDomainDefaults.All);
    }
}
