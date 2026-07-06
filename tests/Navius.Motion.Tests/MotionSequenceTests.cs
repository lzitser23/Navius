using Navius.Motion.Interop;

namespace Navius.Motion.Tests;

public class MotionSequenceTests
{
    private static readonly MotionFrame[] Frames =
    [
        new(Opacity: 0, Transform: "translateY(8px)"),
        new(Opacity: 1, Transform: "none"),
    ];

    [Fact]
    public void Default_at_plays_segments_sequentially()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 200, "linear")
            .To("#b", Frames, 100, "linear")
            .To("#c", Frames, 50, "linear")
            .Build();

        Assert.Equal(0, program.Segments[0].StartMs);
        Assert.Equal(200, program.Segments[1].StartMs); // after #a (ends 200)
        Assert.Equal(300, program.Segments[2].StartMs); // after #b (ends 300)
        Assert.Equal(350, program.TotalMs);             // #c ends 350
    }

    [Fact]
    public void Relative_offsets_resolve_against_the_previous_segment_end()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 200, "linear")          // 0 -> 200
            .To("#b", Frames, 100, "linear", at: "+50")  // 200 + 50 = 250 -> 350
            .To("#c", Frames, 100, "linear", at: "-30")  // 350 - 30 = 320 -> 420
            .Build();

        Assert.Equal(0, program.Segments[0].StartMs);
        Assert.Equal(250, program.Segments[1].StartMs);
        Assert.Equal(320, program.Segments[2].StartMs);
        Assert.Equal(420, program.TotalMs);
    }

    [Fact]
    public void Caret_starts_together_with_the_previous_segment()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 200, "linear")            // 0 -> 200
            .To("#b", Frames, 120, "linear", at: "+80")    // 280 -> 400
            .To("#c", Frames, 60, "linear", at: "<")       // with #b: starts at 280
            .Build();

        Assert.Equal(280, program.Segments[1].StartMs);
        Assert.Equal(280, program.Segments[2].StartMs); // "<" == previous start
    }

    [Fact]
    public void Absolute_number_and_labels_resolve()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 200, "linear")             // 0 -> 200
            .Label("mid")                                 // mid = 200 (the cursor)
            .To("#b", Frames, 100, "linear")             // 200 -> 300
            .To("#c", Frames, 50, "linear", at: "600")      // absolute
            .To("#d", Frames, 50, "linear", at: "mid")      // label -> 200
            .Build();

        Assert.Equal(200, program.Segments[1].StartMs);
        Assert.Equal(600, program.Segments[2].StartMs);
        Assert.Equal(200, program.Segments[3].StartMs);
        Assert.Equal(650, program.TotalMs); // #c ends 650, the latest
    }

    [Fact]
    public void Negative_resolved_start_is_clamped_to_zero()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 100, "linear")               // 0 -> 100
            .To("#b", Frames, 50, "linear", at: "-1000")      // 100 - 1000 -> clamped 0
            .Build();

        Assert.Equal(0, program.Segments[1].StartMs);
    }

    [Fact]
    public void Spring_segments_bake_to_the_solver_duration_and_easing()
    {
        var baked = LinearEasingBaker.Bake(Spring.Bouncy);
        var program = new MotionSequence()
            .To("#a", Frames, Spring.Bouncy)
            .Build();

        Assert.Equal(baked.DurationMilliseconds, program.Segments[0].DurationMs);
        Assert.Equal(baked.Easing, program.Segments[0].Easing);
        Assert.StartsWith("linear(", program.Segments[0].Easing);
    }

    [Fact]
    public void Targets_carry_the_selector_and_leave_the_ref_null()
    {
        var program = new MotionSequence()
            .To("#a", Frames, 100, "linear")
            .Build();

        Assert.Equal("#a", program.Segments[0].Target.Selector);
        Assert.Null(program.Segments[0].Target.Ref);
        Assert.Equal("replace", program.Segments[0].Composite);
    }

    [Fact]
    public void Reduce_motion_defaults_to_user_and_is_settable()
    {
        Assert.Equal("user", new MotionSequence().Build().ReduceMotion);
        var seq = new MotionSequence { ReduceMotion = "always" };
        Assert.Equal("always", seq.Build().ReduceMotion);
    }
}
