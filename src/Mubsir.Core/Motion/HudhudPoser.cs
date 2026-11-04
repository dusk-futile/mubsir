namespace Mubsir.Core.Motion;

/// <summary>What Hudhud is busy with, which adds a small head motion on top of its pose.</summary>
public enum HudhudActivity
{
    Resting,
    Thinking,
    Doing,
    Speaking,
}

/// <summary>Spring values and timers for one frame. Angles are degrees; "Tilt" is beak-up positive.</summary>
public struct HudhudInput
{
    /// <summary>Seconds since the overlay started.</summary>
    public double Time;

    public double Crest;

    public double Raise;

    public double Tilt;

    public double Wing;

    public double Spread;

    /// <summary>1 = looking forward, −1 = head turned round (preening its back).</summary>
    public double Flip;

    /// <summary>0 awake … 1 asleep.</summary>
    public double Sleepy;

    public double BodyY;

    public double BodyVelocity;

    /// <summary>Voice level 0–1 while speaking.</summary>
    public double Voice;

    public HudhudActivity Activity;

    /// <summary>Seconds since a shake started, or a negative value when not shaking.</summary>
    public double ShakeAge;

    /// <summary>Seconds since a preen started, or a negative value when not preening.</summary>
    public double PreenAge;

    /// <summary>The user turned animations off: no breathing, sway or ripples.</summary>
    public bool ReduceMotion;
}

/// <summary>Where every part goes this frame, in rig units (before the on-screen mirror).</summary>
public sealed class HudhudFrame
{
    public HudhudFrame(int feathers, int primaries)
    {
        FeatherAngle = new double[feathers];
        FeatherX = new double[feathers];
        FeatherY = new double[feathers];
        FeatherLength = new double[feathers];
        PrimaryAngle = new double[primaries];
    }

    public double BodyY { get; set; }

    /// <summary>Rotation of the whole bird around its feet.</summary>
    public double BodySway { get; set; }

    public double BodyScaleX { get; set; } = 1;

    public double BodyScaleY { get; set; } = 1;

    public double HeadY { get; set; }

    /// <summary>Rotation of the head around the neck (rig units, clockwise).</summary>
    public double HeadAngle { get; set; }

    public double Flip { get; set; } = 1;

    public double CrestScale { get; set; } = 1;

    public double[] FeatherAngle { get; }

    public double[] FeatherX { get; }

    public double[] FeatherY { get; }

    public double[] FeatherLength { get; }

    public double WingAngle { get; set; }

    public double[] PrimaryAngle { get; }

    public double TailAngle { get; set; }

    /// <summary>The slow breath, −1…1, for the glow behind the bird.</summary>
    public double Breath { get; set; }

    /// <summary>0…1 strength of an ongoing shake (for the dust).</summary>
    public double Shake { get; set; }
}

/// <summary>
/// Turns spring values into part transforms, exactly like the prototype's drawHud: breathing, a slow
/// weight shift, squash and stretch on hops, the full-body shake, head motion per activity, the crest
/// sliding open along the crown, the wing and flight feathers, and the tail.
/// </summary>
public sealed class HudhudPoser
{
    private const double Tau = Math.PI * 2;
    private readonly HudhudRig _rig;
    private readonly RigPart _wing;
    private readonly RigPart _tail;
    private readonly RigPart[] _primaries;

    public HudhudPoser(HudhudRig rig)
    {
        _rig = rig ?? throw new ArgumentNullException(nameof(rig));
        _wing = rig.Part("hWing");
        _tail = rig.Part("hTail");
        _primaries = rig.BackToFront.Where(p => p.Id.StartsWith("hP", StringComparison.Ordinal)).OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
    }

    public HudhudFrame NewFrame() => new(_rig.Crest.Count, _primaries.Length);

    /// <summary>How long a shake lasts, in seconds.</summary>
    public const double ShakeSeconds = 1.05;

    public void Pose(in HudhudInput input, HudhudFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        double a = input.ReduceMotion ? 0 : 1;
        double sl = Math.Clamp(input.Sleepy, 0, 1);
        double t = input.Time;
        double breath = Math.Sin(t * Tau / (3.6 + (1.8 * sl))) * a;

        double ds = input.ShakeAge;
        double shake = ds >= 0 && ds < ShakeSeconds ? Math.Exp(-3.2 * ds) * a : 0;
        if (shake == 0)
        {
            ds = 0;
        }

        double dp = input.PreenAge;
        bool scratching = dp > 0.35 && dp < 1.3;

        // Body: breathing, a slow weight shift, squash and stretch on hops, and the shake.
        double stretch = Math.Clamp(-input.BodyVelocity * 0.00013, -0.07, 0.09) * a;
        frame.BodyY = input.BodyY + (breath * (9 + (8 * sl)));
        frame.BodySway = (Math.Sin(t * Tau / 5.2) * 0.7 * a * (1 - sl)) + (Math.Sin(ds * Tau * 11) * 6 * shake);
        frame.BodyScaleX = 1 - (stretch * 0.6);
        frame.BodyScaleY = 1 + stretch;

        // Head: tilt is beak-up positive; the rig faces right, so it turns the other way.
        double up = input.Tilt;
        up += input.Activity switch
        {
            HudhudActivity.Thinking => Math.Sin(t * Tau / 1.7) * 2.5 * a,
            HudhudActivity.Doing => Math.Sin(t * Tau / 0.85) * 3 * a,
            HudhudActivity.Speaking => input.Voice * 3 * Math.Sin(t * 9) * a,
            _ => 0,
        };
        if (scratching)
        {
            up += Math.Sin(dp * Tau * 9) * 5;
        }

        up -= Math.Sin(ds * Tau * 11) * 4 * shake;
        frame.HeadY = (sl * 26) + (breath * 4);
        frame.HeadAngle = -up;
        frame.Flip = input.Flip;

        // Crest: folds back and fans up; ripples while awake; shivers in the shake.
        double c = input.Crest + (input.Activity == HudhudActivity.Speaking ? input.Voice * 0.22 : 0);
        double cp = Math.Clamp(c, -0.15, 1.15);
        double len = Math.Clamp(c, 0, 1.04);
        for (int i = 0; i < _rig.Crest.Count; i++)
        {
            RigFeather f = _rig.Crest[i];
            frame.FeatherAngle[i] = f.ClosedAngle + ((f.OpenAngle - f.ClosedAngle) * c)
                + (Math.Sin((t * 2.2) + (i * 0.8)) * (0.8 + (1.6 * Math.Max(0, c))) * a * (1 - sl))
                + (Math.Sin((ds * Tau * 12) + (i * 1.3)) * 9 * shake);
            frame.FeatherX[i] = f.ClosedX + ((f.OpenX - f.ClosedX) * cp);
            frame.FeatherY[i] = f.ClosedY + ((f.OpenY - f.ClosedY) * cp);
            frame.FeatherLength[i] = f.LengthClosed + ((1 - f.LengthClosed) * len);
        }

        frame.CrestScale = 1 + (input.Raise * 0.09);

        // Wing (positive lifts it), flight feathers fan out, tail wiggles.
        double wl = input.Wing + (breath * 1.2) + (Math.Sin(ds * Tau * 13) * 14 * shake) + (10 * shake);
        frame.WingAngle = _wing.Rest + wl;
        double sp = input.Spread + (1.2 * shake);
        for (int i = 0; i < _primaries.Length; i++)
        {
            frame.PrimaryAngle[i] = _primaries[i].Rest + (sp * ((i * 7) - 7)) + (wl * 0.35);
        }

        double tw = (Math.Sin(t * Tau / 2.6) * 1.6 * a * (1 - sl)) + (Math.Sin(ds * Tau * 10) * 14 * shake);
        if (scratching)
        {
            tw += Math.Sin(dp * Tau * 7) * 8;
        }

        frame.TailAngle = _tail.Rest + tw;
        frame.Breath = breath;
        frame.Shake = shake;
    }
}
