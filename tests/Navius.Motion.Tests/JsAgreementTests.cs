using System.Diagnostics;
using System.Text.Json;

namespace Navius.Motion.Tests;

/// <summary>
/// The duplication contract: navius-motion.js deliberately duplicates the C#
/// closed-form evaluator so retarget() can carry position and velocity over without
/// an interop round trip. These tests run the real module under node and compare the
/// two implementations on a sampled grid across all three damping regimes.
/// </summary>
public class JsAgreementTests
{
    private const double Tolerance = 1e-9;

    private static readonly (Spring Spring, double Origin, double Target)[] Runs =
    [
        (Spring.Physics(100, 10), 0, 1),                          // underdamped, granular
        (Spring.Physics(100, 20), 0, 1),                          // critically damped
        (Spring.Physics(100, 25), 0, 1),                          // overdamped
        (Spring.Physics(200, 12, initialVelocity: 4), 0.3, 1),    // velocity carry
        (Spring.Physics(300, 24, initialVelocity: -50), 0, 240),  // coarse pixel range
        (Spring.Bouncy, 1, 0),                                    // preset, reversed
    ];

    private static readonly double[] Times = [0, 0.05, 0.123, 0.25, 0.5, 1.0];

    [Fact]
    public void Evaluators_agree_across_regimes_and_times()
    {
        var evals = new List<object>();
        var expected = new List<(double Position, double Velocity)>();
        foreach (var (spring, origin, target) in Runs)
        {
            var solver = new SpringSolver(spring, origin, target);
            foreach (var t in Times)
            {
                evals.Add(new { spring = Params(spring, origin, target), t });
                expected.Add((solver.Position(t), solver.Velocity(t)));
            }
        }

        using var result = RunNode(new { evals, bake = (object?)null });
        var jsEvals = result.RootElement.GetProperty("evals");
        Assert.Equal(expected.Count, jsEvals.GetArrayLength());
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Position, jsEvals[i].GetProperty("position").GetDouble(), Tolerance);
            Assert.Equal(expected[i].Velocity, jsEvals[i].GetProperty("velocity").GetDouble(), Tolerance);
        }
    }

    [Fact]
    public void Bakers_agree_on_duration_and_points()
    {
        var spring = Spring.Snappy;
        var baked = LinearEasingBaker.Bake(spring);

        using var result = RunNode(new
        {
            evals = Array.Empty<object>(),
            bake = Params(spring, 0, 1),
        });
        var jsBake = result.RootElement.GetProperty("bake");

        Assert.Equal(baked.DurationMilliseconds, jsBake.GetProperty("durationMs").GetInt32());
        var jsPoints = jsBake.GetProperty("points");
        Assert.Equal(baked.Points.Count, jsPoints.GetArrayLength());
        for (var i = 0; i < baked.Points.Count; i++)
        {
            // Both sides round to 4 decimals; allow the rounding boundary either way.
            Assert.Equal(baked.Points[i], jsPoints[i].GetDouble(), 0.0001);
        }
    }

    [Fact]
    public void Stagger_schedules_agree()
    {
        // The C# authoring formula (MotionStagger.Delays) and the JS runtime formula
        // (staggerDelays) must produce identical per-child delays across anchors, counts
        // and steps, incl. the half-step even-count centre case.
        var cases = new[]
        {
            (Count: 5, Step: 50.0, From: StaggerFrom.First),
            (Count: 6, Step: 40.0, From: StaggerFrom.Last),
            (Count: 5, Step: 100.0, From: StaggerFrom.Center),
            (Count: 4, Step: 100.0, From: StaggerFrom.Center),
            (Count: 1, Step: 50.0, From: StaggerFrom.Center),
        };

        using var result = RunNode(new
        {
            evals = Array.Empty<object>(),
            bake = (object?)null,
            staggers = cases.Select(c => new { count = c.Count, step = c.Step, from = c.From.ToToken() }).ToArray(),
        });

        var jsStaggers = result.RootElement.GetProperty("staggers");
        Assert.Equal(cases.Length, jsStaggers.GetArrayLength());
        for (var i = 0; i < cases.Length; i++)
        {
            var expected = MotionStagger.Delays(cases[i].Count, cases[i].Step, cases[i].From);
            var actual = jsStaggers[i];
            Assert.Equal(expected.Length, actual.GetArrayLength());
            for (var j = 0; j < expected.Length; j++)
            {
                Assert.Equal(expected[j], actual[j].GetDouble(), 1e-9);
            }
        }
    }

    private static object Params(Spring spring, double origin, double target) => new
    {
        stiffness = spring.Stiffness,
        damping = spring.Damping,
        mass = spring.Mass,
        velocity = spring.InitialVelocity,
        origin,
        target,
    };

    private static JsonDocument RunNode(object input)
    {
        var runner = Path.Combine(AppContext.BaseDirectory, "tools", "eval-spring.mjs");
        var casesPath = Path.Combine(AppContext.BaseDirectory, $"spring-cases-{Guid.NewGuid():N}.json");
        File.WriteAllText(casesPath, JsonSerializer.Serialize(input));
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(runner);
            psi.ArgumentList.Add(RepoPaths.EngineModule);
            psi.ArgumentList.Add(casesPath);

            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"node failed ({process.ExitCode}): {stderr}");
            return JsonDocument.Parse(stdout);
        }
        finally
        {
            File.Delete(casesPath);
        }
    }
}
