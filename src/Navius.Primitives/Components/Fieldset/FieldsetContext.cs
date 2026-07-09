namespace Navius.Primitives.Components.Fieldset;

/// <summary>
/// Cascaded from <see cref="NaviusFieldset"/> to its legend and to the
/// <c>NaviusField</c>s inside it, so they reflect the fieldset's disabled state.
/// A new instance is created whenever <see cref="Disabled"/> changes so cascaded
/// consumers re-render.
/// </summary>
public sealed class FieldsetContext
{
    /// <summary>Whether the fieldset (and everything inside it) is disabled.</summary>
    public bool Disabled { get; init; }

    /// <summary>Stable id the legend renders and the root points <c>aria-labelledby</c> at.</summary>
    public string LegendId { get; init; } = string.Empty;

    // Legend presence is ref-counted on the root so it survives context recreation on a
    // Disabled change; these forward the legend's register/unregister to the root.
    internal Action? RegisterLegendImpl { get; init; }
    internal Action? UnregisterLegendImpl { get; init; }

    /// <summary>Registers a mounted legend so the root emits <c>aria-labelledby</c>.</summary>
    public void RegisterLegend() => RegisterLegendImpl?.Invoke();

    /// <summary>Unregisters the legend on dispose.</summary>
    public void UnregisterLegend() => UnregisterLegendImpl?.Invoke();
}
