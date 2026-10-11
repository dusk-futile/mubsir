using Basira.Core.Context;
using Basira.Core.Policy;

namespace Basira.Core.Tests.Policy;

public class SensitiveDetectorTests
{
    private static readonly string[] Banks = ["cib.com.eg", "https://www.alahli.com/", "*.banquemisr.com", "fawry.com"];

    private static UiNode Field(string? name = null, string? id = null, string? autocomplete = null, string? help = null) =>
        new() { Id = 1, Role = "edit", IsEditable = true, Name = name, AutomationId = id, AutocompleteHint = autocomplete, HelpText = help };

    private static ScreenContext Page(string? domain, params UiNode[] nodes) =>
        new() { Domain = domain, IsBrowser = true, Root = new UiNode { Id = 100, Role = "document", Children = nodes } };

    [Theory]
    [InlineData("cib.com.eg")]
    [InlineData("online.cib.com.eg")]
    [InlineData("www.alahli.com")]
    [InlineData("ebank.banquemisr.com")]
    [InlineData("FAWRY.COM")]
    public void BankDomainsTriggerBankMode(string domain)
    {
        var (sensitive, reasons) = SensitiveDetector.Evaluate(Page(domain), Banks);
        Assert.True(sensitive);
        Assert.Contains(SensitiveDetector.ReasonBankDomain, reasons);
    }

    [Theory]
    [InlineData("notcib.com.eg")]
    [InlineData("cib.com.eg.evil.com")]
    [InlineData("youtube.com")]
    public void OtherDomainsDoNot(string domain) => Assert.False(SensitiveDetector.Evaluate(Page(domain), Banks).Sensitive);

    [Fact]
    public void DomainFromUrlWhenDomainIsMissing()
    {
        var ctx = new ScreenContext { Url = "https://online.cib.com.eg/login?x=1", IsBrowser = true };
        Assert.True(SensitiveDetector.Evaluate(ctx, Banks).Sensitive);
    }

    [Theory]
    [InlineData("Card number", null, null, SensitiveDetector.ReasonCardField)]
    [InlineData(null, "cardNumber", null, SensitiveDetector.ReasonCardField)]
    [InlineData(null, "cc_number", null, SensitiveDetector.ReasonCardField)]
    [InlineData(null, null, "cc-number", SensitiveDetector.ReasonCardField)]
    [InlineData(null, null, "section-pay billing cc-number", SensitiveDetector.ReasonCardField)]
    [InlineData("رقم البطاقة", null, null, SensitiveDetector.ReasonCardField)]
    [InlineData("CVV", null, null, SensitiveDetector.ReasonCvvField)]
    [InlineData("CVC2", null, null, SensitiveDetector.ReasonCvvField)]
    [InlineData("Security code", null, null, SensitiveDetector.ReasonCvvField)]
    [InlineData(null, null, "cc-csc", SensitiveDetector.ReasonCvvField)]
    [InlineData("رمز الأمان", null, null, SensitiveDetector.ReasonCvvField)]
    [InlineData("Expiry date", null, null, SensitiveDetector.ReasonExpiryField)]
    [InlineData("MM/YY", null, null, SensitiveDetector.ReasonExpiryField)]
    [InlineData(null, null, "cc-exp-month", SensitiveDetector.ReasonExpiryField)]
    [InlineData("تاريخ الانتهاء", null, null, SensitiveDetector.ReasonExpiryField)]
    [InlineData("One-time code", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData("Verification code", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData("Enter OTP", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData(null, null, "one-time-code", SensitiveDetector.ReasonOtpField)]
    [InlineData("رمز التحقق", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData("كود التفعيل", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData("كلمة المرور لمرة واحدة", null, null, SensitiveDetector.ReasonOtpField)]
    [InlineData(null, null, null, SensitiveDetector.ReasonOtpField, "Enter the 6-digit verification code")]
    [InlineData("PIN", null, null, SensitiveDetector.ReasonPinField)]
    [InlineData(null, "pinInput", null, SensitiveDetector.ReasonPinField)]
    [InlineData("الرقم السري", null, null, SensitiveDetector.ReasonPinField)]
    [InlineData("IBAN", null, null, SensitiveDetector.ReasonIbanField)]
    [InlineData("آيبان", null, null, SensitiveDetector.ReasonIbanField)]
    [InlineData("رقم الحساب", null, null, SensitiveDetector.ReasonIbanField)]
    [InlineData("Account number", null, null, SensitiveDetector.ReasonIbanField)]
    public void SensitiveFieldsAreDetected(string? name, string? id, string? autocomplete, string reason, string? help = null)
    {
        var field = Field(name, id, autocomplete, help);
        Assert.True(SensitiveDetector.IsSensitiveField(field));
        var (sensitive, reasons) = SensitiveDetector.Evaluate(Page("shop.example", field), Banks);
        Assert.True(sensitive);
        Assert.Contains(reason, reasons);
    }

    [Theory]
    [InlineData("Search")]
    [InlineData("Email")]
    [InlineData("Password")]
    [InlineData("Shipping address")]
    [InlineData("Your opinion")]
    [InlineData("Phone number")]
    [InlineData("الاسم")]
    public void OrdinaryFieldsAreNotSensitive(string name)
    {
        var field = Field(name);
        Assert.False(SensitiveDetector.IsSensitiveField(field));
        Assert.False(SensitiveDetector.Evaluate(Page("shop.example", field), Banks).Sensitive);
    }

    [Fact]
    public void ButtonsAreNotFields()
    {
        var pin = new UiNode { Id = 3, Role = "button", Name = "Pin chat" };
        Assert.False(SensitiveDetector.IsSensitiveField(pin));
        Assert.False(SensitiveDetector.Evaluate(Page("web.whatsapp.com", pin), Banks).Sensitive);
    }

    [Fact]
    public void LuhnValidNumberOnScreenTriggersBankMode()
    {
        var ctx = new ScreenContext { AppName = "Notepad", VisibleText = "my card 4111 1111 1111 1111" };
        var (sensitive, reasons) = SensitiveDetector.Evaluate(ctx, Banks);
        Assert.True(sensitive);
        Assert.Equal([SensitiveDetector.ReasonCardNumber], reasons);
    }

    [Fact]
    public void LuhnValidValueInATreeNodeTriggersBankMode()
    {
        var text = new UiNode { Id = 2, Role = "text", Name = "Card ending 5500 0000 0000 0004" };
        Assert.True(SensitiveDetector.Evaluate(Page("shop.example", text), Banks).Sensitive);
    }

    [Fact]
    public void FocusedFieldOutsideTheTreeCounts()
    {
        var ctx = new ScreenContext { Focused = Field("CVV") };
        Assert.True(SensitiveDetector.Evaluate(ctx, Banks).Sensitive);
    }

    [Fact]
    public void ReasonsNeverContainScreenContent()
    {
        var ctx = Page("cib.com.eg", Field("Card number"), new UiNode { Id = 3, Role = "text", Name = "4111 1111 1111 1111" });
        var (_, reasons) = SensitiveDetector.Evaluate(ctx, Banks);
        Assert.All(reasons, r => Assert.DoesNotContain("4111", r));
        Assert.Equal(reasons.Distinct().Count(), reasons.Count);
    }

    [Theory]
    [InlineData("4111 1111 1111 1111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("٤١١١١١١١١١١١١١١١", true)]
    [InlineData("79927398713", true)]
    [InlineData("4111 1111 1111 1112", false)]
    [InlineData("4111a111", false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    public void Luhn(string digits, bool valid) => Assert.Equal(valid, SensitiveDetector.IsLuhnValid(digits));
}
