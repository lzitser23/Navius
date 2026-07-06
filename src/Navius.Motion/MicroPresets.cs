namespace Navius.Motion;

/// <summary>
/// The micro-interaction preset table: attention and ambient keyframe animations
/// (shake, pulse, shimmer, focus-glow) that play on an element rather than riding the
/// discrete presence-state attributes. Like <see cref="MotionPresets"/> this is the
/// single source: the same records feed the CSS generator
/// (<see cref="MotionStylesheet"/>, which emits a <c>@keyframes</c> block and a class
/// per preset) and the WAAPI runtime (<see cref="MotionPrograms.Micro"/>), so both
/// tiers animate identically. Values are inky and restrained: distances measured in a
/// few pixels, colours driven by neutral CSS variables (one-ink, no hue).
/// </summary>
public static class MicroPresets
{
    /// <summary>
    /// Validation-error horizontal shake: a quick decaying left/right nudge that ends
    /// exactly at identity. One-shot, so trigger it on the error event (runtime tier).
    /// Transform-only, so under reduced motion it collapses to nothing (does not play).
    /// </summary>
    public static MicroPreset Shake { get; } = new(
        "shake",
        [
            new(0,    Transform: "translateX(0)"),
            new(0.15, Transform: "translateX(-6px)"),
            new(0.3,  Transform: "translateX(5px)"),
            new(0.45, Transform: "translateX(-4px)"),
            new(0.6,  Transform: "translateX(3px)"),
            new(0.75, Transform: "translateX(-2px)"),
            new(0.9,  Transform: "translateX(1px)"),
            new(1,    Transform: "translateX(0)"),
        ],
        DurationMs: 450,
        Easing: "cubic-bezier(0.36, 0.07, 0.19, 0.97)",
        Loop: false,
        Reduce: MicroReduce.Collapse);

    /// <summary>
    /// Live-status pulse: a gentle breathing of opacity and scale, looped. Under reduced
    /// motion it collapses to the opacity beat only (the scale is dropped), which stays
    /// non-vestibular while still reading as "live".
    /// </summary>
    public static MicroPreset Pulse { get; } = new(
        "pulse",
        [
            new(0,   Transform: "scale(1)",    Opacity: "1"),
            new(0.5, Transform: "scale(0.85)", Opacity: "0.5"),
            new(1,   Transform: "scale(1)",    Opacity: "1"),
        ],
        DurationMs: 1600,
        Easing: "ease-in-out",
        Loop: true,
        Reduce: MicroReduce.OpacityOnly);

    /// <summary>
    /// Surface/text shimmer sweep: a translucent one-ink highlight travelling across the
    /// element via background-position, looped. Reduced-motion fallback: the sweep stops
    /// (animation none) but the gradient surface remains, so it rests as a static
    /// placeholder rather than vanishing. Tune the highlight with
    /// <c>--navius-motion-shimmer-tint</c>.
    /// </summary>
    public static MicroPreset Shimmer { get; } = new(
        "shimmer",
        [
            new(0, BackgroundPosition: "200% 0"),
            new(1, BackgroundPosition: "-200% 0"),
        ],
        DurationMs: 1600,
        Easing: "linear",
        Loop: true,
        Reduce: MicroReduce.Collapse,
        BaseStyle:
        [
            new("backgroundImage", "linear-gradient(100deg, transparent 20%, var(--navius-motion-shimmer-tint, rgba(128, 128, 128, 0.18)) 50%, transparent 80%)"),
            new("backgroundSize", "200% 100%"),
            new("backgroundRepeat", "no-repeat"),
        ]);

    /// <summary>
    /// Focus emphasis: a hairline ring that pulses outward and fades, looped. One-ink
    /// (a neutral <c>box-shadow</c> ring, no hue). Reduced-motion fallback: the pulse
    /// stops and a static hairline ring remains, so the emphasis is kept without motion.
    /// Tune with <c>--navius-motion-glow-color</c> / <c>--navius-motion-glow-ring</c>.
    /// </summary>
    public static MicroPreset FocusGlow { get; } = new(
        "focus-glow",
        [
            new(0,   BoxShadow: "0 0 0 0 var(--navius-motion-glow-color, rgba(115, 115, 115, 0.5))"),
            new(0.7, BoxShadow: "0 0 0 6px var(--navius-motion-glow-color-fade, rgba(115, 115, 115, 0))"),
            new(1,   BoxShadow: "0 0 0 0 var(--navius-motion-glow-color-fade, rgba(115, 115, 115, 0))"),
        ],
        DurationMs: 1800,
        Easing: "ease-out",
        Loop: true,
        Reduce: MicroReduce.Collapse,
        BaseStyle:
        [
            new("boxShadow", "0 0 0 1px var(--navius-motion-glow-ring, rgba(115, 115, 115, 0.35))"),
        ]);

    /// <summary>All micro presets in stylesheet order (stable: the CSS generator iterates this).</summary>
    public static IReadOnlyList<MicroPreset> All { get; } = [Shake, Pulse, Shimmer, FocusGlow];
}
