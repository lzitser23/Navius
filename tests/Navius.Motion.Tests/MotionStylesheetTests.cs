using System.Globalization;

namespace Navius.Motion.Tests;

public class MotionStylesheetTests
{
    [Fact]
    public void Generation_is_deterministic()
    {
        Assert.Equal(MotionStylesheet.Generate(), MotionStylesheet.Generate());
    }

    [Fact]
    public void Generation_is_culture_independent()
    {
        var invariant = MotionStylesheet.Generate();
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(invariant, MotionStylesheet.Generate());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Committed_stylesheet_matches_the_generator()
    {
        // Regenerate with: dotnet run --project src/Navius.Motion.CssGen
        var committed = File.ReadAllText(RepoPaths.CommittedStylesheet);
        Assert.Equal(MotionStylesheet.Generate(), committed);
    }

    [Fact]
    public void Snapshot_contains_every_tier()
    {
        var css = MotionStylesheet.Generate();

        // Generated banner + regen command.
        Assert.Contains("GENERATED, do not edit by hand", css);
        Assert.Contains("dotnet run --project src/Navius.Motion.CssGen", css);

        // Spring variables for all four named springs.
        foreach (var name in new[] { "default", "smooth", "snappy", "bouncy" })
        {
            Assert.Contains($"--navius-spring-{name}: linear(", css);
            Assert.Contains($"--navius-spring-{name}-duration:", css);
        }

        // Every preset in both tiers, keyed to the discrete state attributes.
        foreach (var preset in MotionPresets.All)
        {
            Assert.Contains($".{preset.PresenceClass}[data-open]", css);
            Assert.Contains($".{preset.PresenceClass}[data-starting-style]", css);
            Assert.Contains($".{preset.PresenceClass}[data-closed]", css);
            Assert.Contains($".{preset.PresenceClass}[data-ending-style]", css);
            Assert.Contains($".{preset.EnterClass}", css);
        }
        Assert.Contains("@starting-style", css);

        // Gestures and the reduced-motion collapse.
        Assert.Contains(".motion-press:active", css);
        Assert.Contains(".motion-hover:hover", css);
        Assert.Contains("@media (hover: hover)", css);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("transition-property: opacity;", css);
    }
}
