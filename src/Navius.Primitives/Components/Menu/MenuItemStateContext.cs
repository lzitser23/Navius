namespace Navius.Primitives.Components.Menu;

/// <summary>
/// Cascaded from a <c>NaviusMenuCheckboxItem</c> or
/// <c>NaviusMenuRadioItem</c> to its <c>NaviusMenuItemIndicator</c>.
/// The indicator reads <see cref="Checked"/> to decide whether to mount (the spec only
/// renders the indicator when checked, or always when ForceMount) and which discrete
/// <c>data-checked</c>/<c>data-unchecked</c>/<c>data-indeterminate</c> attr to emit.
/// </summary>
public sealed class MenuItemStateContext
{
    /// <summary>true=checked, false=unchecked, null=indeterminate (checkbox only).</summary>
    public bool? Checked { get; internal set; }

    /// <summary>the spec mounts the indicator when checked or indeterminate.</summary>
    public bool IsPresent => Checked != false;

    /// <summary>Raised by the owning item when the checked state changes.</summary>
    public event Action? Changed;

    internal void NotifyChanged() => Changed?.Invoke();
}
