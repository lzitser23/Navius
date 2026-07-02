namespace Navius.Primitives.Components.Combobox;

/// <summary>
/// Cascaded by <c>NaviusComboboxList</c> around each row so the (non-generic)
/// <c>NaviusComboboxItem</c> and its <c>NaviusComboboxItemIndicator</c> can read the boxed
/// value + virtual-focus/selection state without the Root's <c>TItem</c> leaking down.
/// Recreated per render; the cascade re-renders an item only when its record changes
/// (highlight/selection moved).
/// </summary>
public sealed record ComboboxItemContext(
    object Value,
    int Index,
    bool IsHighlighted,
    bool IsSelected,
    string OptionId);
