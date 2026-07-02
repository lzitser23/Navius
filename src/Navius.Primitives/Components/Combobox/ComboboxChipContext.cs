namespace Navius.Primitives.Components.Combobox;

/// <summary>
/// Cascaded from a <c>NaviusComboboxChip</c> to its <c>NaviusComboboxChipRemove</c> so the
/// remove button knows which boxed value it targets (<c>Context.RemoveValueAsync(value)</c>).
/// </summary>
public sealed record ComboboxChipContext(object Value);
