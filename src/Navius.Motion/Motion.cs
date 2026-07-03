using System.Globalization;

namespace Navius.Motion;

/// <summary>
/// Attribute-splat helpers for the zero-JS CSS tier: each returns a dictionary for
/// <c>@attributes</c> that opts the element into the generated
/// <c>navius-motion.css</c> classes. Blazor's duplicate-attribute rule is last wins,
/// so splat these after (or instead of) any literal <c>class</c>/<c>style</c>
/// attribute, or merge manually. For the WAAPI tier with interruption handling use
/// <see cref="NaviusMotion"/> or <see cref="Interop.MotionJsInterop"/> instead.
/// </summary>
public static class Motion
{
    /// <summary>
    /// Animate in once when the element is inserted (uses @starting-style; needs no
    /// state attributes, works on any element). Optional delay in milliseconds.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Enter(Preset preset, double delayMs = 0)
    {
        var attributes = new Dictionary<string, object>
        {
            ["class"] = MotionPresets.Get(preset).EnterClass,
        };
        if (delayMs > 0)
        {
            attributes["style"] = "--navius-motion-delay: " + Format(delayMs) + "ms";
        }
        return attributes;
    }

    /// <summary>
    /// Animate open/close transitions of an element that carries the Navius discrete
    /// state attributes (data-open, data-closed, data-starting-style,
    /// data-ending-style), i.e. any Navius primitive part.
    /// </summary>
    public static IReadOnlyDictionary<string, object> Presence(Preset preset)
        => new Dictionary<string, object>
        {
            ["class"] = MotionPresets.Get(preset).PresenceClass,
        };

    /// <summary>Scale down while pressed (:active, which also covers keyboard activation on buttons).</summary>
    public static IReadOnlyDictionary<string, object> Press(double scale = 0.97)
        => new Dictionary<string, object>
        {
            ["class"] = "motion-press",
            ["style"] = "--navius-motion-press-scale: " + Format(scale),
        };

    /// <summary>Lift by <paramref name="lift"/> pixels on hover (hover-capable pointers only).</summary>
    public static IReadOnlyDictionary<string, object> Hover(double lift = 2)
        => new Dictionary<string, object>
        {
            ["class"] = "motion-hover",
            ["style"] = "--navius-motion-hover-lift: " + Format(lift),
        };

    private static string Format(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);
}
