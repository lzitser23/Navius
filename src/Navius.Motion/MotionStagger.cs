namespace Navius.Motion;

/// <summary>The anchor a stagger fans out from.</summary>
public enum StaggerFrom
{
    /// <summary>First child moves first; delay grows with index.</summary>
    First,

    /// <summary>Last child moves first; delay grows toward index 0.</summary>
    Last,

    /// <summary>The centre child moves first; delay grows toward both ends (half-steps for an even count).</summary>
    Center,
}

/// <summary>
/// The stagger delay schedule: each child's <c>--navius-motion-delay</c> as a function
/// of its index, the step and the anchor. This is the authoring/spec surface; the same
/// distance-from-anchor formula is duplicated in <c>navius-motion.js</c>
/// (<c>staggerDelays</c>) for the runtime, and the JS agreement test keeps the two in
/// lock-step. Center distance is fractional for an even count (Motion's stagger
/// semantics), so delays can be half-steps.
/// </summary>
public static class MotionStagger
{
    /// <summary>
    /// The per-child delays in milliseconds, index-aligned to the children, for a group
    /// of <paramref name="count"/> revealed with a <paramref name="stepMs"/> step from
    /// the <paramref name="from"/> anchor.
    /// </summary>
    public static double[] Delays(int count, double stepMs, StaggerFrom from = StaggerFrom.First)
    {
        var n = Math.Max(0, count);
        var delays = new double[n];
        var center = (n - 1) / 2.0;
        for (var i = 0; i < n; i++)
        {
            var distance = from switch
            {
                StaggerFrom.Last => n - 1 - i,
                StaggerFrom.Center => Math.Abs(i - center),
                _ => i,
            };
            delays[i] = Math.Round(distance * stepMs, 4, MidpointRounding.AwayFromZero);
        }
        return delays;
    }

    /// <summary>The lowercase token the JS runtime expects for <see cref="StaggerFrom"/>.</summary>
    public static string ToToken(this StaggerFrom from) => from switch
    {
        StaggerFrom.Last => "last",
        StaggerFrom.Center => "center",
        _ => "first",
    };
}
