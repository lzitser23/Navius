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
}
