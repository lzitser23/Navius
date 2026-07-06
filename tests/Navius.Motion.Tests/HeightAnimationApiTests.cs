using Navius.Motion.Interop;

namespace Navius.Motion.Tests;

public class HeightAnimationApiTests
{
    [Fact]
    public void HeightAnimation_defaults_match_a_calm_tween()
    {
        var options = MotionPrograms.HeightAnimation();
        Assert.Equal(300, options.DurationMs);
        Assert.Equal("ease", options.Easing);
        Assert.Equal("user", options.ReduceMotion);
        Assert.Null(options.Expanded);
    }

    [Fact]
    public void HeightAnimation_bakes_a_spring_to_linear_easing_and_settle_duration()
    {
        var baked = LinearEasingBaker.Bake(Spring.Smooth);
        var options = MotionPrograms.HeightAnimation(Spring.Smooth);

        Assert.Equal(baked.Easing, options.Easing);
        Assert.StartsWith("linear(", options.Easing);
        Assert.Equal(baked.DurationMilliseconds, options.DurationMs);
    }

    [Fact]
    public void HeightAnimation_overrides_apply_over_a_spring_bake()
    {
        var baked = LinearEasingBaker.Bake(Spring.Snappy);
        var options = MotionPrograms.HeightAnimation(Spring.Snappy, durationMs: 500);

        // Duration overridden, easing still the baked spring.
        Assert.Equal(500, options.DurationMs);
        Assert.Equal(baked.Easing, options.Easing);
    }

    [Fact]
    public void HeightAnimation_takes_explicit_timing_and_initial_mode_without_a_spring()
    {
        var options = MotionPrograms.HeightAnimation(
            durationMs: 200, easing: "ease-out", reduceMotion: "always", expanded: false);

        Assert.Equal(200, options.DurationMs);
        Assert.Equal("ease-out", options.Easing);
        Assert.Equal("always", options.ReduceMotion);
        Assert.False(options.Expanded);
    }

    [Fact]
    public void HeightAnimation_carries_the_initial_expanded_mode_through_a_spring_bake()
    {
        Assert.True(MotionPrograms.HeightAnimation(Spring.Smooth, expanded: true).Expanded);
        Assert.Null(MotionPrograms.HeightAnimation(Spring.Smooth).Expanded);
    }

    [Fact]
    public void HeightAnimationOptions_record_defaults()
    {
        var options = new HeightAnimationOptions();
        Assert.Equal(300, options.DurationMs);
        Assert.Equal("ease", options.Easing);
        Assert.Equal("user", options.ReduceMotion);
        Assert.Null(options.Expanded);
    }
}
