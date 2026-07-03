namespace Navius.Motion;

/// <summary>
/// The built-in enter/exit preset names. Each maps to a <see cref="MotionPreset"/>
/// (see <see cref="MotionPresets"/>) consumed by both the generated stylesheet and the
/// WAAPI runtime, so the two tiers always agree. Slide directions name the way the
/// element travels while entering (SlideUp enters moving up, from below).
/// </summary>
public enum Preset
{
    /// <summary>Opacity only.</summary>
    Fade,

    /// <summary>Fade with a short upward drift.</summary>
    FadeUp,

    /// <summary>Fade with a short downward drift.</summary>
    FadeDown,

    /// <summary>Fade with a subtle scale from 95 percent.</summary>
    Zoom,

    /// <summary>Fade with a bouncy scale from 90 percent.</summary>
    Pop,

    /// <summary>Fade sliding up from below.</summary>
    SlideUp,

    /// <summary>Fade sliding down from above.</summary>
    SlideDown,

    /// <summary>Fade sliding left from the right.</summary>
    SlideLeft,

    /// <summary>Fade sliding right from the left.</summary>
    SlideRight,
}
