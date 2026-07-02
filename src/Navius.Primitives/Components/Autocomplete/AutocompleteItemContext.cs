namespace Navius.Primitives.Components.Autocomplete;

/// <summary>
/// Cascaded by <c>NaviusAutocompleteList</c> around each row so the (non-generic)
/// <c>NaviusAutocompleteItem</c> can read its boxed value + virtual-focus state without
/// the Root's <c>TItem</c> leaking down. Recreated per render; the cascade re-renders an
/// item only when its record changes (highlight/selection moved).
/// </summary>
public sealed record AutocompleteItemContext(
    object Value,
    int Index,
    bool IsHighlighted,
    bool IsSelected,
    string OptionId);
