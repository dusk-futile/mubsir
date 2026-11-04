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
    public void A_shake_moves_the_wing_and_dies_away()
    {
        var poser = new HudhudPoser(Rig());
        HudhudFrame frame = poser.NewFrame();
        HudhudInput input = Still();
        input.ReduceMotion = false;
        input.Time = 10;

        input.ShakeAge = 0.05;
        poser.Pose(input, frame);
        Assert.True(frame.Shake > 0.8, "shake " + frame.Shake);

        input.ShakeAge = HudhudPoser.ShakeSeconds + 0.1;
        poser.Pose(input, frame);
        Assert.Equal(0, frame.Shake);
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
