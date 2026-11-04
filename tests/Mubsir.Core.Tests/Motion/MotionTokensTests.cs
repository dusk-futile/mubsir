using Mubsir.Core.Motion;

namespace Mubsir.Core.Tests.Motion;

public class MotionTokensTests
{
    private static string TokensJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "motion-tokens.json"));

    [Fact]
    public void The_overlay_tokens_load_with_every_state()
    {
        MotionTokens tokens = MotionTokens.Parse(TokensJson());

        foreach (CompanionState state in Enum.GetValues<CompanionState>())
        {
            Assert.True(tokens.NotchSizes.ContainsKey(state), "no size for " + state);
        }

        Assert.Equal(new Size2(120, 28), tokens.NotchSizes[CompanionState.Idle]);
        Assert.Equal(new Size2(424, 172), tokens.NotchSizes[CompanionState.Doing]);
        Assert.Equal(new SpringToken(0.52, 0.72), tokens.Spring("notchWidth"));
        Assert.Equal(new SpringToken(0.32, 1.0), tokens.Spring("content"));
        Assert.Equal(new Size2(92, 100), tokens.Hudhud.Size);
        Assert.Equal(20, tokens.Hudhud.SleepAfterSeconds);
        Assert.Equal(0.32, tokens.Hudhud.Pose("idle").Crest);
        Assert.Equal(-0.22, tokens.Hudhud.Pose("sleep").Crest);
        Assert.Equal("#F3C343", tokens.Colors["amber"]);
    }

    [Fact]
    public void A_state_without_a_pose_uses_idle()
    {
        MotionTokens tokens = MotionTokens.Parse(TokensJson());

        Assert.Equal(tokens.Hudhud.Pose("idle"), tokens.Hudhud.Pose("no-such-state"));
    }

    [Fact]
    public void A_missing_notch_size_is_rejected()
    {
        string json = TokensJson().Replace("\"undone\": [372, 48]", "\"unused\": [1, 1]", StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => MotionTokens.Parse(json));
    }

    [Fact]
    public void A_bad_colour_is_rejected()
    {
        string json = TokensJson().Replace("\"#F3C343\"", "\"amber\"", StringComparison.Ordinal);

        Assert.Throws<FormatException>(() => MotionTokens.Parse(json));
    }

    [Theory]
    [InlineData("listening", CompanionState.Listening)]
    [InlineData("CONFIRM", CompanionState.Confirm)]
    [InlineData("undone", CompanionState.Undone)]
    public void State_names_round_trip(string key, CompanionState expected)
    {
        Assert.True(CompanionStates.TryParse(key, out CompanionState state));
        Assert.Equal(expected, state);
        Assert.Equal(key.ToLowerInvariant(), state.Key());
    }
}
