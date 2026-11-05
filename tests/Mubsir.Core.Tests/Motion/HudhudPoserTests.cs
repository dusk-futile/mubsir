using Mubsir.Core.Motion;

namespace Mubsir.Core.Tests.Motion;

public class HudhudPoserTests
{
    private static HudhudRig Rig() => HudhudRig.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "rig.json")));

    private static HudhudInput Still(double crest = 0.32) => new()
    {
        Time = 0,
        Crest = crest,
        Flip = 1,
        ShakeAge = -1,
        PreenAge = -1,
        ReduceMotion = true,
    };

    [Fact]
    public void The_rig_has_every_part_and_its_images()
    {
        HudhudRig rig = Rig();

        Assert.Equal(8, rig.BackToFront.Count);
        Assert.Equal(6, rig.Crest.Count);
        Assert.Equal(1060, rig.MirrorX);
        Assert.NotNull(rig.Shadow);

        string root = RepoRoot();
        foreach (string image in rig.Images())
        {
            Assert.True(File.Exists(Path.Combine(root, "assets", "hudhud", image + ".png")), "missing " + image + ".png");
        }
    }

    [Fact]
    public void A_folded_crest_lies_back_and_a_raised_one_fans_up()
    {
        HudhudRig rig = Rig();
        var poser = new HudhudPoser(rig);
        HudhudFrame frame = poser.NewFrame();

        poser.Pose(Still(crest: 0), frame);
        for (int i = 0; i < rig.Crest.Count; i++)
        {
            Assert.Equal(rig.Crest[i].ClosedAngle, frame.FeatherAngle[i], 6);
            Assert.Equal(rig.Crest[i].ClosedX, frame.FeatherX[i], 6);
            Assert.Equal(rig.Crest[i].LengthClosed, frame.FeatherLength[i], 6);
        }

        poser.Pose(Still(crest: 1), frame);
        for (int i = 0; i < rig.Crest.Count; i++)
        {
            Assert.Equal(rig.Crest[i].OpenAngle, frame.FeatherAngle[i], 6);
            Assert.Equal(rig.Crest[i].OpenY, frame.FeatherY[i], 6);
            Assert.Equal(1, frame.FeatherLength[i], 6);
        }
    }

    [Fact]
    public void Sleeping_tucks_the_head()
    {
        var poser = new HudhudPoser(Rig());
        HudhudFrame frame = poser.NewFrame();
        HudhudInput input = Still();
        input.Sleepy = 1;
        input.Tilt = -18;

        poser.Pose(input, frame);

        Assert.Equal(26, frame.HeadY, 6);
        Assert.Equal(18, frame.HeadAngle, 6);
    }

    [Fact]
    public void A_shake_builds_up_before_it_peaks_and_then_dies_away()
    {
        var poser = new HudhudPoser(Rig());
        HudhudFrame frame = poser.NewFrame();
        HudhudInput input = Still();
        input.ReduceMotion = false;
        input.Time = 10;

        // The crouch comes first: feathers up, no shaking yet.
        input.ShakeAge = 0.1;
        poser.Pose(input, frame);
        Assert.Equal(0, frame.Shake);
        Assert.True(frame.Fluff > 0.5, "fluff " + frame.Fluff);

        input.ShakeAge = 0.28;
        poser.Pose(input, frame);
        Assert.True(frame.Shake > 0.6, "shake " + frame.Shake);
        Assert.True(frame.EyesShut);

        input.ShakeAge = HudhudPoser.ShakeSeconds + 0.1;
        poser.Pose(input, frame);
        Assert.Equal(0, frame.Shake);
        Assert.Equal(0, frame.Fluff);
        Assert.False(frame.EyesShut);
    }

    [Fact]
    public void Each_beat_of_the_shake_is_slow_enough_to_see()
    {
        // Counts direction changes in the burst, sampled at 60 frames a second.
        int turns = 0;
        double last = 0;
        double lastDelta = 0;
        for (double t = 0; t < HudhudPoser.ShakeSeconds; t += 1.0 / 60)
        {
            double v = HudhudPoser.RouseWave(t, 0);
            double delta = v - last;
            if (delta * lastDelta < 0)
            {
                turns++;
            }

            last = v;
            lastDelta = delta == 0 ? lastDelta : delta;
        }

        // 0.6 s at 8 falling to 5.5 beats a second is about four beats, each at least 6 frames.
        Assert.InRange(turns, 5, 10);
    }

    [Fact]
    public void The_shake_starts_at_the_head_and_reaches_the_tail_later()
    {
        double firstAtHead = FirstMoment(0);
        double firstAtTail = FirstMoment(0.1);

        Assert.True(firstAtTail > firstAtHead, "tail " + firstAtTail + ", head " + firstAtHead);
    }

    [Fact]
    public void The_shake_never_swings_the_whole_bird_hard()
    {
        var poser = new HudhudPoser(Rig());
        HudhudFrame frame = poser.NewFrame();
        HudhudInput input = Still();
        input.ReduceMotion = false;
        double largest = 0;
        for (double t = 0; t < HudhudPoser.ShakeSeconds; t += 0.005)
        {
            input.ShakeAge = t;
            input.Time = 5.2; // the slow weight shift is at zero here
            poser.Pose(input, frame);
            largest = Math.Max(largest, Math.Abs(frame.BodySway));
        }

        Assert.True(largest <= 1.05, "feet must stay planted, sway " + largest);
    }

    private static double FirstMoment(double delay)
    {
        for (double t = 0; t < 1; t += 0.001)
        {
            if (Math.Abs(HudhudPoser.RouseWave(t, delay)) > 0.05)
            {
                return t;
            }
        }

        return double.NaN;
    }

    [Fact]
    public void Lifting_the_wing_turns_the_flight_feathers_with_it()
    {
        HudhudRig rig = Rig();
        var poser = new HudhudPoser(rig);
        HudhudFrame rest = poser.NewFrame();
        HudhudFrame lifted = poser.NewFrame();
        HudhudInput input = Still();

        poser.Pose(input, rest);
        input.Wing = 28;
        input.Spread = 1.7;
        poser.Pose(input, lifted);

        Assert.Equal(rig.Part("hWing").Rest + 28, lifted.WingAngle, 6);
        Assert.True(lifted.PrimaryAngle[2] - rest.PrimaryAngle[2] > lifted.PrimaryAngle[0] - rest.PrimaryAngle[0], "feathers should fan out");
    }

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Mubsir.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("repo root not found");
    }
}
