namespace Mubsir.Core.Motion;

/// <summary>The soft curves of the notch: its corners and the voice wave inside it.</summary>
public static class NotchCurves
{
    /// <summary>How far a corner's curve reaches, as a share of the handle's length. A circle would be 0.55.</summary>
    public const double CornerHandle = 0.6;

    /// <summary>A corner starts further back than a circle of the same radius would, so the straight
    /// edge eases into the curve instead of turning at a visible point.</summary>
    public static double CornerLength(double radius, double width, double height, double fillet) =>
        Math.Max(0, Math.Min(radius * 1.28, Math.Min(height - fillet, width / 2)));

    /// <summary>How far one ribbon of the wave reaches from its centre line, in the same units as
    /// <paramref name="maxHeight"/>. It swells and shrinks but never pinches to a point, and it tapers to
    /// nothing at both ends. <paramref name="x"/> runs from 0 to 1.</summary>
    public static double WaveHeight(double x, double time, double frequency, double speed, double amplitude, double level, double maxHeight)
    {
        double envelope = Math.Pow(Math.Sin(Math.PI * x), 2.2);
        double swell = 0.55 + (0.45 * Math.Sin((Math.PI * 2 * frequency * x) + (time * speed)));
        return envelope * maxHeight * ((level * amplitude * swell) + 0.04);
    }
}
