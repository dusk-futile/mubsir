using Mubsir.Core.Motion;

namespace Mubsir.Core.Tests.Motion;

public class NotchCurvesTests
{
    private static MotionTokens Tokens() =>
        MotionTokens.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "motion-tokens.json")));

    private static double Peak(Spring spring)
    {
        double peak = spring.Value;
        for (int i = 0; i < 2000 && spring.Step(1.0 / 60); i++)
        {
            peak = Math.Max(peak, spring.Value);
        }

        return peak;
    }

    [Fact]
    public void Opening_to_listening_is_quick_and_barely_overshoots()
    {
        MotionTokens tokens = Tokens();
        Size2 listening = tokens.NotchSizes[CompanionState.Listening];
        var width = new Spring(tokens.NotchSizes[CompanionState.Idle].Width, tokens.Spring("notchWidth"), 0.05);
        width.SetTarget(listening.Width);

        double seconds = 0;
        while (width.Value < listening.Width - 2)
        {
            width.Step(1.0 / 60);
            seconds += 1.0 / 60;
        }

        Assert.InRange(seconds, 0, 0.36);
        Assert.True(Peak(width) - listening.Width < 2.5);
    }

    [Fact]
    public void The_voice_level_rises_without_bouncing()
    {
        var voice = new Spring(0.1, Tokens().Spring("voice"));
        voice.SetTarget(1);

        Assert.True(Peak(voice) <= 1);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.7)]
    [InlineData(3.3)]
    public void The_wave_never_pinches_or_kinks(double time)
    {
        const int Points = 400;
        double[] heights = new double[Points + 1];
        for (int i = 0; i <= Points; i++)
        {
            heights[i] = NotchCurves.WaveHeight((double)i / Points, time, 1.8, -2.2, 1, 1, 14);
        }

        Assert.Equal(0, heights[0], 6);
        Assert.Equal(0, heights[Points], 6);
        for (int i = Points / 5; i <= Points * 4 / 5; i++)
        {
            Assert.True(heights[i] > 0.5, "pinched at " + i);
        }

        // A kink shows up as a sudden change of slope between neighbouring points.
        for (int i = 1; i < Points; i++)
        {
            Assert.True(Math.Abs(heights[i + 1] - (2 * heights[i]) + heights[i - 1]) < 0.01, "kink at " + i);
        }
    }

    [Theory]
    [InlineData(14, 120, 28, 14)]
    [InlineData(36, 404, 98, 18)]
    [InlineData(24, 372, 48, 18)]
    [InlineData(0, 64, 0, 0)]
    public void Corners_stay_inside_the_notch(double radius, double width, double height, double fillet)
    {
        double corner = NotchCurves.CornerLength(radius, width, height, fillet);

        Assert.InRange(corner, 0, Math.Min(height - fillet, width / 2));
    }
}
