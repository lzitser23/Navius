using Navius.Motion.Interop;

namespace Navius.Motion;

/// <summary>
/// Compiles preset data into the serialized programs the JS executor plays: springs
/// are baked to <c>linear()</c> easings here (in C#) so the runtime tier animates with
/// exactly the same curves as the generated stylesheet.
/// </summary>
public static class MotionPrograms
{
    /// <summary>
    /// Build the enter keyframes and options for
    /// <see cref="MotionJsInterop.AnimateElementAsync"/>: hidden state to visible with
    /// the preset's (or an override) enter spring.
    /// </summary>
    public static (MotionFrame[] Keyframes, MotionAnimateOptions Options) Enter(
        MotionPreset preset, Spring? spring = null, double delayMs = 0)
    {
        var baked = LinearEasingBaker.Bake(spring ?? preset.EnterSpring);
        var keyframes = new[] { ToFrame(preset.Hidden), ToFrame(MotionVisualState.Visible) };
        // Fill backwards: hold the hidden frame through the delay, then release the
        // element at its natural style (which is the visible state) once finished.
        return (keyframes, new MotionAnimateOptions(baked.DurationMilliseconds, baked.Easing, delayMs, Fill: "backwards"));
    }

    /// <summary>
    /// Build a <see cref="PresenceMotionOptions"/> program from a preset, optionally
    /// overriding either phase's spring.
    /// </summary>
    public static PresenceMotionOptions Presence(
        MotionPreset preset, Spring? enterSpring = null, Spring? exitSpring = null)
    {
        var enter = LinearEasingBaker.Bake(enterSpring ?? preset.EnterSpring);
        var exit = LinearEasingBaker.Bake(exitSpring ?? preset.ExitSpring);
        return new PresenceMotionOptions(
            new MotionPhase(
                [ToFrame(preset.Hidden), ToFrame(MotionVisualState.Visible)],
                enter.DurationMilliseconds, enter.Easing),
            new MotionPhase(
                [ToFrame(preset.Hidden)],
                exit.DurationMilliseconds, exit.Easing));
    }

    /// <summary>Build gesture options timed by a spring (default: snappy).</summary>
    public static GestureOptions Gesture(
        Spring? spring = null, double pressScale = 0.97, double hoverLift = 2)
    {
        var baked = LinearEasingBaker.Bake(spring ?? Spring.Snappy);
        return new GestureOptions(baked.DurationMilliseconds, baked.Easing, pressScale, hoverLift);
    }

    private static MotionFrame ToFrame(MotionVisualState state)
        => new(state.Opacity, state.Transform ?? "none");
}
