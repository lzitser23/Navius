namespace Navius.Primitives.Components.Progress;

/// <summary>
/// Shared, derived state for a progress group. The root computes value/max from its
/// parameters and cascades the context so every part reads the same resolved state and
/// the Base UI discrete data attributes (data-complete / data-indeterminate /
/// data-progressing).
/// </summary>
public sealed class ProgressContext
{
    /// <summary>Current value, or <c>null</c> when indeterminate.</summary>
    public double? Value { get; internal set; }

    /// <summary>Maximum value (the spec default 100).</summary>
    public double Max { get; internal set; } = 100;

    /// <summary>True when no value is provided (indeterminate / loading animation).</summary>
    public bool IsIndeterminate => Value is null;

    // Base UI discrete state attrs: present ("") when active, else null (omitted).
    public string? DataComplete => !IsIndeterminate && Value >= Max ? "" : null;
    public string? DataIndeterminate => IsIndeterminate ? "" : null;
    public string? DataProgressing => !IsIndeterminate && Value < Max ? "" : null;

    // Value attrs (not boolean-presence): data-value carries the current value and is
    // omitted while indeterminate (consistent with aria-valuenow); data-max is always present.
    public string? DataValue =>
        IsIndeterminate ? null : Value!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string DataMax => Max.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>0..1 fraction of completion (0 when indeterminate).</summary>
    public double Fraction =>
        IsIndeterminate || Max <= 0 ? 0 : Math.Clamp(Value!.Value / Max, 0, 1);

    /// <summary>Percentage 0..100, useful for transform math in the indicator.</summary>
    public double Percentage => Fraction * 100;

    /// <summary>Stable id wiring the Root's <c>aria-labelledby</c> to a Label part, when present.</summary>
    public string LabelId { get; } = $"navius-progress-label-{Guid.NewGuid():N}";

    /// <summary>True once a <c>NaviusProgressLabel</c> has registered.</summary>
    public bool HasLabel { get; private set; }

    public event Action? Changed;

    internal void SetHasLabel()
    {
        if (HasLabel) return;
        HasLabel = true;
        Changed?.Invoke();
    }
}
