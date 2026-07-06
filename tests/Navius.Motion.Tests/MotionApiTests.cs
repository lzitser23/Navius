using System.Globalization;
using Navius.Motion.Interop;

namespace Navius.Motion.Tests;

public class MotionApiTests
{
    [Fact]
    public void Enter_splat_emits_the_enter_class_and_delay_variable()
    {
        var attributes = Motion.Enter(Preset.FadeUp, delayMs: 80);
        Assert.Equal("motion-enter-fade-up", attributes["class"]);
        Assert.Equal("--navius-motion-delay: 80ms", attributes["style"]);

        var noDelay = Motion.Enter(Preset.Fade);
        Assert.Equal("motion-enter-fade", noDelay["class"]);
        Assert.False(noDelay.ContainsKey("style"));
    }

    [Fact]
    public void Presence_splat_emits_the_presence_class()
    {
        Assert.Equal("motion-pop", Motion.Presence(Preset.Pop)["class"]);
        Assert.Equal("motion-slide-left", Motion.Presence(Preset.SlideLeft)["class"]);
    }

    [Fact]
    public void InView_splat_emits_the_in_view_class_and_optional_delay()
    {
        var attributes = Motion.InView(Preset.SlideUp, delayMs: 120);
        Assert.Equal("motion-in-view-slide-up", attributes["class"]);
        Assert.Equal("--navius-motion-delay: 120ms", attributes["style"]);

        var noDelay = Motion.InView(Preset.Fade);
        Assert.Equal("motion-in-view-fade", noDelay["class"]);
        Assert.False(noDelay.ContainsKey("style"));
    }

    [Fact]
    public void Every_preset_exposes_a_distinct_in_view_class()
    {
        foreach (var preset in MotionPresets.All)
        {
            Assert.Equal("motion-in-view-" + preset.Name, preset.InViewClass);
        }
    }

    [Fact]
    public void SelectionIndicator_options_bake_the_spring()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        var options = MotionPrograms.SelectionIndicator(Spring.Bouncy, activeSelector: "[data-selected]", axis: "x");

        Assert.Equal("[data-selected]", options.ActiveSelector);
        Assert.Equal("x", options.Axis);
        Assert.Equal(baked.DurationMilliseconds, options.DurationMs);
        Assert.Equal(baked.Easing, options.Easing);

        // Default spring is snappy.
        Assert.Equal(LinearEasingBaker.Bake(Spring.Snappy).Easing, MotionPrograms.SelectionIndicator().Easing);
    }

    [Fact]
    public void Press_and_hover_splats_format_invariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("--navius-motion-press-scale: 0.95", Motion.Press(0.95)["style"]);
            Assert.Equal("--navius-motion-hover-lift: 2.5", Motion.Hover(2.5)["style"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
        Assert.Equal("motion-press", Motion.Press()["class"]);
        Assert.Equal("motion-hover", Motion.Hover()["class"]);
    }

    [Fact]
    public void Every_preset_enum_value_resolves_and_the_table_is_ordered()
    {
        foreach (var value in Enum.GetValues<Preset>())
        {
            Assert.NotNull(MotionPresets.Get(value));
        }
        Assert.Equal(9, MotionPresets.All.Count);
        Assert.Equal("fade", MotionPresets.All[0].Name);
        Assert.Equal("slide-right", MotionPresets.All[^1].Name);
    }

    [Fact]
    public void Presence_program_carries_both_phases_of_the_preset()
    {
        var program = MotionPrograms.Presence(MotionPresets.Pop);

        Assert.Equal(2, program.Enter.Keyframes.Length);
        Assert.Equal(0, program.Enter.Keyframes[0].Opacity);
        Assert.Equal("scale(0.9)", program.Enter.Keyframes[0].Transform);
        Assert.Equal(1, program.Enter.Keyframes[1].Opacity);
        Assert.Equal("none", program.Enter.Keyframes[1].Transform);
        Assert.StartsWith("linear(", program.Enter.Easing);
        Assert.True(program.Enter.DurationMs > 0);

        Assert.Single(program.Exit.Keyframes);
        Assert.Equal(0, program.Exit.Keyframes[0].Opacity);
        Assert.True(program.Exit.DurationMs < program.Enter.DurationMs); // snappy out
    }

    [Fact]
    public void Enter_program_fills_backwards_and_honours_overrides()
    {
        var (keyframes, options) = MotionPrograms.Enter(MotionPresets.Fade, Spring.Snappy, delayMs: 120);
        Assert.Equal(2, keyframes.Length);
        Assert.Equal("backwards", options.Fill);
        Assert.Equal(120, options.DelayMs);
        Assert.Equal(LinearEasingBaker.Bake(Spring.Snappy).Easing, options.Easing);
    }

    [Fact]
    public void SpringParams_capture_a_solver_run()
    {
        var solver = new SpringSolver(Spring.Physics(200, 12, initialVelocity: 4), 0.3, 1);
        var p = SpringParams.From(solver);
        Assert.Equal(200, p.Stiffness);
        Assert.Equal(12, p.Damping);
        Assert.Equal(1, p.Mass);
        Assert.Equal(4, p.Velocity);
        Assert.Equal(0.3, p.Origin);
        Assert.Equal(1, p.Target);
    }
}
