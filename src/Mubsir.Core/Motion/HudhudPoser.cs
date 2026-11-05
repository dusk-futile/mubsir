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

    /// <summary>0…1 strength of an ongoing shake.</summary>
    public double Shake { get; set; }

    /// <summary>The body's own turn around its middle, while the feet stay planted.</summary>
    public double TorsoAngle { get; set; }

    /// <summary>0…1 how far the feathers stand up, which makes the body and wing a little rounder.</summary>
    public double Fluff { get; set; }

    /// <summary>Birds shut their eyes while they shake.</summary>
    public bool EyesShut { get; set; }
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
    public const double ShakeSeconds = 1.25;

    private const double CrouchSeconds = 0.14;
    private const double BurstSeconds = 0.6;

    /// <summary>
    /// How strong the shake is, at a moment of its burst. It builds fast and dies away slowly, instead of
    /// starting at full strength, which is what made the old one look like an earthquake.
    /// </summary>
    public static double RouseStrength(double seconds)
    {
        double u = (seconds - CrouchSeconds) / BurstSeconds;
        if (u <= 0 || u >= 1)
        {
            return 0;
        }

        double rise = Math.Min(1, u / 0.18);
        return rise * rise * (3 - (2 * rise)) * Math.Pow(1 - u, 1.2);
    }

    /// <summary>
    /// A bird's rouse: the shake starts at the head and travels down the body, so each part passes
    /// <paramref name="delay"/> seconds later. It slows from 8 to about 5.5 beats a second, slow enough
    /// to see each beat at 60 frames a second.
    /// </summary>
    public static double RouseWave(double seconds, double delay)
    {
        double x = seconds - delay;
        double u = (x - CrouchSeconds) / BurstSeconds;
        if (u <= 0 || u >= 1)
        {
            return 0;
        }

        return RouseStrength(x) * Math.Sin(Tau * BurstSeconds * ((8 * u) - (1.25 * u * u)));
    }

    /// <summary>The feathers stand up before the shake and lie flat again after it.</summary>
    public static double RouseFluff(double seconds)
    {
        if (seconds <= 0 || seconds >= 1.15)
        {
            return 0;
        }

        if (seconds < CrouchSeconds)
        {
            return Smooth(seconds / CrouchSeconds);
        }

        return seconds < 0.7 ? 1 : 1 - Smooth((seconds - 0.7) / 0.45);
    }

    private static double Smooth(double k) => k * k * (3 - (2 * k));

    public void Pose(in HudhudInput input, HudhudFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        double a = input.ReduceMotion ? 0 : 1;
        double sl = Math.Clamp(input.Sleepy, 0, 1);
        double t = input.Time;
        double breath = Math.Sin(t * Tau / (3.6 + (1.8 * sl))) * a;

        // The rouse: a small crouch with the feathers standing up, a burst that travels from the head to
        // the tail with the feet planted, then everything settles with a nod and a wag.
        double ds = input.ShakeAge >= 0 && input.ShakeAge < ShakeSeconds ? input.ShakeAge : -1;
        double Wave(double delay) => ds >= 0 ? RouseWave(ds, delay) * a : 0;
        double fluff = ds >= 0 ? RouseFluff(ds) * a : 0;
        double crouch = ds < 0 ? 0 : (ds < CrouchSeconds ? Smooth(ds / CrouchSeconds) : ds < 0.34 ? 1 - Smooth((ds - CrouchSeconds) / 0.2) : 0) * a;
        double settle = ds >= 0.7 ? (ds - 0.7) / (ShakeSeconds - 0.7) : -1;

        double dp = input.PreenAge;
        bool scratching = dp > 0.35 && dp < 1.3;

        // Body: breathing, a slow weight shift, squash and stretch on hops, and the shake.
        double stretch = Math.Clamp(-input.BodyVelocity * 0.00013, -0.07, 0.09) * a;
        frame.BodyY = input.BodyY + (breath * (9 + (8 * sl))) + (crouch * 14);
        frame.BodySway = (Math.Sin(t * Tau / 5.2) * 0.7 * a * (1 - sl)) + Wave(0.03);
        frame.TorsoAngle = Wave(0.035) * 2.6;
        frame.Fluff = fluff;
        frame.EyesShut = ds > 0.16 && ds < 0.66 && a > 0;
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

        up += Wave(0) * 7;
        if (settle is > 0.27 and < 1)
        {
            // A small, satisfied nod at the end.
            up += Math.Sin(Math.PI * (settle - 0.27) / 0.73) * 5 * a;
        }

        frame.HeadY = (sl * 26) + (breath * 4) + (crouch * 10);
        frame.HeadAngle = -up;
        frame.Flip = input.Flip;

        // Crest: folds back and fans up; ripples while awake; shivers in the shake.
        double c = input.Crest + (input.Activity == HudhudActivity.Speaking ? input.Voice * 0.22 : 0) + (fluff * 0.28);
        double cp = Math.Clamp(c, -0.15, 1.15);
        double len = Math.Clamp(c, 0, 1.04);
        for (int i = 0; i < _rig.Crest.Count; i++)
        {
            RigFeather f = _rig.Crest[i];
            frame.FeatherAngle[i] = f.ClosedAngle + ((f.OpenAngle - f.ClosedAngle) * c)
                + (Math.Sin((t * 2.2) + (i * 0.8)) * (0.8 + (1.6 * Math.Max(0, c))) * a * (1 - sl))
                + (Wave(0.01 + (i * 0.012)) * 6);
            frame.FeatherX[i] = f.ClosedX + ((f.OpenX - f.ClosedX) * cp);
            frame.FeatherY[i] = f.ClosedY + ((f.OpenY - f.ClosedY) * cp);
            frame.FeatherLength[i] = f.LengthClosed + ((1 - f.LengthClosed) * len);
        }

        frame.CrestScale = 1 + (input.Raise * 0.09);

        // Wing (positive lifts it), flight feathers fan out, tail wiggles.
        double wl = input.Wing + (breath * 1.2) + (fluff * 7) + (Wave(0.06) * 9);
        frame.WingAngle = _wing.Rest + wl;
        double sp = input.Spread + (fluff * 1.1);
        for (int i = 0; i < _primaries.Length; i++)
        {
            frame.PrimaryAngle[i] = _primaries[i].Rest + (sp * ((i * 7) - 7)) + (wl * 0.35);
        }

        double tw = (Math.Sin(t * Tau / 2.6) * 1.6 * a * (1 - sl)) + (Wave(0.1) * 11);
        if (settle >= 0)
        {
            // The tail keeps wagging a moment after the body has stopped.
            tw += Math.Sin((ds - 0.7) * Tau * 3.2) * (1 - settle) * 5 * a;
        }

        if (scratching)
        {
            tw += Math.Sin(dp * Tau * 7) * 8;
        }

        frame.TailAngle = _tail.Rest + tw;
        frame.Breath = breath;
        frame.Shake = ds >= 0 ? RouseStrength(ds) * a : 0;
    }
}
