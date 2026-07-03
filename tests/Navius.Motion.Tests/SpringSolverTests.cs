namespace Navius.Motion.Tests;

/// <summary>
/// Golden-value tests for the closed-form solver. Every expected literal below was
/// hand-derived from the damped harmonic oscillator solutions (the same formulas
/// Motion's spring generator uses), evaluated independently of the code under test:
///
///   underdamped (zeta &lt; 1), origin o, target T, delta = T - o, v0 = dx/dt(0):
///     wd = w0 * sqrt(1 - zeta^2)
///     x(t) = T - e^(-zeta*w0*t) * (A*sin(wd*t) + delta*cos(wd*t)),
///       A = (zeta*w0*delta - v0) / wd
///     x'(t) = e^(-zeta*w0*t) * ((zeta*w0*A + delta*wd)*sin(wd*t)
///       + (zeta*w0*delta - A*wd)*cos(wd*t))
///   critically damped (zeta = 1):
///     x(t) = T - e^(-w0*t) * (delta + B*t), B = w0*delta - v0
///     x'(t) = e^(-w0*t) * (w0*delta + w0*B*t - B)
///   overdamped (zeta &gt; 1):
///     wd = w0 * sqrt(zeta^2 - 1), C = zeta*w0*delta - v0
///     x(t) = T - e^(-zeta*w0*t) * (C*sinh(wd*t) + wd*delta*cosh(wd*t)) / wd
///     x'(t) = e^(-zeta*w0*t) * (((zeta*w0*C - wd^2*delta)/wd)*sinh(wd*t)
///       + (zeta*w0*delta - C)*cosh(wd*t))
/// </summary>
public class SpringSolverTests
{
    private const double PositionTolerance = 1e-9;
    private const double VelocityTolerance = 1e-8;

    // k=100, c=10, m=1 (zeta = 0.5, w0 = 10, wd = 10*sqrt(0.75)), 0 -> 1, v0 = 0.
    // x(t) = 1 - e^(-5t) * ((5/wd)*sin(wd*t) + cos(wd*t))
    // x'(t) = e^(-5t) * (100/wd) * sin(wd*t)
    [Theory]
    [InlineData(0.1, 0.34029984660829826, 5.33507195114693)]
    [InlineData(0.3, 1.1243547674084118, 1.3324264401804122)]
    [InlineData(0.5, 1.0745905665950333, -0.8794242073251286)]
    public void Underdamped_matches_closed_form(double t, double expectedX, double expectedV)
    {
        var solver = new SpringSolver(Spring.Physics(100, 10), 0, 1);
        Assert.Equal(expectedX, solver.Position(t), PositionTolerance);
        Assert.Equal(expectedV, solver.Velocity(t), VelocityTolerance);
    }

    // k=100, c=20, m=1 (zeta = 1, w0 = 10), 0 -> 1, v0 = 0.
    // x(t) = 1 - e^(-10t) * (1 + 10t); x'(t) = 100t * e^(-10t)
    [Theory]
    [InlineData(0.05, 0.09020401043104986, 3.032653298563167)]
    [InlineData(0.1, 0.26424111765711533, 3.6787944117144233)]
    [InlineData(0.3, 0.8008517265285442, 1.4936120510359183)]
    public void Critically_damped_matches_closed_form(double t, double expectedX, double expectedV)
    {
        var solver = new SpringSolver(Spring.Physics(100, 20), 0, 1);
        Assert.Equal(1.0, solver.Spring.DampingRatio, 1e-12);
        Assert.Equal(expectedX, solver.Position(t), PositionTolerance);
        Assert.Equal(expectedV, solver.Velocity(t), VelocityTolerance);
    }

    // k=100, c=25, m=1 (zeta = 1.25, w0 = 10, wd = 7.5), 0 -> 1, v0 = 0.
    // x(t) = 1 - (e^(-12.5t)/7.5) * (12.5*sinh(7.5t) + 7.5*cosh(7.5t))
    // x'(t) = e^(-12.5t) * (100/7.5) * sinh(7.5t)
    [Theory]
    [InlineData(0.05, 0.08422543629527424, 2.7394756126664173)]
    [InlineData(0.1, 0.23640421479535967, 3.1413025098401386)]
    [InlineData(0.2, 0.5155992914009884, 2.3304253485513877)]
    public void Overdamped_matches_closed_form(double t, double expectedX, double expectedV)
    {
        var solver = new SpringSolver(Spring.Physics(100, 25), 0, 1);
        Assert.Equal(expectedX, solver.Position(t), PositionTolerance);
        Assert.Equal(expectedV, solver.Velocity(t), VelocityTolerance);
    }

    // Velocity carry-over: k=200, c=12, m=1 (zeta*w0 = 6, wd = sqrt(164)),
    // 0.3 -> 1 (delta = 0.7), v0 = 4. A = (6*0.7 - 4)/wd = 0.2/wd.
    // x(t) = 1 - e^(-6t) * (A*sin(wd*t) + 0.7*cos(wd*t))
    // x'(t) = e^(-6t) * ((6A + 0.7*wd)*sin(wd*t) + 4*cos(wd*t))
    [Theory]
    [InlineData(0.1, 0.8818704538091464, 5.391454068221663)]
    [InlineData(0.25, 1.1561191891657752, -1.0120496937873442)]
    public void Initial_velocity_carries_into_the_curve(double t, double expectedX, double expectedV)
    {
        var solver = new SpringSolver(Spring.Physics(200, 12, initialVelocity: 4), 0.3, 1);
        Assert.Equal(expectedX, solver.Position(t), PositionTolerance);
        Assert.Equal(expectedV, solver.Velocity(t), VelocityTolerance);
    }

    [Fact]
    public void Boundary_conditions_hold_in_every_regime()
    {
        foreach (var spring in new[]
        {
            Spring.Physics(100, 10, initialVelocity: 4),   // underdamped
            Spring.Physics(100, 20, initialVelocity: 4),   // critical
            Spring.Physics(100, 25, initialVelocity: 4),   // overdamped
        })
        {
            var solver = new SpringSolver(spring, 0.25, 1);
            Assert.Equal(0.25, solver.Position(0), 1e-12);
            Assert.Equal(4, solver.Velocity(0), 1e-12);
        }
    }

    [Fact]
    public void Overdamped_far_future_stays_finite()
    {
        // The sinh/cosh argument is capped at 300; without it this would overflow.
        var solver = new SpringSolver(Spring.Physics(100, 25), 0, 1);
        Assert.True(double.IsFinite(solver.Position(1000)));
        Assert.True(double.IsFinite(solver.Velocity(1000)));
    }

    [Fact]
    public void Rest_thresholds_are_granular_for_small_ranges()
    {
        var granular = new SpringSolver(Spring.Default, 0, 1);
        Assert.Equal(0.01, granular.RestSpeed);
        Assert.Equal(0.005, granular.RestDelta);

        var coarse = new SpringSolver(Spring.Default, 0, 100);
        Assert.Equal(2, coarse.RestSpeed);
        Assert.Equal(0.5, coarse.RestDelta);
    }

    [Fact]
    public void IsAtRest_requires_both_thresholds()
    {
        var solver = new SpringSolver(Spring.Default, 0, 1);
        Assert.True(solver.IsAtRest(position: 0.999, velocity: 0.001));
        Assert.False(solver.IsAtRest(position: 0.999, velocity: 5));   // still moving
        Assert.False(solver.IsAtRest(position: 0.5, velocity: 0.001)); // still far away
    }

    [Fact]
    public void Settle_duration_lands_at_rest_and_is_step_aligned()
    {
        var solver = new SpringSolver(Spring.Physics(100, 10), 0, 1);
        var settle = solver.SettleDuration;
        Assert.InRange(settle, 0.01, 20);
        Assert.Equal(0, Math.Round(settle / 0.01) - settle / 0.01, 1e-9); // multiple of 10ms
        Assert.True(solver.IsAtRest(solver.Position(settle), solver.Velocity(settle)));
    }

    [Fact]
    public void Settle_duration_is_capped_at_20_seconds()
    {
        // Almost undamped: the oscillation outlives the cap.
        var solver = new SpringSolver(Spring.Physics(100, 0.01), 0, 1);
        Assert.Equal(20, solver.SettleDuration);
    }
}
