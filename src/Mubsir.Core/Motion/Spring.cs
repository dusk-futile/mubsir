namespace Mubsir.Core.Motion;

/// <summary>How a spring moves: <paramref name="Response"/> is roughly how many seconds it takes to get
/// there, <paramref name="Damping"/> is the damping fraction (1 = no bounce), as in SwiftUI.</summary>
public readonly record struct SpringToken(double Response, double Damping);

/// <summary>
/// A value that moves toward a target like a damped spring. Changing the target keeps the current
/// velocity, so a new state can interrupt a motion halfway without a jump. Same model as the HTML
/// prototype: stiffness (2π / response)², damping 2 · fraction · (2π / response), semi-implicit Euler
/// in 4 ms sub-steps so long frames stay stable.
/// </summary>
public sealed class Spring
{
    private const double SubStep = 0.004;
    private readonly double _stiffness;
    private readonly double _damping;

    /// <param name="value">Starting value (also the first target).</param>
    /// <param name="token">Response and damping.</param>
    /// <param name="precision">Distance from the target that counts as arrived, in the value's own
    /// units: about a tenth of a pixel for sizes, smaller for 0–1 values.</param>
    public Spring(double value, SpringToken token, double precision = 0.001)
    {
        if (!(token.Response > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(token), "response must be positive");
        }

        if (!(precision > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(precision), "precision must be positive");
        }

        double omega = 2 * Math.PI / token.Response;
        _stiffness = omega * omega;
        _damping = 2 * Math.Max(0, token.Damping) * omega;
        Value = Target = value;
        Precision = precision;
    }

    public double Value { get; private set; }

    public double Velocity { get; private set; }

    public double Target { get; private set; }

    public double Precision { get; }

    /// <summary>When true (the user turned animations off), targets are reached at once and kicks do nothing.</summary>
    public bool Instant { get; set; }

    public bool IsSettled => Velocity == 0 && Value == Target;

    public void SetTarget(double target)
    {
        Target = target;
        if (Instant)
        {
            Value = target;
            Velocity = 0;
        }
    }

    /// <summary>Puts the value at <paramref name="value"/> at rest, with no motion.</summary>
    public void Jump(double value)
    {
        Value = Target = value;
        Velocity = 0;
    }

    /// <summary>Adds velocity (units per second), for a bump or a hop that springs back.</summary>
    public void Kick(double velocity)
    {
        if (!Instant)
        {
            Velocity += velocity;
        }
    }

    /// <summary>Advances the spring by <paramref name="dt"/> seconds. Returns true while it still moves.</summary>
    public bool Step(double dt)
    {
        if (IsSettled)
        {
            return false;
        }

        if (!(dt > 0))
        {
            return true;
        }

        int steps = Math.Max(1, (int)Math.Ceiling(dt / SubStep));
        double h = dt / steps;
        double x = Value;
        double v = Velocity;
        for (int i = 0; i < steps; i++)
        {
            double a = (-_stiffness * (x - Target)) - (_damping * v);
            v += a * h;
            x += v * h;
        }

        if (Math.Abs(x - Target) < Precision && Math.Abs(v) < Precision * 10)
        {
            x = Target;
            v = 0;
        }

        Value = x;
        Velocity = v;
        return !IsSettled;
    }
}
