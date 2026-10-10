using System.Text.Json;
using Basira.Core.Common;
using Basira.Core.Config;
using Basira.Core.Input;

namespace Basira.Core.Tests.Config;

public class SettingsSerializerTests
{
    [Fact]
    public void Settings_Defaults()
    {
        var s = new BasiraSettings();

        Assert.Equal("claude-haiku-5-5", s.BrainModel);
        Assert.Null(s.EscalationModel);
        Assert.Equal("gemini-3.5-flash-lite", s.EarsModel);
        Assert.Equal(50, s.SpeechRate);
        Assert.Equal(Verbosity.Brief, s.Verbosity);
        Assert.Equal(KeyIdentity.RightCtrl, s.TalkKey);
        Assert.Equal(8m, s.BudgetSoftCapUsd);
        Assert.False(s.PrivateMode);
        Assert.False(s.BasiraPaused);
        Assert.True(s.UseNvda);
        Assert.Equal("VoiceElement", s.NvdaLangTagStyle);
        Assert.Null(s.ContentLoggingUntil);
        Assert.False(s.SharedPcMode);
        Assert.Equal(15, s.AgentMaxSteps);
        Assert.Equal(180, s.AgentMaxSeconds);
        Assert.Contains("paypal.com", s.BankDomains);
        Assert.Contains("wise.com", s.BankDomains);
        Assert.Contains("fawry.com", s.BankDomains);
        Assert.Contains(s.BankDomains, d => d.StartsWith("instapay", StringComparison.Ordinal));
    }

    [Fact]
    public void Settings_BankDomainsAreAFreshEditableCopy()
    {
        var a = new BasiraSettings();
        var b = new BasiraSettings();

        a.BankDomains.Add("mybank.example");

        Assert.DoesNotContain("mybank.example", b.BankDomains);
        Assert.DoesNotContain("mybank.example", BankDomainDefaults.All);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var original = new BasiraSettings
        {
            BrainModel = "claude-haiku-5-5-20261001",
            EscalationModel = "claude-sonnet-5-5",
            EarsModel = "gemini-3.5-flash-lite",
            SpeechRate = 72,
            Verbosity = Verbosity.Detailed,
            TalkKey = KeyIdentity.Apps,
            BudgetSoftCapUsd = 5.5m,
            PrivateMode = true,
            BasiraPaused = true,
            BankDomains = ["nbe.com.eg", "mybank.ly"],
            UseNvda = false,
            NvdaLangTagStyle = "LangAttribute",
            ContentLoggingUntil = new DateTimeOffset(2026, 10, 11, 9, 30, 0, TimeSpan.FromHours(3)),
            SharedPcMode = true,
            AgentMaxSteps = 20,
            AgentMaxSeconds = 240,
        };

        string json = SettingsSerializer.Serialize(original);
        BasiraSettings back = SettingsSerializer.DeserializeSettings(json);

        Assert.Equal(original.BrainModel, back.BrainModel);
        Assert.Equal(original.EscalationModel, back.EscalationModel);
        Assert.Equal(original.SpeechRate, back.SpeechRate);
        Assert.Equal(original.Verbosity, back.Verbosity);
        Assert.Equal(original.TalkKey, back.TalkKey);
        Assert.Equal(original.BudgetSoftCapUsd, back.BudgetSoftCapUsd);
        Assert.True(back.PrivateMode);
        Assert.True(back.BasiraPaused);
        Assert.Equal(original.BankDomains, back.BankDomains);
        Assert.False(back.UseNvda);
        Assert.Equal("LangAttribute", back.NvdaLangTagStyle);
        Assert.Equal(original.ContentLoggingUntil, back.ContentLoggingUntil);
        Assert.True(back.SharedPcMode);
        Assert.Equal(20, back.AgentMaxSteps);
        Assert.Equal(240, back.AgentMaxSeconds);
        Assert.Equal(json, SettingsSerializer.Serialize(back));
    }

    [Fact]
    public void Settings_JsonIsReadable()
    {
        string json = SettingsSerializer.Serialize(new BasiraSettings());

        Assert.Contains("\"brainModel\": \"claude-haiku-5-5\"", json);
        Assert.Contains("\"verbosity\": \"Brief\"", json);
        Assert.Contains("\"speechRate\": 50", json);

        // The talk key is stored as its three codes only, not its computed helpers.
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement key = doc.RootElement.GetProperty("talkKey");
        Assert.Equal(["virtualKey", "scanCode", "extended"], key.EnumerateObject().Select(p => p.Name));
        Assert.Equal(0xA3, key.GetProperty("virtualKey").GetInt32());
        Assert.Equal(0x1D, key.GetProperty("scanCode").GetInt32());
        Assert.True(key.GetProperty("extended").GetBoolean());
    }

    [Fact]
    public void Settings_MissingFieldsKeepDefaults()
    {
        BasiraSettings s = SettingsSerializer.DeserializeSettings("""{ "speechRate": 65 }""");

        Assert.Equal(65, s.SpeechRate);
        Assert.Equal("claude-haiku-5-5", s.BrainModel);
        Assert.Equal(KeyIdentity.RightCtrl, s.TalkKey);
        Assert.Equal(8m, s.BudgetSoftCapUsd);
        Assert.True(s.UseNvda);
        Assert.Equal(BankDomainDefaults.All, s.BankDomains);
        Assert.Equal(15, s.AgentMaxSteps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Settings_EmptyInputGivesDefaults(string json)
    {
        BasiraSettings s = SettingsSerializer.DeserializeSettings(json);

        Assert.Equal(SettingsSerializer.Serialize(new BasiraSettings()), SettingsSerializer.Serialize(s));
    }

    [Fact]
    public void Settings_UnknownFieldsAreIgnored()
    {
        const string json = """
            {
              "speechRate": 40,
              "someFutureSetting": { "nested": [1, 2, 3] },
              "theme": "dark",
              "talkKey": { "virtualKey": 93, "scanCode": 93, "extended": true, "isModifier": false, "label": "Apps" }
            }
            """;

        BasiraSettings s = SettingsSerializer.DeserializeSettings(json);

        Assert.Equal(40, s.SpeechRate);
        Assert.Equal(KeyIdentity.Apps, s.TalkKey);
    }

    [Fact]
    public void Settings_ToleratesCommentsTrailingCommasAndAnyCase()
    {
        const string json = """
            {
              // edited by hand
              "SpeechRate": 30,
              "VERBOSITY": "normal",
              "bankDomains": ["a.example", "b.example",],
            }
            """;

        BasiraSettings s = SettingsSerializer.DeserializeSettings(json);

        Assert.Equal(30, s.SpeechRate);
        Assert.Equal(Verbosity.Normal, s.Verbosity);
        Assert.Equal(["a.example", "b.example"], s.BankDomains);
    }

    [Fact]
    public void Settings_OutOfRangeValuesAreRepaired()
    {
        const string json = """
            {
              "brainModel": "  ",
              "escalationModel": "",
              "earsModel": null,
              "speechRate": 250,
              "verbosity": 9,
              "talkKey": { "virtualKey": 0, "scanCode": 0, "extended": false },
              "budgetSoftCapUsd": -3,
              "bankDomains": [" NBE.com.eg. ", "", "nbe.com.eg", "  "],
              "nvdaLangTagStyle": "",
              "agentMaxSteps": 0,
              "agentMaxSeconds": -5
            }
            """;

        BasiraSettings s = SettingsSerializer.DeserializeSettings(json);

        Assert.Equal("claude-haiku-5-5", s.BrainModel);
        Assert.Null(s.EscalationModel);
        Assert.Equal("gemini-3.5-flash-lite", s.EarsModel);
        Assert.Equal(100, s.SpeechRate);
        Assert.Equal(Verbosity.Brief, s.Verbosity);
        Assert.Equal(KeyIdentity.RightCtrl, s.TalkKey);
        Assert.Equal(0m, s.BudgetSoftCapUsd);
        Assert.Equal(["nbe.com.eg"], s.BankDomains);
        Assert.Equal("VoiceElement", s.NvdaLangTagStyle);
        Assert.Equal(15, s.AgentMaxSteps);
        Assert.Equal(180, s.AgentMaxSeconds);
    }

    [Fact]
    public void Settings_NegativeSpeechRateIsClampedToZero()
    {
        Assert.Equal(0, SettingsSerializer.DeserializeSettings("""{ "speechRate": -10 }""").SpeechRate);
    }

    [Fact]
    public void Settings_NullBankDomainsGetDefaults_EmptyListStaysEmpty()
    {
        Assert.Equal(BankDomainDefaults.All, SettingsSerializer.DeserializeSettings("""{ "bankDomains": null }""").BankDomains);
        Assert.Empty(SettingsSerializer.DeserializeSettings("""{ "bankDomains": [] }""").BankDomains);
    }

    [Fact]
    public void Settings_NumericEnumValuesAreAccepted()
    {
        Assert.Equal(Verbosity.Detailed, SettingsSerializer.DeserializeSettings("""{ "verbosity": 2 }""").Verbosity);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"speechRate\": ")]
    [InlineData("{ \"speechRate\": \"fast\" }")]
    [InlineData("[1, 2]")]
    public void Settings_MalformedJsonThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() => SettingsSerializer.DeserializeSettings(json));
    }

    [Fact]
    public void Settings_TryDeserialize_FallsBackToDefaults()
    {
        Assert.False(SettingsSerializer.TryDeserializeSettings("not json", out BasiraSettings bad));
        Assert.Equal(50, bad.SpeechRate);

        Assert.True(SettingsSerializer.TryDeserializeSettings("""{ "speechRate": 61 }""", out BasiraSettings good));
        Assert.Equal(61, good.SpeechRate);

        Assert.True(SettingsSerializer.TryDeserializeSettings(null, out BasiraSettings none));
        Assert.Equal(50, none.SpeechRate);
    }

    [Fact]
    public void Settings_NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.Serialize((BasiraSettings)null!));
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.DeserializeSettings(null!));
    }

    [Fact]
    public void ContentLogging_ExpiresAtItsTime()
    {
        var now = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

        Assert.False(new BasiraSettings().IsContentLoggingOn(now));
        Assert.True(new BasiraSettings { ContentLoggingUntil = now.AddHours(24) }.IsContentLoggingOn(now));
        Assert.False(new BasiraSettings { ContentLoggingUntil = now }.IsContentLoggingOn(now));
        Assert.False(new BasiraSettings { ContentLoggingUntil = now.AddMinutes(-1) }.IsContentLoggingOn(now));
    }

    [Fact]
    public void Settings_WithExpressionLeavesTheOriginalUnchanged()
    {
        var a = new BasiraSettings();
        BasiraSettings b = a with { PrivateMode = true, SpeechRate = 80 };

        Assert.False(a.PrivateMode);
        Assert.Equal(50, a.SpeechRate);
        Assert.True(b.PrivateMode);
        Assert.Equal(80, b.SpeechRate);
    }
}
