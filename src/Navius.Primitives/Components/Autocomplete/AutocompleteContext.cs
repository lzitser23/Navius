using System.Linq;
using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Autocomplete;

/// <summary>
/// One filtered row surfaced to the (non-generic) List/Item parts. <see cref="Value"/>
/// is the boxed <c>TItem</c> the generic Root stored; <see cref="Text"/> is
/// <c>ItemToString(item)</c>; <see cref="Index"/> is the row's position in the filtered
/// list (drives the option id + the highlighted index).
/// </summary>
public sealed record AutocompleteItemData(object Value, string Text, int Index);

/// <summary>
/// Shared state for one autocomplete (the Base UI Autocomplete: a free-text input that
/// filters a list; the value is the typed string). Implements
/// <see cref="IAnchoredOverlayContext"/> so the listbox reuses the shared overlay
/// machinery (<see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>): the
/// <b>input</b> is the positioning anchor + trigger, the Positioner part publishes
/// placement options, and the Popup engages the engine + dismissable layer.
///
/// Unlike Menu/Select the autocomplete uses <b>virtual focus</b>: focus stays in the
/// input, the highlighted row is advertised via <c>aria-activedescendant</c>, and there
/// is NO roving (the Popup runs with <c>TrapFocus=false</c> / <c>MoveFocusInside=false</c>).
/// It is <b>non-modal</b> (<see cref="Modal"/> false) so the page stays interactive.
///
/// The query/filter/selection logic lives in the generic Root; this context holds the
/// read surface the parts render from and routes actions back to the Root via the ctor
/// delegates (mirroring <c>MenuContext</c>/<c>SelectContext</c>). Highlight movement is
/// resolved here (it only needs the filtered count) rather than round-tripping the Root.
/// </summary>
public sealed class AutocompleteContext : IAnchoredOverlayContext
{
    private readonly Func<string, Task> _setQuery;
    private readonly Func<object, Task> _select;
    private readonly Func<bool, Task> _requestSetOpen;

    public AutocompleteContext(
        Func<string, Task> setQuery, Func<object, Task> select, Func<bool, Task> requestSetOpen)
    {
        _setQuery = setQuery;
        _select = select;
        _requestSetOpen = requestSetOpen;
    }

    public bool Open { get; private set; }

    /// <summary>The autocomplete is non-modal: the page keeps scrolling and focus stays in the input.</summary>
    public bool Modal => false;

    /// <summary>Id of the <b>List</b> (the listbox) — the input's <c>aria-controls</c> points here.</summary>
    public string ContentId { get; } = $"navius-autocomplete-{Guid.NewGuid():N}";

    /// <summary>The input element (registered by <c>NaviusAutocompleteInput</c>): trigger + anchor.</summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>The optional dropdown <c>Trigger</c> button (registered by <c>NaviusAutocompleteTrigger</c>).
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

    /// <summary>The live input text.</summary>
    public string Query { get; private set; } = "";

    /// <summary>Index into <see cref="Items"/> of the virtually-focused row, or -1 for none.</summary>
    public int HighlightedIndex { get; private set; } = -1;

    /// <summary>The current filtered rows.</summary>
    public IReadOnlyList<AutocompleteItemData> Items { get; private set; } = Array.Empty<AutocompleteItemData>();

    public int ItemCount => Items.Count;

    public bool IsEmpty => ItemCount == 0;

    /// <summary>Boxed selected value(s). A single-value autocomplete holds at most one.</summary>
    public IReadOnlyCollection<object> SelectedValues { get; private set; } = Array.Empty<object>();

    public string OptionId(int i) => $"{ContentId}-opt-{i}";

    /// <summary>The active option id for <c>aria-activedescendant</c>; null when closed or nothing highlighted.</summary>
    public string? ActiveDescendantId =>
        Open && HighlightedIndex >= 0 && HighlightedIndex < ItemCount ? OptionId(HighlightedIndex) : null;

    /// <summary>
    /// Set by the Popup while open so the keyboard highlight path can follow the active option
    /// into view (virtual focus has no roving to do it). Given the active option id; cleared on close.
    /// </summary>
    internal Func<string, Task>? ScrollActiveIntoView { get; set; }

    private Task FollowActiveAsync() =>
        ScrollActiveIntoView is not null && HighlightedIndex >= 0
            ? ScrollActiveIntoView(OptionId(HighlightedIndex))
            : Task.CompletedTask;

    public bool IsSelected(object value) => SelectedValues.Contains(value);

    /// <summary>Per-row render callback provided by the generic Root (invokes the typed template or the text fallback).</summary>
    public Func<AutocompleteItemData, RenderFragment>? RenderItem { get; private set; }

    internal void SetRenderItem(Func<AutocompleteItemData, RenderFragment> render) => RenderItem = render;

    // --- field setters used by the Root to batch state before one RaiseChangedAsync ---

    internal void SetOpen(bool value) => Open = value;
    internal void SetQueryValue(string value) => Query = value;
    internal void SetHighlightedIndex(int value) => HighlightedIndex = value;
    internal void SetItems(IReadOnlyList<AutocompleteItemData> value) => Items = value;
    internal void SetSelectedValues(IReadOnlyCollection<object> value) => SelectedValues = value;

    // --- actions the parts invoke ---

    /// <summary>Set the query (typing) — the Root filters, opens, and resets the highlight.</summary>
    public Task SetQueryAsync(string query) => _setQuery(query);

    /// <summary>Select a value — the Root commits it as the text, closes, and fires <c>ValueChanged</c>.</summary>
    public Task SelectAsync(object value) => _select(value);

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
        await FollowActiveAsync();
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
        await FollowActiveAsync();
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
