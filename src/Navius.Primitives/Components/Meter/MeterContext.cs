namespace Navius.Primitives.Components.Meter;

/// <summary>
/// Shared, derived state for a meter group. The root resolves value/min/max from its
/// parameters and cascades the context so parts read the same min-aware fraction. A
/// meter is always determinate (Base UI Meter has no indeterminate state and no data-*).
/// </summary>
public sealed class MeterContext
{
    public double Value { get; internal set; }
    public double Min { get; internal set; }
    public double Max { get; internal set; } = 100;

    /// <summary>0..1 completion fraction, min-aware: (value - min) / (max - min).</summary>
    public double Fraction => Max <= Min ? 0 : Math.Clamp((Value - Min) / (Max - Min), 0, 1);

    /// <summary>Percentage 0..100.</summary>
    public double Percentage => Fraction * 100;

    /// <summary>Stable id wiring the Root's <c>aria-labelledby</c> to a Label part, when present.</summary>
    public string LabelId { get; } = $"navius-meter-label-{Guid.NewGuid():N}";

    public bool HasLabel { get; private set; }

    public event Action? Changed;

    internal void SetHasLabel()
    {
        if (HasLabel) return;
        HasLabel = true;
        Changed?.Invoke();
    }
}
