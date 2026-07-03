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

    /// <summary>
    /// Build the runtime program for a micro preset (shake, pulse, ...): the keyframes
    /// cross the boundary as WAAPI frames, timed with the preset's own duration/easing
    /// (these are attention/ambient animations, not springs). Loops materialize as
    /// infinite iterations JS-side; the returned handle plays/stops them.
    /// </summary>
    public static MicroOptions Micro(MicroPreset preset)
    {
        var frames = preset.Keyframes.Select(ToMicroKeyframe).ToArray();
        return new MicroOptions(frames, preset.DurationMs, preset.Easing, preset.Loop);
    }

    /// <summary>
    /// Build <see cref="AutoAnimateOptions"/> for a FLIP-on-mutation container. A
    /// <paramref name="spring"/> bakes to a <c>linear()</c> easing and its settle
    /// duration (our differentiator: springs on the FLIP); <paramref name="durationMs"/>
    /// and <paramref name="easing"/> override either independently. With no arguments the
    /// @formkit/auto-animate defaults (250ms, ease-in-out) apply.
    /// </summary>
    public static AutoAnimateOptions AutoAnimate(
        Spring? spring = null, double? durationMs = null, string? easing = null, string reduceMotion = "user")
    {
        if (spring is Spring resolved)
        {
            var baked = LinearEasingBaker.Bake(resolved);
            return new AutoAnimateOptions(durationMs ?? baked.DurationMilliseconds, easing ?? baked.Easing, reduceMotion);
        }
        return new AutoAnimateOptions(durationMs ?? 250, easing ?? "ease-in-out", reduceMotion);
    }

    private static MotionFrame ToFrame(MotionVisualState state)
        => new(state.Opacity, state.Transform ?? "none");

    private static IReadOnlyDictionary<string, object> ToMicroKeyframe(MicroFrame frame)
    {
        var dict = new Dictionary<string, object> { ["offset"] = frame.Offset };
        if (frame.Transform is not null) dict["transform"] = frame.Transform;
        if (frame.Opacity is not null) dict["opacity"] = frame.Opacity;
        if (frame.BoxShadow is not null) dict["boxShadow"] = frame.BoxShadow;
        if (frame.BackgroundPosition is not null) dict["backgroundPosition"] = frame.BackgroundPosition;
        return dict;
    }
}
