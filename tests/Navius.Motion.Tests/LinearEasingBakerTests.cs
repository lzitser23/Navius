using System.Globalization;
using System.Text.RegularExpressions;

namespace Navius.Motion.Tests;

public class LinearEasingBakerTests
{
    [Fact]
    public void Point_count_keeps_points_per_second_constant()
    {
        var baked = LinearEasingBaker.Bake(Spring.Smooth);
        var solver = new SpringSolver(Spring.Smooth, 0, 1);
        var expected = Math.Max((int)Math.Round(solver.SettleDuration / 0.01, MidpointRounding.AwayFromZero), 2);
        Assert.Equal(expected, baked.Points.Count);

        // Halving the resolution halves the point count (same duration).
        var coarse = LinearEasingBaker.Bake(Spring.Smooth, resolutionSeconds: 0.02);
        Assert.Equal(Math.Max((int)Math.Round(solver.SettleDuration / 0.02, MidpointRounding.AwayFromZero), 2), coarse.Points.Count);
        Assert.Equal(baked.DurationSeconds, coarse.DurationSeconds);
    }

    [Fact]
    public void Minimum_two_points()
    {
        // A 10ms duration-resolved spring yields a single sample slot; the baker
        // must still emit the two endpoints.
        var baked = LinearEasingBaker.Bake(Spring.FromDuration(0.01));
        Assert.Equal(2, baked.Points.Count);
        Assert.Equal("linear(0, 1)", baked.Easing);
    }

    [Fact]
    public void Points_are_rounded_to_four_decimals_and_endpoints_are_exact()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        Assert.Equal(0, baked.Points[0]);
        Assert.Equal(1, baked.Points[^1]);
        foreach (var point in baked.Points)
        {
            Assert.Equal(Math.Round(point, 4), point, 1e-12);
        }
    }

    [Fact]
    public void Easing_string_has_the_linear_shape()
    {
        var baked = LinearEasingBaker.Bake(Spring.Snappy);
        Assert.Matches(@"^linear\(-?\d+(\.\d{1,4})?(, -?\d+(\.\d{1,4})?)*\)$", baked.Easing);
        Assert.StartsWith("linear(0, ", baked.Easing);
        Assert.EndsWith(", 1)", baked.Easing);
    }

    [Fact]
    public void Formatting_is_invariant_under_a_comma_decimal_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var baked = LinearEasingBaker.Bake(Spring.Bouncy);
            Assert.DoesNotMatch(@"\d,\d", baked.Easing.Replace(", ", "|"));
            Assert.Matches(@"^linear\(-?\d+(\.\d{1,4})?(, -?\d+(\.\d{1,4})?)*\)$", baked.Easing);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Duration_milliseconds_rounds_the_seconds()
    {
        var baked = LinearEasingBaker.Bake(Spring.Smooth);
        Assert.Equal((int)Math.Round(baked.DurationSeconds * 1000, MidpointRounding.AwayFromZero), baked.DurationMilliseconds);
        Assert.True(baked.DurationMilliseconds > 0);
    }

    [Fact]
    public void MapTo_produces_the_keyframes_fallback_values()
    {
        var baked = LinearEasingBaker.Bake(Spring.Smooth);
        var values = baked.MapTo(10, 20);
        Assert.Equal(baked.Points.Count, values.Length);
        Assert.Equal(10, values[0]);
        Assert.Equal(20, values[^1]);
        Assert.Equal(10 + baked.Points[5] * 10, values[5], 1e-12);
    }

    [Fact]
    public void Perceptual_multiplier_is_real_over_visual_duration()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        Assert.Equal(baked.DurationSeconds / Spring.Bouncy.VisualDurationSeconds,
            baked.PerceptualDurationMultiplier, 1e-12);
        Assert.True(baked.PerceptualDurationMultiplier > 1);
    }

    [Fact]
    public void Baking_a_motionless_run_throws()
    {
        Assert.Throws<ArgumentException>(() => LinearEasingBaker.Bake(new SpringSolver(Spring.Default, 1, 1)));
    }

    [Fact]
    public void Bouncy_curve_actually_overshoots_in_the_baked_points()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        Assert.Contains(baked.Points, p => p > 1.05);
    }
}
