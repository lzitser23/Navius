namespace Navius.Motion;

/// <summary>
/// One enter/exit preset defined as data: the hidden state the element occupies while
/// closed (and passes through while entering/exiting) plus the springs for each phase.
/// This single definition feeds both the CSS generator (<see cref="MotionStylesheet"/>)
/// and the WAAPI runtime (<see cref="Interop.MotionJsInterop"/>), which is what keeps
/// the zero-JS tier and the interruption-capable tier in agreement.
/// </summary>
public sealed record MotionPreset(
    string Name,
    MotionVisualState Hidden,
    Spring EnterSpring,
    Spring ExitSpring)
{
    /// <summary>The CSS class the generated stylesheet emits for this preset (presence tier).</summary>
    public string PresenceClass => "motion-" + Name;

    /// <summary>The CSS class for the insert-only tier (uses @starting-style, no state attributes needed).</summary>
    public string EnterClass => "motion-enter-" + Name;
}

/// <summary>
/// A visual state expressed as the two compositor-friendly properties the presets
/// animate. <see cref="Transform"/> is a raw CSS transform value or null for none.
/// </summary>
public sealed record MotionVisualState(double Opacity, string? Transform)
{
    /// <summary>The fully visible state every preset enters to.</summary>
    public static MotionVisualState Visible { get; } = new(1, null);
}
