namespace Navius.Primitives.Components.ContextMenu;

/// <summary>
/// Cascaded by a CheckboxItem / RadioItem to its <see cref="NaviusContextMenuItemIndicator"/>
/// child so the indicator can render only when the parent is checked (or indeterminate).
/// Mirrors the spec, where ItemIndicator is shown only for a checked/indeterminate parent.
/// </summary>
public sealed class ContextMenuCheckableContext
{
    /// <summary>True = checked, false = unchecked, null = indeterminate (checkbox only).</summary>
    public bool? Checked { get; private set; }

    /// <summary>True when checked or indeterminate — the indicator should be visible.</summary>
    public bool IsShown => Checked is null or true;

    public event Action? Changed;

    public void SetChecked(bool? value)
    {
        if (Checked == value)
        {
            return;
        }

        Checked = value;
        Changed?.Invoke();
    }
}
