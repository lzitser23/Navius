using System.Globalization;
using System.Text;

namespace Navius.Motion;

/// <summary>
/// Generates <c>wwwroot/navius-motion.css</c>, the zero-JS tier: every
/// <see cref="MotionPresets"/> entry becomes a presence class keyed to the Navius
/// discrete state attributes plus an insert-only class built on @starting-style, with
/// spring easings baked to <c>linear()</c> once per named spring and shared through
/// CSS custom properties. Output is deterministic (fixed ordering, invariant culture):
/// run <c>Navius.Motion.CssGen</c> to regenerate the committed file, never hand-edit it.
/// </summary>
public static class MotionStylesheet
{
    private static readonly (string Name, Spring Spring)[] NamedSprings =
    [
        ("default", Spring.Default),
        ("smooth", Spring.Smooth),
        ("snappy", Spring.Snappy),
        ("bouncy", Spring.Bouncy),
    ];

    /// <summary>Generate the full stylesheet text (LF newlines, invariant culture).</summary>
    public static string Generate()
    {
        var baked = NamedSprings.ToDictionary(s => s.Name, s => LinearEasingBaker.Bake(s.Spring));
        var sb = new StringBuilder();

        sb.Append(
            "/*\n" +
            " * navius-motion.css (GENERATED, do not edit by hand)\n" +
            " *\n" +
            " * Spring physics computed in C# (Navius.Motion.SpringSolver) and baked into\n" +
            " * CSS linear() easings, so animation runs entirely on the compositor with\n" +
            " * zero JavaScript. Presence classes (.motion-*) key off the Navius discrete\n" +
            " * state attributes; enter classes (.motion-enter-*) use @starting-style and\n" +
            " * work on any element. Regenerate with:\n" +
            " *\n" +
            " *   dotnet run --project src/Navius.Motion.CssGen\n" +
            " */\n\n");

        AppendSpringVariables(sb, baked);
        AppendPresenceClasses(sb, baked);
        AppendEnterClasses(sb, baked);
        AppendGestureClasses(sb);
        AppendMicroKeyframes(sb);
        AppendMicroClasses(sb);
        AppendReducedMotion(sb);

        return sb.ToString();
    }

    private static void AppendSpringVariables(StringBuilder sb, Dictionary<string, BakedEasing> baked)
    {
        sb.Append("/* Baked spring easings. The duration variable is the real settle duration the\n");
        sb.Append(" * curve was sampled over; the multiplier comment is real / perceived duration. */\n");
        sb.Append(":root {\n");
        foreach (var (name, _) in NamedSprings)
        {
            var b = baked[name];
            sb.Append("  --navius-spring-").Append(name).Append(": ").Append(b.Easing).Append(";\n");
            sb.Append("  --navius-spring-").Append(name).Append("-duration: ")
                .Append(b.DurationMilliseconds.ToString(CultureInfo.InvariantCulture))
                .Append("ms; /* perceptual multiplier ")
                .Append(b.PerceptualDurationMultiplier.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" */\n");
        }
        sb.Append("}\n\n");
    }

    private static void AppendPresenceClasses(StringBuilder sb, Dictionary<string, BakedEasing> baked)
    {
        sb.Append("/* Presence presets: pair with elements that carry data-open / data-closed /\n");
        sb.Append(" * data-starting-style / data-ending-style (every Navius primitive part). */\n");
        foreach (var preset in MotionPresets.All)
        {
            var cls = "." + preset.PresenceClass;
            var enter = VarsFor(preset.EnterSpring, baked);
            var exit = VarsFor(preset.ExitSpring, baked);

            sb.Append(cls).Append(" {\n");
            sb.Append("  transition:\n");
            sb.Append("    opacity ").Append(enter.Duration).Append(' ').Append(enter.Easing).Append(",\n");
            sb.Append("    transform ").Append(enter.Duration).Append(' ').Append(enter.Easing).Append(";\n");
            sb.Append("}\n");

            sb.Append(cls).Append("[data-open] {\n");
            sb.Append("  opacity: 1;\n");
            sb.Append("  transform: none;\n");
            sb.Append("}\n");

            sb.Append(cls).Append("[data-starting-style],\n");
            sb.Append(cls).Append("[data-closed] {\n");
            AppendState(sb, preset.Hidden);
            sb.Append("}\n");

            sb.Append(cls).Append("[data-ending-style] {\n");
            sb.Append("  transition-duration: ").Append(exit.Duration).Append(";\n");
            sb.Append("  transition-timing-function: ").Append(exit.Easing).Append(";\n");
            sb.Append("}\n\n");
        }
    }

    private static void AppendEnterClasses(StringBuilder sb, Dictionary<string, BakedEasing> baked)
    {
        sb.Append("/* Insert-only enter presets: animate once when the element enters the DOM\n");
        sb.Append(" * (via @starting-style), no state attributes required. Delay with\n");
        sb.Append(" * --navius-motion-delay. */\n");
        foreach (var preset in MotionPresets.All)
        {
            var cls = "." + preset.EnterClass;
            var enter = VarsFor(preset.EnterSpring, baked);

            sb.Append(cls).Append(" {\n");
            sb.Append("  transition:\n");
            sb.Append("    opacity ").Append(enter.Duration).Append(' ').Append(enter.Easing).Append(",\n");
            sb.Append("    transform ").Append(enter.Duration).Append(' ').Append(enter.Easing).Append(";\n");
            sb.Append("  transition-delay: var(--navius-motion-delay, 0ms);\n");
            sb.Append("}\n");

            sb.Append("@starting-style {\n");
            sb.Append("  ").Append(cls).Append(" {\n");
            AppendState(sb, preset.Hidden, indent: "    ");
            sb.Append("  }\n");
            sb.Append("}\n\n");
        }
    }

    private static void AppendGestureClasses(StringBuilder sb)
    {
        sb.Append("/* Gesture micro-interactions (CSS tier). :active also covers keyboard\n");
        sb.Append(" * activation on buttons; the hover media query filters emulated touch hover. */\n");
        sb.Append(".motion-press {\n");
        sb.Append("  transition: transform var(--navius-spring-snappy-duration) var(--navius-spring-snappy);\n");
        sb.Append("}\n");
        sb.Append(".motion-press:active {\n");
        sb.Append("  transform: scale(var(--navius-motion-press-scale, 0.97));\n");
        sb.Append("}\n");
        sb.Append("@media (hover: hover) {\n");
        sb.Append("  .motion-hover {\n");
        sb.Append("    transition: transform var(--navius-spring-snappy-duration) var(--navius-spring-snappy);\n");
        sb.Append("  }\n");
        sb.Append("  .motion-hover:hover {\n");
        sb.Append("    transform: translateY(calc(-1px * var(--navius-motion-hover-lift, 2)));\n");
        sb.Append("  }\n");
        sb.Append("}\n\n");
    }

    private static void AppendMicroKeyframes(StringBuilder sb)
    {
        sb.Append("/* Micro pack (attention & ambient) keyframes. Authored once in\n");
        sb.Append(" * Navius.Motion.MicroPresets; the same presets feed the WAAPI runtime via\n");
        sb.Append(" * MotionPrograms.Micro, so both tiers animate identically. */\n");
        foreach (var preset in MicroPresets.All)
        {
            AppendKeyframesBlock(sb, preset.KeyframesName, preset.Keyframes, opacityOnly: false);
            if (preset.Reduce == MicroReduce.OpacityOnly)
            {
                AppendKeyframesBlock(sb, preset.ReducedKeyframesName, preset.Keyframes, opacityOnly: true);
            }
            sb.Append('\n');
        }
    }

    private static void AppendKeyframesBlock(
        StringBuilder sb, string name, IReadOnlyList<MicroFrame> frames, bool opacityOnly)
    {
        sb.Append("@keyframes ").Append(name).Append(" {\n");
        foreach (var frame in frames)
        {
            sb.Append("  ").Append(FormatOffset(frame.Offset)).Append(" { ");
            AppendFrameBody(sb, frame, opacityOnly);
            sb.Append("}\n");
        }
        sb.Append("}\n");
    }

    private static void AppendFrameBody(StringBuilder sb, MicroFrame frame, bool opacityOnly)
    {
        if (!opacityOnly && frame.Transform is not null)
        {
            sb.Append("transform: ").Append(frame.Transform).Append("; ");
        }
        if (frame.Opacity is not null)
        {
            sb.Append("opacity: ").Append(frame.Opacity).Append("; ");
        }
        if (!opacityOnly && frame.BoxShadow is not null)
        {
            sb.Append("box-shadow: ").Append(frame.BoxShadow).Append("; ");
        }
        if (!opacityOnly && frame.BackgroundPosition is not null)
        {
            sb.Append("background-position: ").Append(frame.BackgroundPosition).Append("; ");
        }
    }

    private static void AppendMicroClasses(StringBuilder sb)
    {
        sb.Append("/* Micro pack classes. Splat the matching Motion helper (Motion.Shake(),\n");
        sb.Append(" * Motion.Pulse(), Motion.Shimmer(), Motion.FocusGlow()) or add the class. */\n");
        foreach (var preset in MicroPresets.All)
        {
            sb.Append('.').Append(preset.Class).Append(" {\n");
            if (preset.BaseStyle is not null)
            {
                foreach (var decl in preset.BaseStyle)
                {
                    sb.Append("  ").Append(ToCssProperty(decl.Property)).Append(": ").Append(decl.Value).Append(";\n");
                }
            }
            sb.Append("  animation: ").Append(preset.KeyframesName).Append(' ')
                .Append(FormatMilliseconds(preset.DurationMs)).Append(' ').Append(preset.Easing);
            if (preset.Loop)
            {
                sb.Append(" infinite");
            }
            sb.Append(";\n}\n\n");
        }
    }

    private static void AppendReducedMotion(StringBuilder sb)
    {
        sb.Append("/* prefers-reduced-motion: collapse every transform animation to opacity only\n");
        sb.Append(" * (the state selectors are repeated so this later block wins the cascade). */\n");
        sb.Append("@media (prefers-reduced-motion: reduce) {\n");

        foreach (var preset in MotionPresets.All)
        {
            var cls = "." + preset.PresenceClass;
            sb.Append("  ").Append(cls).Append(",\n");
            sb.Append("  ").Append(cls).Append("[data-starting-style],\n");
            sb.Append("  ").Append(cls).Append("[data-closed],\n");
            sb.Append("  ").Append(cls).Append("[data-ending-style],\n");
            sb.Append("  .").Append(preset.EnterClass).Append(preset == MotionPresets.All[^1] ? " {\n" : ",\n");
        }
        sb.Append("    transition-property: opacity;\n");
        sb.Append("    transform: none;\n");
        sb.Append("  }\n");

        sb.Append("  @starting-style {\n");
        foreach (var preset in MotionPresets.All)
        {
            sb.Append("    .").Append(preset.EnterClass).Append(preset == MotionPresets.All[^1] ? " {\n" : ",\n");
        }
        sb.Append("      transform: none;\n");
        sb.Append("    }\n");
        sb.Append("  }\n");

        sb.Append("  .motion-press:active,\n");
        sb.Append("  .motion-hover:hover {\n");
        sb.Append("    transform: none;\n");
        sb.Append("  }\n");

        sb.Append("\n  /* Micro pack: transform-driven attention effects rest (shake,\n");
        sb.Append("   * focus-glow, shimmer); pulse keeps only its opacity beat. Shimmer's\n");
        sb.Append("   * documented fallback is a static placeholder: the sweep stops but the\n");
        sb.Append("   * gradient surface remains. */\n");
        foreach (var preset in MicroPresets.All)
        {
            sb.Append("  .").Append(preset.Class).Append(" {\n");
            sb.Append(preset.Reduce == MicroReduce.OpacityOnly
                ? "    animation-name: " + preset.ReducedKeyframesName + ";\n"
                : "    animation: none;\n");
            sb.Append("  }\n");
        }
        sb.Append("}\n");
    }

    private static void AppendState(StringBuilder sb, MotionVisualState state, string indent = "  ")
    {
        sb.Append(indent).Append("opacity: ")
            .Append(LinearEasingBaker.FormatPoint(state.Opacity)).Append(";\n");
        if (state.Transform is not null)
        {
            sb.Append(indent).Append("transform: ").Append(state.Transform).Append(";\n");
        }
    }

    private static string FormatOffset(double offset)
        => (offset * 100).ToString("0.####", CultureInfo.InvariantCulture) + "%";

    private static string FormatMilliseconds(double milliseconds)
        => milliseconds.ToString("0.####", CultureInfo.InvariantCulture) + "ms";

    /// <summary>camelCase (WAAPI/IDL) to kebab-case (CSS): backgroundPosition -> background-position.</summary>
    private static string ToCssProperty(string camelCase)
    {
        var sb = new StringBuilder(camelCase.Length + 4);
        foreach (var c in camelCase)
        {
            if (char.IsUpper(c))
            {
                sb.Append('-').Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static (string Duration, string Easing) VarsFor(Spring spring, Dictionary<string, BakedEasing> baked)
    {
        foreach (var (name, named) in NamedSprings)
        {
            if (named == spring)
            {
                return ($"var(--navius-spring-{name}-duration)", $"var(--navius-spring-{name})");
            }
        }
        // Unnamed spring: inline its baked values.
        var b = LinearEasingBaker.Bake(spring);
        return (b.DurationMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms", b.Easing);
    }
}
