using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Slider;

/// <summary>
/// Shared state for one slider, cascaded from <see cref="NaviusSlider"/> to its
/// parts (track, range, thumb). The root owns the authoritative value array and
/// pushes it here; parts read <see cref="Values"/> / per-thumb fractions for
/// layout and ARIA, and call back through the request delegate to move a thumb.
/// </summary>
public sealed class SliderContext
{
    private readonly Func<int, double, Task> _requestSetThumb;
    private int _thumbCount;

    public SliderContext(
        Func<int, double, Task> requestSetThumb,
        double min,
        double max,
        double step,
        double largeStep,
        int minStepsBetweenThumbs,
        string orientation,
        string dir,
        bool inverted,
        bool disabled)
    {
        _requestSetThumb = requestSetThumb;
        Min = min;
        Max = max;
        Step = step <= 0 ? 1 : step;
        LargeStep = largeStep;
        MinStepsBetweenThumbs = minStepsBetweenThumbs;
        Orientation = orientation;
        Dir = dir;
        Inverted = inverted;
        Disabled = disabled;
        Values = new double[] { min };
    }

    public double Min { get; private set; }

    public double Max { get; private set; }

    public double Step { get; private set; }

    /// <summary>Large step for PageUp/Down + Shift+Arrow.</summary>
    public double LargeStep { get; private set; }

    /// <summary>Minimum number of steps that must separate adjacent thumbs.</summary>
    public int MinStepsBetweenThumbs { get; private set; }

    /// <summary><c>"horizontal"</c> or <c>"vertical"</c>.</summary>
    public string Orientation { get; private set; }

    /// <summary><c>"ltr"</c> or <c>"rtl"</c>.</summary>
    public string Dir { get; private set; }

    /// <summary>When true, high values sit at the start of the track.</summary>
    public bool Inverted { get; private set; }

    public bool Disabled { get; private set; }

    /// <summary>One entry per thumb. Always sorted ascending by the root.</summary>
    public IReadOnlyList<double> Values { get; private set; }

    private bool IsVertical => Orientation == "vertical";

    private bool IsRtl => Dir == "rtl";

    /// <summary>Position of a value within [Min, Max] as 0..1 (before inversion).</summary>
    private double FractionOf(double value)
    {
        var span = Max - Min;
        if (span <= 0)
        {
            return 0;
        }

        return Math.Clamp((value - Min) / span, 0, 1);
    }

    /// <summary>
    /// Position of thumb <paramref name="index"/> as a 0..1 offset along the visual
    /// axis, honouring <see cref="Inverted"/> and (for horizontal) <see cref="Dir"/>.
    /// </summary>
    public double OffsetFraction(int index)
    {
        if (index < 0 || index >= Values.Count)
        {
            return 0;
        }

        var f = FractionOf(Values[index]);
        var flip = Inverted ^ (!IsVertical && IsRtl);
        return flip ? 1 - f : f;
    }

    /// <summary>CSS percentage for a thumb's offset, e.g. <c>"42%"</c>.</summary>
    public string OffsetPercent(int index) => $"{OffsetFraction(index) * 100:0.####}%";

    /// <summary>Visual start/end of the filled range as 0..1 offsets along the axis.</summary>
    public (double start, double end) RangeBounds()
    {
        if (Values.Count == 0)
        {
            return (0, 0);
        }

        double lowF;
        double highF;

        if (Values.Count == 1)
        {
            // Single thumb fills from the track origin to the thumb.
            lowF = 0;
            highF = FractionOf(Values[0]);
        }
        else
        {
            double min = Values[0];
            double max = Values[0];
            foreach (var v in Values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }

            lowF = FractionOf(min);
            highF = FractionOf(max);
        }

        var flip = Inverted ^ (!IsVertical && IsRtl);
        if (flip)
        {
            // Mirror the band: start measured from the same (visual start) edge.
            return (1 - highF, 1 - lowF);
        }

        return (lowF, highF);
    }

    /// <summary>Index of the thumb currently being dragged via pointer, or -1 when none.</summary>
    public int ActiveThumb { get; private set; } = -1;

    /// <summary>Set the dragging thumb (the spec <c>data-dragging</c>); notifies parts on change.</summary>
    internal async Task SetActiveThumbAsync(int index)
    {
        if (ActiveThumb == index)
        {
            return;
        }

        ActiveThumb = index;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public event Func<Task>? Changed;

    /// <summary>
    /// Registers a thumb in document order and returns its zero-based index. Parts
    /// call this once on init so each thumb knows which value it owns.
    /// </summary>
    public int RegisterThumb() => _thumbCount++;

    /// <summary>Re-sync the root's parameters into the context (called from OnParametersSet).</summary>
    internal void Configure(
        double min,
        double max,
        double step,
        double largeStep,
        int minStepsBetweenThumbs,
        string orientation,
        string dir,
        bool inverted,
        bool disabled)
    {
        Min = min;
        Max = max;
        Step = step <= 0 ? 1 : step;
        LargeStep = largeStep;
        MinStepsBetweenThumbs = minStepsBetweenThumbs;
        Orientation = orientation;
        Dir = dir;
        Inverted = inverted;
        Disabled = disabled;
    }

    /// <summary>Replace the authoritative values and re-render parts if they changed.</summary>
    internal async Task SetValuesInternalAsync(IReadOnlyList<double> values)
    {
        if (SameValues(Values, values))
        {
            return;
        }

        Values = values;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    private static bool SameValues(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Thumb entry point: ask the root to move thumb <paramref name="index"/> to an absolute value.</summary>
    public Task RequestSetThumbAsync(int index, double value) => _requestSetThumb(index, value);

    /// <summary>
    /// Maps a visual 0..1 fraction (from the engine drag tracker, measured from the
    /// axis start) back to an un-inverted value fraction, honouring inversion + RTL.
    /// </summary>
    public double VisualFractionToValueFraction(double visual)
    {
        var flip = Inverted ^ (!IsVertical && IsRtl);
        return flip ? 1 - visual : visual;
    }
}
