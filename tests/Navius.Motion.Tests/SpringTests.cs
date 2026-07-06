namespace Navius.Motion.Tests;

public class SpringTests
{
    [Theory]
    [InlineData(0.3, 0.2)]
    [InlineData(0.5, 0.25)]
    [InlineData(0.8, 0.6)]
    public void FromDuration_round_trips_the_requested_duration(double duration, double bounce)
    {
        var spring = Spring.FromDuration(duration, bounce);
        Assert.True(spring.IsDurationResolved);
        Assert.Equal(duration, spring.ResolvedDurationSeconds);
        Assert.True(spring.Stiffness > 0);
        Assert.True(spring.Damping > 0);

        // The solver honours the requested duration and the run is essentially
        // settled there (the Newton-Raphson envelope solves for a 0.001 amplitude).
        var solver = new SpringSolver(spring, 0, 1);
        Assert.Equal(duration, solver.SettleDuration);
        Assert.True(Math.Abs(1 - solver.Position(duration)) < 0.01);
        Assert.True(Math.Abs(1 - solver.Position(duration * 2)) < 0.005);
    }

    [Fact]
    public void FromDuration_clamps_duration_to_ten_seconds()
    {
        Assert.Equal(10, Spring.FromDuration(50).ResolvedDurationSeconds);
        Assert.Equal(0.01, Spring.FromDuration(0.001).ResolvedDurationSeconds);
    }

    [Fact]
    public void FromDuration_clamps_bounce_into_the_damping_window()
    {
        Assert.Equal(1.0, Spring.FromDuration(0.5, bounce: 0).DampingRatio, 1e-9);
        Assert.Equal(0.05, Spring.FromDuration(0.5, bounce: 1.5).DampingRatio, 1e-9);
    }

    [Fact]
    public void FromVisualDuration_matches_the_shortcut_formula()
    {
        // stiffness = (2*pi / (0.5 * 1.2))^2, damping = 2 * (1 - 0.25) * sqrt(stiffness),
        // hand-derived: 109.6622711232151 and 15.707963267948966.
        var spring = Spring.FromVisualDuration(0.5, 0.25);
        Assert.Equal(109.6622711232151, spring.Stiffness, 1e-9);
        Assert.Equal(15.707963267948966, spring.Damping, 1e-9);
        Assert.Equal(1, spring.Mass);
        Assert.False(spring.IsDurationResolved);
        Assert.Equal(0.5, spring.VisualDurationSeconds, 1e-12);
    }

    [Fact]
    public void Presets_have_their_documented_damping_ratios()
    {
        Assert.Equal(0.8, Spring.Default.DampingRatio, 1e-9);
        Assert.Equal(1.0, Spring.Smooth.DampingRatio, 1e-9);
        Assert.Equal(0.9, Spring.Snappy.DampingRatio, 1e-9);
        Assert.Equal(0.55, Spring.Bouncy.DampingRatio, 1e-9);
    }

    [Fact]
    public void Presets_feel_like_their_names()
    {
        double Overshoot(Spring spring)
        {
            var solver = new SpringSolver(spring, 0, 1);
            var max = 0.0;
            for (var t = 0.0; t <= solver.SettleDuration; t += 0.01)
            {
                max = Math.Max(max, solver.Position(t));
            }
            return max - 1;
        }

        Assert.True(Overshoot(Spring.Smooth) <= 0);               // never overshoots
        Assert.True(Overshoot(Spring.Bouncy) > 0.1);              // clearly bounces
        Assert.True(Overshoot(Spring.Snappy) < 0.01);             // barely overshoots
        Assert.True(new SpringSolver(Spring.Snappy, 0, 1).SettleDuration
            < new SpringSolver(Spring.Smooth, 0, 1).SettleDuration); // and is faster
    }

    [Fact]
    public void WithInitialVelocity_only_changes_velocity()
    {
        var spring = Spring.Snappy.WithInitialVelocity(3);
        Assert.Equal(Spring.Snappy.Stiffness, spring.Stiffness);
        Assert.Equal(Spring.Snappy.Damping, spring.Damping);
        Assert.Equal(3, spring.InitialVelocity);
    }
}
