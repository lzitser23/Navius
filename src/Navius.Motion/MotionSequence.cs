using System.Globalization;
using Microsoft.AspNetCore.Components;
using Navius.Motion.Interop;

namespace Navius.Motion;

/// <summary>
/// The imperative plane: a fluent builder that composes a multi-element animation
/// timeline in C# and compiles it to a serialized program the JS executor plays. Springs
/// are baked to <c>linear()</c> easings here (the same solver as every other tier) and
/// every segment's <c>at</c> offset is resolved to an ABSOLUTE start time at
/// <see cref="Build"/> time, so the runtime never resolves offsets itself: it just plays
/// one WAAPI animation per segment with a computed delay. That keeps scheduling
/// deterministic and makes the at-offset arithmetic directly unit-testable.
/// <para>
/// Offsets and durations are in milliseconds (the interop boundary is milliseconds
/// everywhere). A segment's <c>at</c> is resolved against a running cursor (the end of
/// the previously added segment): <c>null</c> plays right after the previous segment;
/// <c>"+120"</c> starts 120ms after it; <c>"-80"</c> overlaps it by 80ms; <c>"&lt;"</c>
/// starts together with the previous segment; a bare number is an absolute time; a label
/// name (see <see cref="Label"/>) starts at that label's time.
/// </para>
/// <para>
/// NOT supported (own the limits honestly): per-frame / onUpdate callbacks, nested
/// sequences, and infinite iterations. One segment targets one element.
/// </para>
/// </summary>
public sealed class MotionSequence
{
    private readonly List<MotionSegment> _segments = new();
    private readonly Dictionary<string, double> _labels = new(StringComparer.Ordinal);
    private double _cursor;         // default start for the next segment (end of the previous)
    private double _previousStart;  // start of the previous segment (the "<" anchor)

    /// <summary>Reduced-motion policy passed to the runtime: user, always or never.</summary>
    public string ReduceMotion { get; set; } = "user";

    /// <summary>Add a spring-timed segment targeting a CSS selector (first match wins, scoped to the run root).</summary>
    public MotionSequence To(string selector, MotionFrame[] keyframes, Spring spring, string? at = null)
        => Add(new MotionTarget(Selector: selector), keyframes, Bake(spring), at);

    /// <summary>Add a duration-timed segment targeting a CSS selector.</summary>
    public MotionSequence To(string selector, MotionFrame[] keyframes, double durationMs, string easing = "linear", string? at = null)
        => Add(new MotionTarget(Selector: selector), keyframes, (durationMs, easing), at);

    /// <summary>Add a spring-timed segment targeting an element captured with @ref.</summary>
    public MotionSequence To(ElementReference element, MotionFrame[] keyframes, Spring spring, string? at = null)
        => Add(new MotionTarget(Ref: element), keyframes, Bake(spring), at);

    /// <summary>Add a duration-timed segment targeting an element captured with @ref.</summary>
    public MotionSequence To(ElementReference element, MotionFrame[] keyframes, double durationMs, string easing = "linear", string? at = null)
        => Add(new MotionTarget(Ref: element), keyframes, (durationMs, easing), at);

    /// <summary>Mark the current cursor (the end of the last segment) with a name a later <c>at</c> can target.</summary>
    public MotionSequence Label(string name)
    {
        _labels[name] = _cursor;
        return this;
    }

    /// <summary>Compile the timeline to a serializable program (absolute start times, baked easings).</summary>
    public MotionProgram Build()
    {
        var total = 0.0;
        foreach (var segment in _segments)
        {
            var end = segment.StartMs + segment.DurationMs;
            if (end > total)
            {
                total = end;
            }
        }
        return new MotionProgram(_segments.ToArray(), total, ReduceMotion);
    }

    private static (double DurationMs, string Easing) Bake(Spring spring)
    {
        var baked = LinearEasingBaker.Bake(spring);
        return (baked.DurationMilliseconds, baked.Easing);
    }

    private MotionSequence Add(
        MotionTarget target, MotionFrame[] keyframes, (double DurationMs, string Easing) timing, string? at)
    {
        var start = Math.Max(0, ResolveAt(at));
        var segment = new MotionSegment(target, keyframes, start, timing.DurationMs, timing.Easing);
        _segments.Add(segment);
        _previousStart = start;
        _cursor = start + timing.DurationMs;
        return this;
    }

    private double ResolveAt(string? at)
    {
        if (string.IsNullOrEmpty(at))
        {
            return _cursor;
        }
        if (at == "<")
        {
            return _previousStart;
        }
        if (at[0] == '+')
        {
            return _cursor + ParseOffset(at.AsSpan(1));
        }
        if (at[0] == '-')
        {
            return _cursor - ParseOffset(at.AsSpan(1));
        }
        if (_labels.TryGetValue(at, out var labelTime))
        {
            return labelTime;
        }
        return ParseOffset(at);
    }

    private static double ParseOffset(ReadOnlySpan<char> value)
        => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}

/// <summary>
/// A compiled sequence: the resolved segments (absolute start times, baked easings), the
/// total timeline length in milliseconds, and the reduced-motion policy. Serialized to
/// camelCase for <c>runProgram</c>.
/// </summary>
public sealed record MotionProgram(
    IReadOnlyList<MotionSegment> Segments,
    double TotalMs,
    string ReduceMotion = "user");

/// <summary>
/// One resolved timeline segment: the target, its WAAPI keyframes, the absolute start
/// (used as the animation's <c>delay</c>) and duration in milliseconds, the baked easing,
/// and the WAAPI <c>composite</c> mode.
/// </summary>
public sealed record MotionSegment(
    MotionTarget Target,
    MotionFrame[] Keyframes,
    double StartMs,
    double DurationMs,
    string Easing,
    string Composite = "replace");

/// <summary>
/// A segment target: exactly one of a CSS <see cref="Selector"/> (resolved to the first
/// match, scoped to the run root) or a <see cref="Ref"/> captured with @ref (a serialized
/// ElementReference the JS reviver revives to the DOM node).
/// </summary>
public sealed record MotionTarget(
    string? Selector = null,
    ElementReference? Ref = null);
