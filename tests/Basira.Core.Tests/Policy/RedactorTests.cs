using Basira.Core.Policy;

namespace Basira.Core.Tests.Policy;

public class RedactorTests
{
    [Theory]
    [InlineData("Card: 4111 1111 1111 1111 thanks", "Card: [card] thanks")]
    [InlineData("4111-1111-1111-1111", "[card]")]
    [InlineData("4111111111111111", "[card]")]
    [InlineData("MasterCard 5500 0000 0000 0004.", "MasterCard [card].")]
    [InlineData("Amex 3782 822463 10005 expires soon", "Amex [card] expires soon")]
    [InlineData("Visa 4242424242424242, exp 12/27", "Visa [card], exp 12/27")]
    [InlineData("رقم البطاقة ٤١١١ ١١١١ ١١١١ ١١١١", "رقم البطاقة [card]")]
    [InlineData("Order 12 4111 1111 1111 1111", "Order 12 [card]")]
    public void RedactsLuhnValidCards(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Theory]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("1234567890123")]
    [InlineData("Years 2019 2020 2021 2022")]
    public void KeepsNumbersThatAreNotCards(string input) => Assert.Equal(input, Redactor.Redact(input));

    [Theory]
    [InlineData("IBAN: EG380019000500000000263180002", "IBAN: [iban]")]
    [InlineData("GB82 WEST 1234 5698 7654 32 please", "[iban] please")]
    [InlineData("DE89 3704 0044 0532 0130 00", "[iban]")]
    [InlineData("آيبان SA0380000000608010167519 شكرا", "آيبان [iban] شكرا")]
    [InlineData("ae070331234567890123456", "[iban]")]
    public void RedactsValidIbans(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Fact]
    public void IbanWithWrongChecksumIsNotAnIban()
    {
        Assert.DoesNotContain("[iban]", Redactor.Redact("GB83 WEST 1234 5698 7654 32"));
        Assert.DoesNotContain("[iban]", Redactor.Redact("AB12 CDEF GHIJ KLMN OPQR"));
    }

    [Theory]
    [InlineData("Your verification code is 482913", "Your verification code is [code]")]
    [InlineData("G-482913 is your Google verification code.", "G-[code] is your Google verification code.")]
    [InlineData("Your OTP: 1234", "Your OTP: [code]")]
    [InlineData("PIN 4821", "PIN [code]")]
    [InlineData("Enter code 123 456 to continue", "Enter code [code] to continue")]
    [InlineData("CVV 123", "CVV [code]")]
    [InlineData("رمز التحقق الخاص بك هو ٤٨٢٩١٣", "رمز التحقق الخاص بك هو [code]")]
    [InlineData("كود التفعيل: 5521", "كود التفعيل: [code]")]
    [InlineData("كلمة المرور المؤقتة 99887766", "كلمة المرور المؤقتة [code]")]
    [InlineData("482913 هو الرمز الخاص بك", "[code] هو الرمز الخاص بك")]
    public void RedactsCodesNearCodeWords(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Theory]
    [InlineData("Order 4821 shipped")]
    [InlineData("Promo code SAVE20 gives 1500 EGP off")]
    [InlineData("Enter the code to get $1500")]
    [InlineData("Code coverage is 85%")]
    [InlineData("Shipping takes 3 days, opinion poll 2024")]
    public void KeepsNumbersAwayFromCodeWordsOrWithCurrency(string input) => Assert.Equal(input, Redactor.Redact(input));

    [Theory]
    [InlineData("Call 01012345678 now", "Call [phone] now")]
    [InlineData("Call 010 1234 5678", "Call [phone]")]
    [InlineData("Call 0101-234-5678", "Call [phone]")]
    [InlineData("+20 101 234 5678", "[phone]")]
    [InlineData("(+20) 101 234 5678", "[phone]")]
    [InlineData("+966 50 123 4567", "[phone]")]
    [InlineData("+218 91 234 5678", "[phone]")]
    [InlineData("+971 50 123 4567", "[phone]")]
    [InlineData("+212 6 12 34 56 78", "[phone]")]
    [InlineData("+962 7 9123 4567", "[phone]")]
    [InlineData("+970 59 123 4567", "[phone]")]
    [InlineData("00201012345678", "[phone]")]
    [InlineData("جوال 0551234567", "جوال [phone]")]
    [InlineData("055 123 4567", "[phone]")]
    [InlineData("091-234-5678", "[phone]")]
    [InlineData("اتصل على ٠١٠١٢٣٤٥٦٧٨", "اتصل على [phone]")]
    [InlineData("WhatsApp +201012345678+", "WhatsApp [phone]+")]
    public void RedactsPhones(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Theory]
    [InlineData("write to ahmed.ali@example.com today", "write to [email] today")]
    [InlineData("Contact: test_user+tag@mail.co.uk.", "Contact: [email].")]
    [InlineData("البريد: Omar.K@Gmail.COM", "البريد: [email]")]
    public void RedactsEmails(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Theory]
    [InlineData("الرقم القومي 29001011234567", "الرقم القومي [id]")]
    [InlineData("ID 30105150101234.", "ID [id].")]
    [InlineData("٢٩٠٠١٠١١٢٣٤٥٦٧", "[id]")]
    public void RedactsEgyptianNationalIds(string input, string expected) => Assert.Equal(expected, Redactor.Redact(input));

    [Theory]
    [InlineData("In 2026 the price was 250 EGP")]
    [InlineData("Meeting at 10:30 on 11/10/2026")]
    [InlineData("Page 12 of 300")]
    [InlineData("Version 10.0.19045.3803")]
    [InlineData("Windows 10 build 19045")]
    [InlineData("Total: 1,250,000 EGP")]
    [InlineData("Total: 1,234,567,890,123 EGP")]
    [InlineData("pi is 3.14159265358979")]
    [InlineData("Order #12345678")]
    [InlineData("السعر ١٥٠٠ جنيه")]
    [InlineData("صفحة ١٢ من ٣٠٠")]
    [InlineData("الساعة ١٠:٣٠")]
    [InlineData("0 5 10 15 20 25")]
    [InlineData("from 01-12-2026 to 05-12-2026")]
    [InlineData("Chapter 3, page 45, 2nd edition")]
    public void KeepsOrdinaryNumbers(string input) => Assert.Equal(input, Redactor.Redact(input));

    [Fact]
    public void HandlesSeveralKindsInOneText()
    {
        var input = "Send 4111111111111111 to ahmed@x.com or call +201012345678, code 778899.";
        Assert.Equal("Send [card] to [email] or call [phone], code [code].", Redactor.Redact(input));
    }

    [Fact]
    public void ReplacesTheOriginalArabicIndicSpan()
    {
        var input = "هاتف ٠١٠١٢٣٤٥٦٧٨ والسعر ٢٥٠ جنيه";
        Assert.Equal("هاتف [phone] والسعر ٢٥٠ جنيه", Redactor.Redact(input));
    }

    [Fact]
    public void IsIdempotent()
    {
        var once = Redactor.Redact("Card 4111 1111 1111 1111, OTP 4821, mail a@b.co, phone 01012345678");
        Assert.Equal(once, Redactor.Redact(once));
    }

    [Fact]
    public void EmptyInput() => Assert.Equal("", Redactor.Redact(""));

    [Fact]
    public void LongTextWithManyNumbersIsFast()
    {
        var text = string.Concat(Enumerable.Range(0, 5000).Select(i => $"{i} {i * 7} {i * 13} "));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Redactor.Redact(text);
        Assert.True(sw.ElapsedMilliseconds < 2000, sw.ElapsedMilliseconds + " ms");
    }

    [Fact]
    public void CardOrIbanDetectionHelpers()
    {
        Assert.True(Redactor.ContainsCardOrIban("pay with 4111 1111 1111 1111"));
        Assert.True(Redactor.ContainsCardOrIban("GB82WEST12345698765432"));
        Assert.False(Redactor.ContainsCardOrIban("call 01012345678"));
        Assert.False(Redactor.ContainsCardOrIban(null));
    }
}
