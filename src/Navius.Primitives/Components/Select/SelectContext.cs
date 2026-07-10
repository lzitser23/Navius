using System.Linq;
using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Select;

/// <summary>
/// Shared state for a select. Open and Value are routed back to the root, which
/// decides controlled vs. uncontrolled. Options report their selected state
/// against <see cref="Value"/> and register their display text so the
/// <c>NaviusSelectValue</c> can show the label (not the raw value key).
///
/// Implements <see cref="IAnchoredOverlayContext"/> so the listbox reuses the shared
/// overlay machinery (<see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>):
/// the Trigger is the positioning anchor, the Positioner part publishes placement
/// options, and the Popup engages the engine + dismissable layer (adding roving focus on
/// top, since a listbox roves rather than focus-traps). The select is <b>non-modal</b>
/// (<see cref="Modal"/> is false) so the page keeps scrolling while it is open.
/// </summary>
public sealed class SelectContext : IAnchoredOverlayContext
{
    private readonly Func<bool, Task> _setOpen;
    private readonly Func<string, Task> _selectValue;

    // value -> rendered display text (label), populated by NaviusSelectItem /
    // NaviusSelectItemText so the trigger can show the human label for the key.
    //
    // This is a PERSISTENT cache: entries are overwritten on (re)registration but are
    // deliberately NOT dropped when an item unmounts (e.g. the popup closes and its
    // options dispose). Otherwise the closed trigger would lose the selected item's label
    // and fall back to rendering the raw value key (e.g. "customer_42").
    private readonly Dictionary<string, string?> _textByValue = new();

    public SelectContext(Func<bool, Task> setOpen, Func<string, Task> selectValue)
    {
        _setOpen = setOpen;
        _selectValue = selectValue;
    }

    public bool Open { get; private set; }
    public string? Value { get; private set; }

    /// <summary>
    /// True for a multi-select listbox: clicking an option TOGGLES it in the set and keeps
    /// the popup open (the root owns close-vs-stay-open). Selection then lives in
    /// <see cref="SelectedValues"/> instead of the single <see cref="Value"/>.
    /// </summary>
    public bool Multiple { get; set; }

    private IReadOnlyList<string> _selectedValues = Array.Empty<string>();

    /// <summary>The committed multi-select set (opaque string keys). Empty in single mode.</summary>
    public IReadOnlyList<string> SelectedValues => _selectedValues;

    /// <summary>Whether anything is selected — the set in multi mode, the single value otherwise.</summary>
    public bool HasSelection => Multiple ? _selectedValues.Count > 0 : Value is not null;

    public string? Placeholder { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public string? Name { get; set; }
    public string? Dir { get; set; }

    /// <summary>
    /// Initial-focus mode passed to roving focus when the listbox opens:
    /// "selected" (default — land on the selected option, else first),
    /// "first" (Enter/ArrowDown on closed trigger), or "last" (ArrowUp).
    /// </summary>
    public string OpenFocusMode { get; private set; } = "selected";

    public string ContentId { get; } = $"navius-select-{Guid.NewGuid():N}";

    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>A select has no separate anchor part: the popup anchors to the trigger.</summary>
    public ElementReference PositionReference => TriggerElement;

    /// <summary>
    /// Root modality. The spec Select is <b>non-modal</b>: the page keeps scrolling and the
    /// listbox does not trap focus. Drives the base's <c>LockPageScroll</c> (false here).
    /// </summary>
    public bool Modal { get; set; }

    /// <summary>Custom portal mount-container selector (the spec <c>Portal.container</c>); null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup) mounted while closed (the spec <c>Portal.keepMounted</c>) for exit animations.</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by <c>NaviusSelectPositioner</c>.</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    /// <summary>Published by the Positioner part so the Popup can engage the engine + style the positioning div.</summary>
    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    // Optional arrow element, registered by NaviusSelectArrow and consumed by the
    // Popup's positioner so the arrow points at the trigger.
    public ElementReference ArrowElement { get; private set; }
    public bool HasArrow { get; private set; }

    /// <summary>Raised when an arrow registers/unregisters so the popup can (re)wire positioning.</summary>
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

    internal async Task SetOpenInternalAsync(bool open)
    {
        if (Open == open) return;
        Open = open;
        if (Changed is not null) await Changed.Invoke();
    }

    internal async Task SetValueInternalAsync(string? value)
    {
        if (Value == value) return;
        Value = value;
        if (Changed is not null) await Changed.Invoke();
    }

    /// <summary>
    /// Multi-select parallel to <see cref="SetValueInternalAsync"/>. Compared by SEQUENCE (not
    /// reference) so a controlled <c>@bind-Values</c> re-render pushing an equal-but-new list
    /// does not re-fire <see cref="Changed"/> or fight OnParametersSetAsync.
    /// </summary>
    internal async Task SetSelectedValuesInternalAsync(IReadOnlyList<string> values)
    {
        if (_selectedValues.SequenceEqual(values)) return;
        _selectedValues = values;
        if (Changed is not null) await Changed.Invoke();
    }

    public Task RequestSetOpenAsync(bool open)
    {
        if (Disabled) return Task.CompletedTask;
        OpenFocusMode = "selected";
        return _setOpen(open);
    }

    public Task RequestToggleAsync()
    {
        if (Disabled) return Task.CompletedTask;
        OpenFocusMode = "selected";
        return _setOpen(!Open);
    }

    public Task RequestCloseAsync() => _setOpen(false);

    /// <summary>Open with an explicit initial-focus mode (first | last | selected).</summary>
    public Task RequestOpenWithFocusAsync(string mode)
    {
        if (Disabled) return Task.CompletedTask;
        OpenFocusMode = mode;
        return _setOpen(true);
    }

    public Task SelectValueAsync(string value)
    {
        if (Disabled) return Task.CompletedTask;
        return _selectValue(value);
    }

    public bool IsSelected(string value) =>
        Multiple ? _selectedValues.Contains(value) : Value == value;

    // --- Value -> text registry (so the trigger shows the label, not the key) ---

    public void RegisterText(string value, string? text) => _textByValue[value] = text;

    public void UnregisterText(string value)
    {
        // Intentionally a no-op: the label must SURVIVE the item's unmount so that a
        // closed select (whose options have disposed) still renders the selected label
        // instead of the raw value key. Re-registration overwrites the entry.
    }

    /// <summary>The display label for the current value, or null if none registered.</summary>
    public string? SelectedText =>
        Value is not null && _textByValue.TryGetValue(Value, out var t) ? t : null;

    /// <summary>
    /// The display labels for the multi-select set (falling back to the raw key when an item
    /// registered no label). Drives the joined-label summary in <c>NaviusSelectValue</c>.
    /// </summary>
    public IReadOnlyList<string> SelectedTexts =>
        _selectedValues
            .Select(v => _textByValue.TryGetValue(v, out var t) && t is not null ? t : v)
            .ToList();
}
