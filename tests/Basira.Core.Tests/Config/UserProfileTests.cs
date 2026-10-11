using System.Text.Json;
using Basira.Core.Common;
using Basira.Core.Config;

namespace Basira.Core.Tests.Config;

public class UserProfileTests
{
    [Fact]
    public void Defaults()
    {
        var p = new UserProfile();

        Assert.Null(p.Name);
        Assert.Equal(Lang.Arabic, p.PreferredLanguage);
        Assert.Null(p.Dialect);
        Assert.Equal(ReplyStyle.SimpleMsa, p.ReplyStyle);
        Assert.Null(p.Studies);
        Assert.Null(p.Work);
        Assert.Empty(p.Apps);
        Assert.Null(p.Needs);
        Assert.Empty(p.TopTasks);
        Assert.False(p.CloudConsentGiven);
        Assert.Null(p.CloudConsentAt);
    }

    [Fact]
    public void RoundTrip_KeepsArabicReadable()
    {
        var original = new UserProfile
        {
            Name = "أبو عمر",
            PreferredLanguage = Lang.Arabic,
            Dialect = "Libyan",
            ReplyStyle = ReplyStyle.Dialect,
            Studies = "التاريخ",
            Work = "معلم متقاعد",
            Apps = ["واتساب", "Chrome", "يوتيوب"],
            Needs = "يحب الاستماع إلى الكتب",
            TopTasks = ["قراءة الأخبار", "الرد على رسائل واتساب", "تشغيل القرآن"],
            CloudConsentGiven = true,
            CloudConsentAt = new DateTimeOffset(2026, 10, 10, 18, 5, 0, TimeSpan.FromHours(2)),
        };

        string json = SettingsSerializer.Serialize(original);
        UserProfile back = SettingsSerializer.DeserializeProfile(json);

        Assert.Equal(original.Name, back.Name);
        Assert.Equal(original.PreferredLanguage, back.PreferredLanguage);
        Assert.Equal(original.Dialect, back.Dialect);
        Assert.Equal(original.ReplyStyle, back.ReplyStyle);
        Assert.Equal(original.Studies, back.Studies);
        Assert.Equal(original.Work, back.Work);
        Assert.Equal(original.Apps, back.Apps);
        Assert.Equal(original.Needs, back.Needs);
        Assert.Equal(original.TopTasks, back.TopTasks);
        Assert.True(back.CloudConsentGiven);
        Assert.Equal(original.CloudConsentAt, back.CloudConsentAt);
        Assert.Equal(json, SettingsSerializer.Serialize(back));

        // Arabic is written as text, not \u escapes, so the family can read and edit the file.
        Assert.Contains("أبو عمر", json);
        Assert.Contains("\"replyStyle\": \"Dialect\"", json);
        Assert.DoesNotContain("\\u06", json);
    }

    [Fact]
    public void MissingFieldsKeepDefaults()
    {
        UserProfile p = SettingsSerializer.DeserializeProfile("""{ "name": "Omar" }""");

        Assert.Equal("Omar", p.Name);
        Assert.Equal(Lang.Arabic, p.PreferredLanguage);
        Assert.Equal(ReplyStyle.SimpleMsa, p.ReplyStyle);
        Assert.NotNull(p.Apps);
        Assert.NotNull(p.TopTasks);
        Assert.False(p.CloudConsentGiven);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("null")]
    public void EmptyInputGivesAnEmptyProfile(string json)
    {
        UserProfile p = SettingsSerializer.DeserializeProfile(json);

        Assert.Null(p.Name);
        Assert.Empty(p.Apps);
        Assert.Empty(p.TopTasks);
    }

    [Fact]
    public void UnknownFieldsAreIgnored()
    {
        UserProfile p = SettingsSerializer.DeserializeProfile("""{ "name": "Omar", "favouriteColour": "blue", "pets": [1] }""");

        Assert.Equal("Omar", p.Name);
    }

    [Fact]
    public void BadValuesAreRepaired()
    {
        const string json = """
            {
              "preferredLanguage": 7,
              "replyStyle": 42,
              "apps": null,
              "topTasks": [" read the news ", "", "   ", "قراءة البريد"]
            }
            """;

        UserProfile p = SettingsSerializer.DeserializeProfile(json);

        Assert.Equal(Lang.Arabic, p.PreferredLanguage);
        Assert.Equal(ReplyStyle.SimpleMsa, p.ReplyStyle);
        Assert.Empty(p.Apps);
        Assert.Equal(["read the news", "قراءة البريد"], p.TopTasks);
    }

    [Fact]
    public void EnglishPreferenceByName()
    {
        UserProfile p = SettingsSerializer.DeserializeProfile("""{ "preferredLanguage": "english", "replyStyle": "dialect" }""");

        Assert.Equal(Lang.English, p.PreferredLanguage);
        Assert.Equal(ReplyStyle.Dialect, p.ReplyStyle);
    }

    [Fact]
    public void MalformedJson()
    {
        Assert.Throws<JsonException>(() => SettingsSerializer.DeserializeProfile("{ \"name\": "));
        Assert.False(SettingsSerializer.TryDeserializeProfile("{ \"name\": ", out UserProfile fallback));
        Assert.Null(fallback.Name);
        Assert.True(SettingsSerializer.TryDeserializeProfile("""{ "name": "Omar" }""", out UserProfile ok));
        Assert.Equal("Omar", ok.Name);
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.Serialize((UserProfile)null!));
        Assert.Throws<ArgumentNullException>(() => SettingsSerializer.DeserializeProfile(null!));
    }

    [Fact]
    public void ListsAreIndependentBetweenInstances()
    {
        var a = new UserProfile();
        var b = new UserProfile();

        a.Apps.Add("Word");

        Assert.Empty(b.Apps);
    }
}
