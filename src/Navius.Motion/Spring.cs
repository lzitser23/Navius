namespace Navius.Motion;

/// <summary>
/// A spring definition: resolved physics parameters (stiffness, damping, mass,
/// initial velocity) plus, when created from perceptual parameters
/// (<see cref="FromDuration"/> / <see cref="FromVisualDuration"/>), the metadata the
/// solver needs to honour the requested timing. Immutable; feed it to
/// <see cref="SpringSolver"/> to evaluate motion. Formulas follow Motion's
/// closed-form spring generator so baked output matches the reference behaviour.
/// </summary>
public readonly record struct Spring
{
    private Spring(double stiffness, double damping, double mass, double initialVelocity,
        bool isDurationResolved, double resolvedDuration)
    {
        Stiffness = stiffness;
        Damping = damping;
        Mass = mass;
        InitialVelocity = initialVelocity;
        IsDurationResolved = isDurationResolved;
        ResolvedDurationSeconds = resolvedDuration;
    }

    /// <summary>Spring constant (force per unit displacement).</summary>
    public double Stiffness { get; }

    /// <summary>Damping coefficient (force per unit velocity).</summary>
    public double Damping { get; }

    /// <summary>Moving mass. Almost always 1.</summary>
    public double Mass { get; }

    /// <summary>Initial velocity in value units per second (dx/dt at t = 0).</summary>
    public double InitialVelocity { get; }

    /// <summary>
    /// True when the spring was derived from <see cref="FromDuration"/>; the solver then
    /// treats <see cref="ResolvedDurationSeconds"/> as the settle duration (Motion's
    /// isResolvedFromDuration semantics) instead of stepping to rest.
    /// </summary>
    public bool IsDurationResolved { get; }

    /// <summary>The clamped requested duration when <see cref="IsDurationResolved"/>; otherwise 0.</summary>
    public double ResolvedDurationSeconds { get; }

    /// <summary>Damping ratio zeta = damping / (2 * sqrt(stiffness * mass)). 1 = critically damped.</summary>
    public double DampingRatio => Damping / (2 * Math.Sqrt(Stiffness * Mass));

    /// <summary>Undamped angular frequency omega0 = sqrt(stiffness / mass), radians per second.</summary>
    public double AngularFrequency => Math.Sqrt(Stiffness / Mass);

    /// <summary>
    /// The perceived duration of the spring in seconds: the inverse of the
    /// <see cref="FromVisualDuration"/> mapping, 2 * pi / (1.2 * omega0). Used to derive
    /// the perceptual duration multiplier when baking.
    /// </summary>
    public double VisualDurationSeconds => 2 * Math.PI / (1.2 * AngularFrequency);

    /// <summary>Define a spring from raw physics parameters.</summary>
    public static Spring Physics(double stiffness, double damping, double mass = 1, double initialVelocity = 0)
        => new(stiffness, damping, mass, initialVelocity, false, 0);

    /// <summary>
    /// Define a spring perceptually from a settle duration (seconds) and a bounce in
    /// [0, 1] (0 = no bounce, critically damped; 1 = maximum bounce). Solves for the
    /// undamped frequency with Newton-Raphson on the amplitude envelope (Motion's
    /// findSpring): dampingRatio = clamp(1 - bounce, 0.05, 1), duration clamped to
    /// [0.01, 10] seconds, then stiffness = omega0^2 * mass and
    /// damping = 2 * dampingRatio * sqrt(mass * stiffness).
    /// </summary>
    public static Spring FromDuration(double durationSeconds, double bounce = 0.25, double initialVelocity = 0)
    {
        const double safeMin = 0.001;
        const int rootIterations = 12;

        var dampingRatio = Math.Clamp(1 - bounce, 0.05, 1.0);
        var duration = Math.Clamp(durationSeconds, 0.01, 10.0);
        var velocity = initialVelocity;

        Func<double, double> envelope;
        Func<double, double> derivative;
        if (dampingRatio < 1)
        {
            envelope = f =>
            {
                var exponentialDecay = f * dampingRatio;
                var delta = exponentialDecay * duration;
                var a = exponentialDecay - velocity;
                var b = f * Math.Sqrt(1 - dampingRatio * dampingRatio);
                var c = Math.Exp(-delta);
                return safeMin - (a / b) * c;
            };
            derivative = f =>
            {
                var exponentialDecay = f * dampingRatio;
                var delta = exponentialDecay * duration;
                var d = delta * velocity + velocity;
                var e = dampingRatio * dampingRatio * f * f * duration;
                var g = f * f * Math.Sqrt(1 - dampingRatio * dampingRatio);
                var factor = safeMin - envelope(f) > 0 ? -1.0 : 1.0;
                return factor * ((d - e) * Math.Exp(-delta)) / g;
            };
        }
        else
        {
            envelope = f =>
            {
                var a = Math.Exp(-f * duration);
                var b = (f - velocity) * duration + 1;
                return -safeMin + a * b;
            };
            derivative = f =>
            {
                var a = Math.Exp(-f * duration);
                var b = (velocity - f) * (duration * duration);
                return a * b;
            };
        }

        var result = 5.0 / duration;
        for (var i = 1; i < rootIterations; i++)
        {
            result -= envelope(result) / derivative(result);
        }

        if (double.IsNaN(result))
        {
            return new Spring(100, 10, 1, initialVelocity, true, duration);
        }

        var stiffness = result * result;
        var damping = dampingRatio * 2 * Math.Sqrt(stiffness);
        return new Spring(stiffness, damping, 1, initialVelocity, true, duration);
    }

    /// <summary>
    /// Define a spring perceptually from its visual (perceived) duration in seconds,
    /// Motion's visualDuration shortcut: stiffness = (2 * pi / (visualDuration * 1.2))^2,
    /// damping = 2 * clamp(1 - bounce, 0.05, 1) * sqrt(stiffness). The real settle
    /// duration is longer than the visual one; the baker reports the ratio as the
    /// perceptual duration multiplier.
    /// </summary>
    public static Spring FromVisualDuration(double visualDurationSeconds, double bounce = 0.25, double initialVelocity = 0)
    {
        var root = 2 * Math.PI / (visualDurationSeconds * 1.2);
        var stiffness = root * root;
        var damping = 2 * Math.Clamp(1 - bounce, 0.05, 1.0) * Math.Sqrt(stiffness);
        return new Spring(stiffness, damping, 1, initialVelocity, false, 0);
    }

    /// <summary>Return this spring with a different initial velocity (units per second).</summary>
    public Spring WithInitialVelocity(double initialVelocity)
        => new(Stiffness, Damping, Mass, initialVelocity, IsDurationResolved, ResolvedDurationSeconds);

    /// <summary>Balanced default: visual duration 0.3s, bounce 0.2. A calm, general-purpose spring.</summary>
    public static Spring Default => FromVisualDuration(0.3, 0.2);

    /// <summary>Critically damped glide: visual duration 0.35s, bounce 0. No overshoot at all.</summary>
    public static Spring Smooth => FromVisualDuration(0.35, 0.0);

    /// <summary>Fast and eager: visual duration 0.2s, bounce 0.1. Settles quickly with a hint of life.</summary>
    public static Spring Snappy => FromVisualDuration(0.2, 0.1);

    /// <summary>Playful overshoot: visual duration 0.35s, bounce 0.45. Clearly visible bounce.</summary>
    public static Spring Bouncy => FromVisualDuration(0.35, 0.45);
}
