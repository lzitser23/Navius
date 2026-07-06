namespace Navius.Motion;

/// <summary>
/// Stateless closed-form damped harmonic oscillator: evaluates a <see cref="Spring"/>
/// run from <see cref="Origin"/> to <see cref="Target"/> at any time without stepping.
/// Position and velocity are exact analytic solutions for all three damping regimes
/// (Motion's spring generator formulas), which is what makes retargeting with velocity
/// carry-over and CSS baking possible. Time is in seconds, velocity in value units per
/// second. The same evaluator is deliberately duplicated in navius-motion.js for
/// interruption handling; the two must agree (see the JS agreement test).
/// </summary>
public sealed class SpringSolver
{
    // Cap on the sinh/cosh argument in the overdamped regime so the envelope
    // product stays finite (Motion's freqForT guard).
    private const double MaxHyperbolicArg = 300;

    // Settle stepping: sample every 10ms, give up at 20s (Motion's
    // calcGeneratorDuration cap).
    private const double SettleStepSeconds = 0.01;
    private const double MaxSettleSeconds = 20;

    private readonly double _dampingRatio;
    private readonly double _undampedFreq;
    private readonly double _delta;
    private readonly double _initialVelocity;
    private double _settleDuration = -1;

    /// <summary>Create a solver for one spring run. Initial velocity comes from the spring.</summary>
    public SpringSolver(Spring spring, double origin = 0, double target = 1)
    {
        Spring = spring;
        Origin = origin;
        Target = target;
        _dampingRatio = spring.DampingRatio;
        _undampedFreq = spring.AngularFrequency;
        _delta = target - origin;
        _initialVelocity = spring.InitialVelocity;

        // Motion's granular rest thresholds: small value ranges (|delta| < 5, e.g.
        // opacity or scale) need much tighter rest detection than pixel ranges.
        var granular = Math.Abs(_delta) < 5;
        RestSpeed = granular ? 0.01 : 2;
        RestDelta = granular ? 0.005 : 0.5;
    }

    /// <summary>The spring being evaluated.</summary>
    public Spring Spring { get; }

    /// <summary>Start value.</summary>
    public double Origin { get; }

    /// <summary>End value.</summary>
    public double Target { get; }

    /// <summary>Rest velocity threshold in units per second (granular-aware default).</summary>
    public double RestSpeed { get; init; }

    /// <summary>Rest displacement threshold in value units (granular-aware default).</summary>
    public double RestDelta { get; init; }

    /// <summary>Position at time <paramref name="t"/> seconds.</summary>
    public double Position(double t)
    {
        var zw = _dampingRatio * _undampedFreq;
        if (_dampingRatio < 1)
        {
            var wd = _undampedFreq * Math.Sqrt(1 - _dampingRatio * _dampingRatio);
            var envelope = Math.Exp(-zw * t);
            var a = (zw * _delta - _initialVelocity) / wd;
            return Target - envelope * (a * Math.Sin(wd * t) + _delta * Math.Cos(wd * t));
        }
        if (_dampingRatio == 1)
        {
            var envelope = Math.Exp(-_undampedFreq * t);
            var b = _undampedFreq * _delta - _initialVelocity;
            return Target - envelope * (_delta + b * t);
        }
        else
        {
            var wd = _undampedFreq * Math.Sqrt(_dampingRatio * _dampingRatio - 1);
            var envelope = Math.Exp(-zw * t);
            var freqForT = Math.Min(wd * t, MaxHyperbolicArg);
            var c = zw * _delta - _initialVelocity;
            return Target - envelope * (c * Math.Sinh(freqForT) + wd * _delta * Math.Cosh(freqForT)) / wd;
        }
    }

    /// <summary>Velocity at time <paramref name="t"/> seconds (analytic derivative, units per second).</summary>
    public double Velocity(double t)
    {
        var zw = _dampingRatio * _undampedFreq;
        if (_dampingRatio < 1)
        {
            var wd = _undampedFreq * Math.Sqrt(1 - _dampingRatio * _dampingRatio);
            var envelope = Math.Exp(-zw * t);
            var a = (zw * _delta - _initialVelocity) / wd;
            return envelope * ((zw * a + _delta * wd) * Math.Sin(wd * t)
                + (zw * _delta - a * wd) * Math.Cos(wd * t));
        }
        if (_dampingRatio == 1)
        {
            var envelope = Math.Exp(-_undampedFreq * t);
            var b = _undampedFreq * _delta - _initialVelocity;
            return envelope * (_undampedFreq * _delta + _undampedFreq * b * t - b);
        }
        else
        {
            var wd = _undampedFreq * Math.Sqrt(_dampingRatio * _dampingRatio - 1);
            var envelope = Math.Exp(-zw * t);
            var freqForT = Math.Min(wd * t, MaxHyperbolicArg);
            var c = zw * _delta - _initialVelocity;
            return envelope * (((zw * c - wd * wd * _delta) / wd) * Math.Sinh(freqForT)
                + (zw * _delta - c) * Math.Cosh(freqForT));
        }
    }

    /// <summary>
    /// True when the run is settled: |velocity| within <see cref="RestSpeed"/> and
    /// |target - position| within <see cref="RestDelta"/> (Motion's done check).
    /// </summary>
    public bool IsAtRest(double position, double velocity)
        => Math.Abs(velocity) <= RestSpeed && Math.Abs(Target - position) <= RestDelta;

    /// <summary>
    /// The real settle duration in seconds. Duration-resolved springs report their
    /// clamped requested duration; physics springs step in 10ms increments until
    /// <see cref="IsAtRest"/>, capped at 20 seconds.
    /// </summary>
    public double SettleDuration
    {
        get
        {
            if (_settleDuration >= 0)
            {
                return _settleDuration;
            }
            if (Spring.IsDurationResolved)
            {
                return _settleDuration = Spring.ResolvedDurationSeconds;
            }
            for (var i = 0; ; i++)
            {
                var t = i * SettleStepSeconds;
                if (t >= MaxSettleSeconds)
                {
                    return _settleDuration = MaxSettleSeconds;
                }
                if (IsAtRest(Position(t), Velocity(t)))
                {
                    return _settleDuration = t;
                }
            }
        }
    }
}
