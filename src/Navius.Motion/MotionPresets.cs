namespace Navius.Motion;

/// <summary>
/// The built-in preset table. Fade drifts are 8px (a suggestion of direction), slides
/// are 24px (clearly directional), Zoom scales from 0.95 (subtle), Pop from 0.9 with
/// the bouncy spring (playful). Every preset fades so exits always clear cleanly, and
/// every preset exits with the snappy spring: exits should get out of the way faster
/// than enters arrive.
/// </summary>
public static class MotionPresets
{
    /// <summary>Opacity-only fade. Smooth in, snappy out.</summary>
    public static MotionPreset Fade { get; } = new(
        "fade", new MotionVisualState(0, null), Spring.Smooth, Spring.Snappy);

    /// <summary>Fade drifting up 8px on enter.</summary>
    public static MotionPreset FadeUp { get; } = new(
        "fade-up", new MotionVisualState(0, "translateY(8px)"), Spring.Smooth, Spring.Snappy);

    /// <summary>Fade drifting down 8px on enter.</summary>
    public static MotionPreset FadeDown { get; } = new(
        "fade-down", new MotionVisualState(0, "translateY(-8px)"), Spring.Smooth, Spring.Snappy);

    /// <summary>Fade scaling from 0.95. The default overlay feel.</summary>
    public static MotionPreset Zoom { get; } = new(
        "zoom", new MotionVisualState(0, "scale(0.95)"), Spring.Default, Spring.Snappy);

    /// <summary>Fade scaling from 0.9 with visible overshoot.</summary>
    public static MotionPreset Pop { get; } = new(
        "pop", new MotionVisualState(0, "scale(0.9)"), Spring.Bouncy, Spring.Snappy);

    /// <summary>Fade sliding up 24px from below.</summary>
    public static MotionPreset SlideUp { get; } = new(
        "slide-up", new MotionVisualState(0, "translateY(24px)"), Spring.Default, Spring.Snappy);

    /// <summary>Fade sliding down 24px from above.</summary>
    public static MotionPreset SlideDown { get; } = new(
        "slide-down", new MotionVisualState(0, "translateY(-24px)"), Spring.Default, Spring.Snappy);

    /// <summary>Fade sliding 24px leftward (in from the right).</summary>
    public static MotionPreset SlideLeft { get; } = new(
        "slide-left", new MotionVisualState(0, "translateX(24px)"), Spring.Default, Spring.Snappy);

    /// <summary>Fade sliding 24px rightward (in from the left).</summary>
    public static MotionPreset SlideRight { get; } = new(
        "slide-right", new MotionVisualState(0, "translateX(-24px)"), Spring.Default, Spring.Snappy);

    /// <summary>All presets in stylesheet order (stable: the CSS generator iterates this).</summary>
    public static IReadOnlyList<MotionPreset> All { get; } =
        [Fade, FadeUp, FadeDown, Zoom, Pop, SlideUp, SlideDown, SlideLeft, SlideRight];

    /// <summary>Resolve a <see cref="Preset"/> name to its definition.</summary>
    public static MotionPreset Get(Preset preset) => preset switch
    {
        Preset.Fade => Fade,
        Preset.FadeUp => FadeUp,
        Preset.FadeDown => FadeDown,
        Preset.Zoom => Zoom,
        Preset.Pop => Pop,
        Preset.SlideUp => SlideUp,
        Preset.SlideDown => SlideDown,
        Preset.SlideLeft => SlideLeft,
        Preset.SlideRight => SlideRight,
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };
}
