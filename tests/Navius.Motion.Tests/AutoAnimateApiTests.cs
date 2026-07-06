using Navius.Motion.Interop;

namespace Navius.Motion.Tests;

public class AutoAnimateApiTests
{
    [Fact]
    public void AutoAnimate_defaults_match_the_reference()
    {
        var options = MotionPrograms.AutoAnimate();
        Assert.Equal(250, options.DurationMs);
        Assert.Equal("ease-in-out", options.Easing);
        Assert.Equal("user", options.ReduceMotion);
    }

    [Fact]
    public void AutoAnimate_bakes_a_spring_to_linear_easing_and_settle_duration()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        var options = MotionPrograms.AutoAnimate(Spring.Bouncy);

        Assert.Equal(baked.Easing, options.Easing);
        Assert.StartsWith("linear(", options.Easing);
        Assert.Equal(baked.DurationMilliseconds, options.DurationMs);
    }

    [Fact]
    public void AutoAnimate_overrides_apply_over_a_spring_bake()
    {
        var baked = LinearEasingBaker.Bake(Spring.Snappy);
        var options = MotionPrograms.AutoAnimate(Spring.Snappy, durationMs: 400);

        // Duration overridden, easing still the baked spring.
        Assert.Equal(400, options.DurationMs);
        Assert.Equal(baked.Easing, options.Easing);
    }

    [Fact]
    public void AutoAnimate_takes_explicit_duration_and_easing_without_a_spring()
    {
        var options = MotionPrograms.AutoAnimate(durationMs: 320, easing: "ease-out", reduceMotion: "always");
        Assert.Equal(320, options.DurationMs);
        Assert.Equal("ease-out", options.Easing);
        Assert.Equal("always", options.ReduceMotion);
    }

    [Fact]
    public void AutoAnimateOptions_record_defaults()
    {
        var options = new AutoAnimateOptions();
        Assert.Equal(250, options.DurationMs);
        Assert.Equal("ease-in-out", options.Easing);
        Assert.Equal("user", options.ReduceMotion);
    }
}
