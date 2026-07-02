using System.Linq;
using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Combobox;

/// <summary>
/// One filtered row surfaced to the (non-generic) List/Item parts. <see cref="Value"/>
/// is the boxed <c>TItem</c> the generic Root stored; <see cref="Text"/> is
/// <c>ItemToString(item)</c>; <see cref="Index"/> is the row's position in the filtered
/// list (drives the option id + the highlighted index).
/// </summary>
public sealed record ComboboxItemData(object Value, string Text, int Index);

/// <summary>
/// Shared state for one combobox (the Base UI Combobox: value-selection — the input is a
/// filter/search box while the committed VALUE is the selected item(s), tracked SEPARATELY
/// from the filter text). Sibling of <c>AutocompleteContext</c>; it reuses the same
/// virtual-focus + overlay machinery (<see cref="IAnchoredOverlayContext"/>,
/// <see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>): the <b>input</b>
/// is the positioning anchor + trigger, the Positioner publishes placement options, and the
/// Popup engages the engine + dismissable layer with <c>TrapFocus=false</c> /
/// <c>MoveFocusInside=false</c> (focus never leaves the input).
///
/// The delta over Autocomplete is the value surface: <see cref="SelectedValues"/> (boxed)
/// drives the Item <c>data-selected</c>, the Value part and the Chips, while
/// <see cref="Query"/> is the throw-away filter text. Single-select commits one value +
/// closes; multi-select toggles a value, clears the query and keeps the popup open (chips).
/// The value/selection logic lives in the generic Root; this context holds the read surface
/// and routes actions back via the ctor delegates (mirroring <c>AutocompleteContext</c>).
/// </summary>
public sealed class ComboboxContext : IAnchoredOverlayContext
{
    private readonly Func<string, Task> _setQuery;
    private readonly Func<object, Task> _select;
    private readonly Func<object, Task> _removeValue;
    private readonly Func<Task> _removeLast;
    private readonly Func<Task> _clear;
    private readonly Func<bool, Task> _requestSetOpen;
    private Func<object, object, bool> _equals = ReferenceEquals;

    public ComboboxContext(
        Func<string, Task> setQuery,
        Func<object, Task> select,
        Func<object, Task> removeValue,
        Func<Task> removeLast,
        Func<Task> clear,
        Func<bool, Task> requestSetOpen)
    {
        _setQuery = setQuery;
        _select = select;
        _removeValue = removeValue;
        _removeLast = removeLast;
        _clear = clear;
        _requestSetOpen = requestSetOpen;
    }

    public bool Open { get; private set; }

    /// <summary>The combobox is non-modal: the page keeps scrolling and focus stays in the input.</summary>
    public bool Modal => false;

    /// <summary>True for a multi-select combobox (chips); toggling a value keeps the popup open.</summary>
    public bool Multiple { get; set; }

    public bool Disabled { get; set; }
    public bool ReadOnly { get; set; }
    public string? Placeholder { get; set; }

    /// <summary>Id of the <b>List</b> (the listbox) — the input's <c>aria-controls</c> points here.</summary>
    public string ContentId { get; } = $"navius-combobox-{Guid.NewGuid():N}";

    /// <summary>The input element (registered by <c>NaviusComboboxInput</c>): trigger + anchor.</summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>The optional dropdown <c>Trigger</c> button (registered by <c>NaviusComboboxTrigger</c>).
    /// The dismiss layer treats it as "inside" so its click toggles the popup rather than racing an outside-dismiss.</summary>
    public ElementReference TriggerButtonElement { get; set; }
    public bool HasTriggerButton { get; set; }

    /// <summary>The popup anchors to the input.</summary>
    public ElementReference PositionReference => TriggerElement;

    public string Dir { get; set; } = "ltr";
    public bool IsRtl => Dir == "rtl";

    public string? PortalContainer { get; set; }
    public bool PortalKeepMounted { get; set; }

    public PositionOptions Options { get; private set; } = new(SideOffset: 4);
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    public ElementReference ArrowElement { get; private set; }
    public bool HasArrow { get; private set; }

    public event Func<Task>? ArrowChanged;

    public async Task RegisterArrowAsync(ElementReference arrow)
    {
        ArrowElement = arrow;
        HasArrow = true;
        if (ArrowChanged is not null)
        {
            await ArrowChanged.Invoke();
        }
    }

    public async Task UnregisterArrowAsync()
    {
        HasArrow = false;
        ArrowElement = default;
        if (ArrowChanged is not null)
        {
            await ArrowChanged.Invoke();
        }
    }

    public event Func<Task>? Changed;

    /// <summary>Fire <see cref="Changed"/> so every subscribed part + the overlay presence re-render.</summary>
    internal async Task RaiseChangedAsync()
    {
        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    // --- virtual-focus read surface (driven by the Root; the Input renders from it) ---

    /// <summary>The live filter text (NOT the committed value).</summary>
    public string Query { get; private set; } = "";

    /// <summary>Index into <see cref="Items"/> of the virtually-focused row, or -1 for none.</summary>
    public int HighlightedIndex { get; private set; } = -1;

    /// <summary>The current filtered rows.</summary>
    public IReadOnlyList<ComboboxItemData> Items { get; private set; } = Array.Empty<ComboboxItemData>();

    public int ItemCount => Items.Count;

    public bool IsEmpty => ItemCount == 0;

    /// <summary>Boxed committed value(s): one for single-select, N for multi-select (the chips).</summary>
    public IReadOnlyList<object> SelectedValues { get; private set; } = Array.Empty<object>();

    /// <summary>Display text of the committed selection (single label, or comma-joined for multi).</summary>
    public string SelectedLabel { get; private set; } = "";

    public bool HasSelection => SelectedValues.Count > 0;

    public string OptionId(int i) => $"{ContentId}-opt-{i}";

    /// <summary>The active option id for <c>aria-activedescendant</c>; null when closed or nothing highlighted.</summary>
    public string? ActiveDescendantId =>
        Open && HighlightedIndex >= 0 && HighlightedIndex < ItemCount ? OptionId(HighlightedIndex) : null;

    /// <summary>Selected detection via the Root-provided typed equality (value-type / record aware).</summary>
    public bool IsSelected(object value) => SelectedValues.Any(v => _equals(v, value));

    /// <summary>Per-row render callback provided by the generic Root (the ItemTemplate or a text fallback).</summary>
    public Func<ComboboxItemData, RenderFragment>? RenderItem { get; private set; }

    /// <summary>Per-chip render callback provided by the generic Root (the ChipTemplate or a default chip).</summary>
    public Func<object, RenderFragment>? RenderChip { get; private set; }

    internal void SetRenderItem(Func<ComboboxItemData, RenderFragment> render) => RenderItem = render;
    internal void SetRenderChip(Func<object, RenderFragment> render) => RenderChip = render;
    internal void SetEquals(Func<object, object, bool> equals) => _equals = equals;

    // --- field setters used by the Root to batch state before one RaiseChangedAsync ---

    internal void SetOpen(bool value) => Open = value;
    internal void SetQueryValue(string value) => Query = value;
    internal void SetHighlightedIndex(int value) => HighlightedIndex = value;
    internal void SetItems(IReadOnlyList<ComboboxItemData> value) => Items = value;
    internal void SetSelectedValues(IReadOnlyList<object> value) => SelectedValues = value;
    internal void SetSelectedLabel(string value) => SelectedLabel = value;

    // --- actions the parts invoke ---

    /// <summary>Set the filter (typing) — the Root filters, opens and resets the highlight. Does NOT commit a value.</summary>
    public Task SetQueryAsync(string query) => _setQuery(query);

    /// <summary>Select a value: single → commit + close + show the label; multi → toggle + clear query + keep open.</summary>
    public Task SelectAsync(object value) => _select(value);

    /// <summary>Remove a value from the selection (chips + ChipRemove + Backspace-on-empty).</summary>
    public Task RemoveValueAsync(object value) => _removeValue(value);

    /// <summary>Remove the last selected value (Backspace on an empty multi-select input).</summary>
    public Task RemoveLastValueAsync() => _removeLast();

    /// <summary>Clear the whole selection AND the query (the Clear button).</summary>
    public Task ClearAsync() => _clear();

    public Task RequestSetAsync(bool open) => _requestSetOpen(open);

    public Task RequestCloseAsync() => _requestSetOpen(false);

    /// <summary>Move the highlight by <paramref name="delta"/>, clamping (or wrapping when <paramref name="loop"/>).</summary>
    public async Task MoveHighlightAsync(int delta, bool loop)
    {
        if (ItemCount == 0)
        {
            return;
        }

        var next = HighlightedIndex < 0
            ? (delta > 0 ? 0 : ItemCount - 1)
            : HighlightedIndex + delta;

        next = loop
            ? ((next % ItemCount) + ItemCount) % ItemCount
            : Math.Clamp(next, 0, ItemCount - 1);

        if (next == HighlightedIndex)
        {
            return;
        }

        HighlightedIndex = next;
        await RaiseChangedAsync();
    }

    /// <summary>Highlight a specific row (pointer hover).</summary>
    public async Task SetHighlightAsync(int index)
    {
        if (index < 0 || index >= ItemCount || index == HighlightedIndex)
        {
            return;
        }

        HighlightedIndex = index;
        await RaiseChangedAsync();
    }

    /// <summary>Highlight the first (<paramref name="last"/> false) or last row (Home/End/PageUp/PageDown).</summary>
    public async Task HighlightEdgeAsync(bool last)
    {
        if (ItemCount == 0)
        {
            return;
        }

        var index = last ? ItemCount - 1 : 0;
        if (index == HighlightedIndex)
        {
            return;
        }

        HighlightedIndex = index;
        await RaiseChangedAsync();
    }

    /// <summary>Select the highlighted row, if any (Enter).</summary>
    public Task CommitHighlightedAsync()
    {
        if (HighlightedIndex < 0 || HighlightedIndex >= ItemCount)
        {
            return Task.CompletedTask;
        }

        return _select(Items[HighlightedIndex].Value);
    }
}
