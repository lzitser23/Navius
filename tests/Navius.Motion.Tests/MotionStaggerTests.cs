using Navius.Motion.Interop;

namespace Navius.Motion.Tests;

public class MotionStaggerTests
{
    [Fact]
    public void From_first_grows_with_index()
    {
        Assert.Equal(new double[] { 0, 50, 100, 150 }, MotionStagger.Delays(4, 50, StaggerFrom.First));
    }

    [Fact]
    public void From_last_grows_toward_zero()
    {
        Assert.Equal(new double[] { 150, 100, 50, 0 }, MotionStagger.Delays(4, 50, StaggerFrom.Last));
    }

    [Fact]
    public void From_center_odd_count_is_symmetric_about_the_middle()
    {
        // n = 5, centre index 2: distances 2,1,0,1,2.
        Assert.Equal(new double[] { 200, 100, 0, 100, 200 }, MotionStagger.Delays(5, 100, StaggerFrom.Center));
    }

    [Fact]
    public void From_center_even_count_uses_half_steps()
    {
        // n = 4, centre 1.5: distances 1.5, 0.5, 0.5, 1.5.
        Assert.Equal(new double[] { 150, 50, 50, 150 }, MotionStagger.Delays(4, 100, StaggerFrom.Center));
    }

    [Fact]
    public void Zero_and_negative_counts_yield_empty()
    {
        Assert.Empty(MotionStagger.Delays(0, 50));
        Assert.Empty(MotionStagger.Delays(-3, 50));
    }

    [Fact]
    public void Single_child_has_no_delay_regardless_of_anchor()
    {
        Assert.Equal(new double[] { 0 }, MotionStagger.Delays(1, 50, StaggerFrom.First));
        Assert.Equal(new double[] { 0 }, MotionStagger.Delays(1, 50, StaggerFrom.Last));
        Assert.Equal(new double[] { 0 }, MotionStagger.Delays(1, 50, StaggerFrom.Center));
    }

    [Fact]
    public void Options_serialize_the_lowercase_anchor_token()
    {
        Assert.Equal("first", StaggerOptions.Of(50, StaggerFrom.First).From);
        Assert.Equal("last", StaggerOptions.Of(50, StaggerFrom.Last).From);
        Assert.Equal("center", StaggerOptions.Of(50, StaggerFrom.Center).From);
        Assert.Equal(50, StaggerOptions.Of(50, StaggerFrom.Center).Step);
    }
}
