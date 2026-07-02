namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Cascaded by a <see cref="NaviusMenubarCheckboxItem"/> or
/// <see cref="NaviusMenubarRadioItem"/> so a nested <see cref="NaviusMenubarItemIndicator"/>
/// can read the parent's checked state and render its content only when checked
/// (unless force-mounted). <see cref="IsChecked"/> is re-evaluated on each render of
/// the owning item, so the indicator follows the item's state.
/// </summary>
public sealed class MenubarItemIndicatorContext
{
    private readonly Func<bool> _isChecked;

    public MenubarItemIndicatorContext(Func<bool> isChecked) => _isChecked = isChecked;

    /// <summary>True when the owning checkbox/radio item is currently checked.</summary>
    public bool IsChecked => _isChecked();
}
