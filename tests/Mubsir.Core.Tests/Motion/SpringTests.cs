using Mubsir.Core.Motion;

namespace Mubsir.Core.Tests.Motion;

public class SpringTests
{
    private const double Frame = 1.0 / 60;

    private static double Run(Spring spring, double seconds, Func<double, double>? track = null)
    {
        double peak = spring.Value;
        for (double t = 0; t < seconds; t += Frame)
        {
            spring.Step(Frame);
            peak = Math.Max(peak, spring.Value);
            track?.Invoke(spring.Value);
        }

        return peak;
    }

    [Fact]
    public void Reaches_the_target_and_settles()
    {
        var spring = new Spring(0, new SpringToken(0.4, 1), precision: 0.05);
        spring.SetTarget(100);

        Run(spring, 3);

        Assert.Equal(100, spring.Value);
        Assert.True(spring.IsSettled);
        Assert.False(spring.Step(Frame));
    }

    [Fact]
    public void Gets_almost_all_the_way_in_one_response()
    {
        // A critically damped spring is about 98.6 % of the way there after one response.
        var spring = new Spring(0, new SpringToken(0.5, 1), precision: 0.001);
        spring.SetTarget(1);

        Run(spring, 0.5);

        Assert.InRange(spring.Value, 0.97, 1.0);
    }

    [Fact]
    public void No_bounce_at_damping_one()
    {
        var spring = new Spring(0, new SpringToken(0.32, 1), precision: 0.01);
        spring.SetTarget(100);

        double peak = Run(spring, 2);

        Assert.True(peak <= 100.01, "overshot to " + peak);
    }

    [Fact]
    public void Bounces_a_little_below_damping_one_then_settles()
    {
        var spring = new Spring(0, new SpringToken(0.42, 0.55), precision: 0.01);
        spring.SetTarget(100);

        double peak = Run(spring, 4);

        Assert.InRange(peak, 105, 130);
        Assert.Equal(100, spring.Value);
    }

    [Fact]
    public void Changing_the_target_keeps_the_velocity()
    {
        var spring = new Spring(0, new SpringToken(0.52, 0.72), precision: 0.05);
        spring.SetTarget(400);
        Run(spring, 0.15);
        double velocity = spring.Velocity;
        double value = spring.Value;

        spring.SetTarget(120);

        Assert.Equal(velocity, spring.Velocity);
        Assert.Equal(value, spring.Value);
        Assert.True(velocity > 0);
    }

    [Fact]
    public void A_long_frame_stays_stable()
    {
        var spring = new Spring(0, new SpringToken(0.12, 0.7), precision: 0.001);
        spring.SetTarget(1);

        spring.Step(0.5);

        Assert.InRange(spring.Value, 0.9, 1.1);
    }

    [Fact]
    public void Instant_mode_jumps_and_ignores_kicks()
    {
        var spring = new Spring(0, new SpringToken(0.4, 0.8)) { Instant = true };

        spring.SetTarget(5);
        spring.Kick(300);

        Assert.Equal(5, spring.Value);
        Assert.Equal(0, spring.Velocity);
        Assert.True(spring.IsSettled);
    }

    [Fact]
    public void A_kick_moves_it_and_it_comes_back()
    {
        var spring = new Spring(0, new SpringToken(0.46, 0.42), precision: 0.05);

        spring.Kick(-750);
        double lowest = 0;
        Run(spring, 3, v => lowest = Math.Min(lowest, v));

        Assert.True(lowest < -20, "barely moved: " + lowest);
        Assert.Equal(0, spring.Value);
    }

    [Fact]
    public void Rejects_a_zero_response()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Spring(0, new SpringToken(0, 1)));
    }
}
