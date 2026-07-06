using System.Globalization;

namespace Navius.Motion;

/// <summary>
/// Bakes a spring run into a CSS <c>linear()</c> easing string plus a real duration:
/// sample the solver at a fixed resolution (default 10ms, so points-per-second stays
/// constant regardless of duration), normalize to 0..1 progress, round to 4 decimals
/// (Motion's generateLinearEasing recipe). The sampled points double as the
/// pre-generated-keyframes fallback for engines without <c>linear()</c> support.
/// All formatting is invariant culture.
/// </summary>
public static class LinearEasingBaker
{
    /// <summary>Default sample resolution in seconds (10ms, 100 points per second).</summary>
    public const double DefaultResolutionSeconds = 0.01;

    /// <summary>
    /// Bake a spring on a 0 to 1 probe run (Motion's approach: the normalized easing is
    /// value-independent, so one bake serves any keyframe pair).
    /// </summary>
    public static BakedEasing Bake(Spring spring, double resolutionSeconds = DefaultResolutionSeconds)
        => Bake(new SpringSolver(spring, 0, 1), resolutionSeconds);

    /// <summary>Bake an explicit solver run. The run must move (origin != target).</summary>
    public static BakedEasing Bake(SpringSolver solver, double resolutionSeconds = DefaultResolutionSeconds)
    {
        if (solver.Origin == solver.Target)
        {
            throw new ArgumentException("Cannot bake a spring whose origin equals its target.", nameof(solver));
        }

        var duration = solver.SettleDuration;
        var delta = solver.Target - solver.Origin;
        var numPoints = Math.Max((int)Math.Round(duration / resolutionSeconds, MidpointRounding.AwayFromZero), 2);

        var points = new double[numPoints];
        for (var i = 0; i < numPoints; i++)
        {
            var t = duration * i / (numPoints - 1);
            points[i] = Math.Round((solver.Position(t) - solver.Origin) / delta, 4, MidpointRounding.AwayFromZero);
        }
        // Motion's generator snaps to the target once done; mirror that so the curve
        // always lands exactly on 1 instead of inside the rest threshold.
        points[numPoints - 1] = 1;

        var easing = "linear(" + string.Join(", ", points.Select(FormatPoint)) + ")";
        var multiplier = duration / solver.Spring.VisualDurationSeconds;
        return new BakedEasing(easing, duration, points, multiplier);
    }

    internal static string FormatPoint(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}

/// <summary>
/// A baked spring: the <c>linear()</c> easing string, the real duration it must play
/// over, the raw sampled progress points (the keyframes fallback), and the perceptual
/// duration multiplier (real duration / visual duration, the tailwindcss-motion trick:
/// a baked spring's settling tail needs a longer real duration than its perceived one).
/// </summary>
public sealed record BakedEasing(
    string Easing,
    double DurationSeconds,
    IReadOnlyList<double> Points,
    double PerceptualDurationMultiplier)
{
    /// <summary>Real duration in milliseconds, rounded to the nearest integer.</summary>
    public int DurationMilliseconds => (int)Math.Round(DurationSeconds * 1000, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Map the normalized progress points onto an actual value range: the
    /// pre-generated-keyframes fallback (play these values with linear easing where
    /// <c>linear()</c> is unsupported).
    /// </summary>
    public double[] MapTo(double from, double to)
    {
        var values = new double[Points.Count];
        for (var i = 0; i < Points.Count; i++)
        {
            values[i] = from + Points[i] * (to - from);
        }
        return values;
    }
}
