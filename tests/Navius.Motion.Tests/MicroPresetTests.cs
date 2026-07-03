namespace Navius.Motion.Tests;

/// <summary>
/// The micro pack single-source table (<see cref="MicroPresets"/>) and the two things it
/// feeds: the Motion splat helpers (class names) and the runtime program builder
/// (<see cref="MotionPrograms.Micro"/>). The stylesheet side is covered by
/// <see cref="MotionStylesheetTests"/>.
/// </summary>
public class MicroPresetTests
{
    [Fact]
    public void Table_is_ordered_and_names_derive_the_class_and_keyframes()
    {
        Assert.Equal(4, MicroPresets.All.Count);
        Assert.Equal(new[] { "shake", "pulse", "shimmer", "focus-glow" },
            MicroPresets.All.Select(p => p.Name).ToArray());

        Assert.Equal("motion-shake", MicroPresets.Shake.Class);
        Assert.Equal("navius-shake", MicroPresets.Shake.KeyframesName);
        Assert.Equal("navius-pulse-reduced", MicroPresets.Pulse.ReducedKeyframesName);
    }

    [Fact]
    public void Loop_and_reduced_behaviour_match_each_preset_intent()
    {
        // One-shot, transform-only: collapses to nothing under reduced motion.
        Assert.False(MicroPresets.Shake.Loop);
        Assert.Equal(MicroReduce.Collapse, MicroPresets.Shake.Reduce);

        // Looping status beat: keeps the opacity keyframes under reduced motion.
        Assert.True(MicroPresets.Pulse.Loop);
        Assert.Equal(MicroReduce.OpacityOnly, MicroPresets.Pulse.Reduce);

        // Ambient loops that rest under reduced motion.
        Assert.True(MicroPresets.Shimmer.Loop);
        Assert.Equal(MicroReduce.Collapse, MicroPresets.Shimmer.Reduce);
        Assert.True(MicroPresets.FocusGlow.Loop);
        Assert.Equal(MicroReduce.Collapse, MicroPresets.FocusGlow.Reduce);
    }

    [Fact]
    public void Splat_helpers_emit_the_table_class()
    {
        Assert.Equal("motion-shake", Motion.Shake()["class"]);
        Assert.Equal("motion-pulse", Motion.Pulse()["class"]);
        Assert.Equal("motion-shimmer", Motion.Shimmer()["class"]);
        Assert.Equal("motion-focus-glow", Motion.FocusGlow()["class"]);

        // Class-only (no inline style), like Presence.
        Assert.False(Motion.Shake().ContainsKey("style"));
    }

    [Fact]
    public void Micro_program_carries_the_shake_keyframes_as_a_one_shot()
    {
        var program = MotionPrograms.Micro(MicroPresets.Shake);

        Assert.Equal(8, program.Keyframes.Count);
        Assert.False(program.Loop);
        Assert.Equal(450, program.DurationMs);
        Assert.StartsWith("cubic-bezier(", program.Easing);

        // First frame: offset 0, a transform, no opacity.
        var first = program.Keyframes[0];
        Assert.Equal(0d, first["offset"]);
        Assert.Equal("translateX(0)", first["transform"]);
        Assert.False(first.ContainsKey("opacity"));

        // Ends at identity.
        Assert.Equal("translateX(0)", program.Keyframes[^1]["transform"]);
        Assert.Equal(1d, program.Keyframes[^1]["offset"]);
    }

    [Fact]
    public void Micro_program_carries_the_pulse_keyframes_as_a_loop_with_opacity()
    {
        var program = MotionPrograms.Micro(MicroPresets.Pulse);

        Assert.True(program.Loop);
        Assert.Equal(3, program.Keyframes.Count);

        var mid = program.Keyframes[1];
        Assert.Equal(0.5d, mid["offset"]);
        Assert.Equal("scale(0.85)", mid["transform"]);
        Assert.Equal("0.5", mid["opacity"]);
    }
}
