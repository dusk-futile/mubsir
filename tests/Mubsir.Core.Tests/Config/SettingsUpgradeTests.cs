using Mubsir.Core.Config;

namespace Mubsir.Core.Tests.Config;

public sealed class SettingsUpgradeTests
{
    [Fact]
    public void Settings_saved_by_an_older_version_still_load()
    {
        // Written before Hudhud and the Sonnet option were removed.
        const string old = """
            {
              "showNotch": false,
              "showHudhud": true,
              "brainModel": "claude-sonnet-5-5",
              "escalationModel": "claude-sonnet-5-5",
              "speechRate": 70
            }
            """;

        MubsirSettings settings = SettingsSerializer.DeserializeSettings(old);

        Assert.False(settings.ShowNotch);
        Assert.Equal(70, settings.SpeechRate);
        Assert.DoesNotContain("sonnet", SettingsSerializer.Serialize(settings), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hudhud", SettingsSerializer.Serialize(settings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void One_voice_per_answer_is_off_until_chosen_because_it_costs()
    {
        Assert.False(new MubsirSettings().OneVoicePerAnswer);
        Assert.True(SettingsSerializer.DeserializeSettings("{\"oneVoicePerAnswer\": true}").OneVoicePerAnswer);
    }
}
