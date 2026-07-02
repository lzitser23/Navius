using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.NavigationMenu;

/// <summary>
/// Shared state for one navigation menu, cascaded from <see cref="NaviusNavigationMenu"/>
/// to its parts. The root owns the authoritative <see cref="Value"/> (the value of the
/// item whose content is currently open; <c>null</c> = nothing open) and pushes it here.
/// Triggers register their <see cref="ElementReference"/> so the popup can anchor its
/// positioning against the ACTIVE trigger (the moving anchor), and so the engine's roving
/// controller can move focus across the trigger row.
///
/// Implements <see cref="IAnchoredOverlayContext"/> so the shared overlay machinery
/// (<see cref="OverlayPopupBase"/>/<see cref="OverlayPresence"/>/<see cref="OverlayPositionerBase"/>)
/// drives the Popup/Positioner/Arrow, exactly like the Menu family — the navigation menu
/// exposes a single shared Popup that morphs/re-anchors between active items. Per-item
/// (standalone) popups wrap this context in a small per-item adapter (see
/// <see cref="NavigationMenuPopupContext"/>).
///
/// The same context type also backs <see cref="NaviusNavigationMenuSub"/> (a nested
/// submenu root); <see cref="IsRoot"/> distinguishes the two so parts can render the
/// correct element (<c>nav</c> vs <c>div</c>) and decide whether a viewport applies.
/// </summary>
public sealed class NavigationMenuContext : IAnchoredOverlayContext
{
    private readonly Func<string?, Task> _requestSetValue;

    public NavigationMenuContext(Func<string?, Task> requestSetValue, string orientation, bool isRoot = true)
    {
        _requestSetValue = requestSetValue;
        Orientation = orientation;
        IsRoot = isRoot;
    }

    /// <summary><c>"horizontal"</c> or <c>"vertical"</c>; drives the arrow-key axis and data-orientation.</summary>
    public string Orientation { get; }

    /// <summary>True for the top-level menu root, false for a nested <c>Sub</c>.</summary>
    public bool IsRoot { get; }

    /// <summary>The value of the currently open item, or <c>null</c> when nothing is open.</summary>
    public string? Value { get; private set; }

    /// <summary>Stable id base so trigger/content can wire aria-controls / id.</summary>
    public string IdBase { get; } = $"navius-navmenu-{Guid.NewGuid():N}";

    public event Func<Task>? Changed;

    public bool IsOpen(string value) => string.Equals(Value, value, StringComparison.Ordinal);

    public string TriggerId(string value) => $"{IdBase}-trigger-{value}";

    /// <summary>Per-item popup id (standalone mode, one popup per item), for aria wiring.</summary>
    public string PopupIdFor(string value) => $"{IdBase}-content-{value}";

    // ---- IOverlayContext (shared surface) -------------------------------------

    /// <summary>Shared-mode open: the single popup is open iff any item is active.</summary>
    public bool Open => Value is not null;

    /// <summary>The navigation menu is never modal (no scroll-lock / focus-trap).</summary>
    public bool Modal => false;

    /// <summary>The shared Popup's id (one resizing surface for the whole menu).</summary>
    public string ContentId => $"{IdBase}-popup";

    /// <summary>The ACTIVE trigger — the moving anchor the shared popup positions against.</summary>
    public ElementReference TriggerElement =>
        Value is not null && _triggers.TryGetValue(Value, out var e) ? e : default;

    public bool HasTrigger => Value is not null && _triggers.ContainsKey(Value);

    /// <summary>A navigation menu has no separate anchor part: the popup anchors to the active trigger.</summary>
    public ElementReference PositionReference => TriggerElement;

    public Task RequestCloseAsync() => _requestSetValue(null);

    // ---- IAnchoredOverlayContext (arrow + positioner) -------------------------

    /// <summary>Optional arrow element (Arrow part), registered by the arrow part.</summary>
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

    public async Task UnregisterArrowAsync(ElementReference arrow)
    {
        // Standalone mode shares ONE arrow slot across per-item popups. When items switch,
        // the incoming item's arrow registers BEFORE the outgoing item's arrow unmounts, so
        // a blind clear would drop the live arrow. Only the current owner may clear the slot.
        if (!HasArrow || !string.Equals(arrow.Id, ArrowElement.Id, StringComparison.Ordinal))
        {
            return;
        }

        HasArrow = false;
        ArrowElement = default;
        if (ArrowChanged is not null)
        {
            await ArrowChanged.Invoke();
        }
    }

    /// <summary>Placement options collected by <see cref="NaviusNavigationMenuPositioner"/>.</summary>
    public PositionOptions Options { get; private set; } = new(Side: "bottom", Align: "center");

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>Custom portal mount-container selector; null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup) mounted while closed for exit animations.</summary>
    public bool PortalKeepMounted { get; set; }

    // ---- Viewport coordination -------------------------------------------------

    /// <summary>
    /// True when the menu uses the shared-viewport layout: a single root-level
    /// <c>Portal &gt; Positioner &gt; Popup &gt; Viewport</c> where every open <c>Content</c>
    /// teleports its panel into the one resizing viewport (rather than each item owning a
    /// standalone popup). Set from the root-level Popup (always instantiated, so this is
    /// stable from page load, independent of the Viewport's open/close mount lifecycle).
    /// </summary>
    public bool HasViewport => _sharedPopupRegistered || _viewportRegistered;

    private bool _sharedPopupRegistered;
    private bool _viewportRegistered;

    /// <summary>CSS selector targeting the viewport's content slot for Content teleport.</summary>
    public string ViewportSlotSelector => $"[data-navius-navigationmenu-viewport-slot='{IdBase}']";

    private Func<Task>? _viewportChanged;

    /// <summary>The root-level (Item-less) Popup registers shared-viewport mode on init.</summary>
    public void RegisterSharedPopup() => _sharedPopupRegistered = true;

    public void UnregisterSharedPopup() => _sharedPopupRegistered = false;

    /// <summary>The Viewport part registers here so it can re-render when the open value changes.</summary>
    public void RegisterViewport(Func<Task> onChanged)
    {
        _viewportRegistered = true;
        _viewportChanged = onChanged;
    }

    public void UnregisterViewport()
    {
        _viewportRegistered = false;
        _viewportChanged = null;
    }

    // ---- Trigger registry ------------------------------------------------------

    /// <summary>Each item registers the trigger element so content can anchor against it.</summary>
    private readonly Dictionary<string, ElementReference> _triggers = new(StringComparer.Ordinal);

    /// <summary>Document order of registered triggers, used to pick the single roving Tab seat.</summary>
    private readonly List<string> _triggerOrder = new();

    public void RegisterTrigger(string value, ElementReference element)
    {
        _triggers[value] = element;
        if (!_triggerOrder.Contains(value, StringComparer.Ordinal))
        {
            _triggerOrder.Add(value);
        }
    }

    public void UnregisterTrigger(string value)
    {
        _triggers.Remove(value);
        _triggerOrder.RemoveAll(v => string.Equals(v, value, StringComparison.Ordinal));
    }

    public bool TryGetTrigger(string value, out ElementReference element) =>
        _triggers.TryGetValue(value, out element);

    /// <summary>Index of <paramref name="value"/> in document order, or -1 if unknown. Drives activation direction.</summary>
    public int TriggerIndex(string value) =>
        _triggerOrder.FindIndex(v => string.Equals(v, value, StringComparison.Ordinal));

    /// <summary>The value that was open immediately before the current one (null = was closed).</summary>
    public string? PreviousValue { get; private set; }

    /// <summary>
    /// The direction the active item just moved, mapped to Base UI's
    /// <c>data-activation-direction</c> (<c>left|right|up|down</c>). Null when opening from
    /// a fully-closed state or when the index is unknown. Replaces the former
    /// <c>data-motion</c> token; both the Popup and the opening Content read it.
    /// </summary>
    public string? ActivationDirection
    {
        get
        {
            var cur = Value;
            var prev = PreviousValue;
            if (cur is null || prev is null)
            {
                return null;
            }

            var prevIdx = TriggerIndex(prev);
            var curIdx = TriggerIndex(cur);
            if (prevIdx < 0 || curIdx < 0 || prevIdx == curIdx)
            {
                return null;
            }

            if (Orientation == "horizontal")
            {
                return prevIdx < curIdx ? "right" : "left";
            }

            return prevIdx < curIdx ? "down" : "up";
        }
    }

    /// <summary>
    /// True if <paramref name="value"/> should hold the single seat in the Tab order:
    /// the open item's trigger if one is open, else the first registered trigger. The
    /// engine's roving controller takes over with arrow keys once focus is inside.
    /// </summary>
    public bool IsTabStop(string value)
    {
        if (Value is not null)
        {
            return string.Equals(Value, value, StringComparison.Ordinal);
        }

        return _triggerOrder.Count > 0 &&
               string.Equals(_triggerOrder[0], value, StringComparison.Ordinal);
    }

    /// <summary>Replace the authoritative open value and re-render parts if it changed.</summary>
    internal async Task SetValueInternalAsync(string? value)
    {
        if (string.Equals(Value, value, StringComparison.Ordinal))
        {
            return;
        }

        PreviousValue = Value;
        Value = value;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }

        if (_viewportChanged is not null)
        {
            await _viewportChanged.Invoke();
        }
    }

    // ---- OnOpenChangeComplete -------------------------------------------------
    // The Popup reports when its presence settles (open/close) so the root can raise
    // NaviusNavigationMenu.OnOpenChangeComplete.

    public event Func<bool, Task>? OpenChangeCompleted;

    internal Task NotifyOpenChangeCompletedAsync(bool open) =>
        OpenChangeCompleted?.Invoke(open) ?? Task.CompletedTask;

    // ---- Hover coordination ----------------------------------------------------
    // The owning root/sub registers the delegates that implement its open/close
    // timing so parts can route hover through the context regardless of nesting depth.

    private Func<string, Task>? _onTriggerPointerEnter;
    private Func<Task>? _onPointerLeave;
    private Action? _onPointerReenter;

    public void RegisterHover(Func<string, Task> onEnter, Func<Task> onLeave, Action onReenter)
    {
        _onTriggerPointerEnter = onEnter;
        _onPointerLeave = onLeave;
        _onPointerReenter = onReenter;
    }

    public Task PointerEnterTriggerAsync(string value) =>
        _onTriggerPointerEnter?.Invoke(value) ?? Task.CompletedTask;

    public Task PointerLeaveAsync() =>
        _onPointerLeave?.Invoke() ?? Task.CompletedTask;

    public void PointerReenter() => _onPointerReenter?.Invoke();

    // ---- Keyboard-open coordination -------------------------------------------
    // A keyboard activation on the trigger marks the value to open so the matching Popup
    // moves focus to its first focusable child on engage (APG); pointer/hover opens do not.

    private string? _keyboardOpenValue;

    /// <summary>
    /// Raised (with the value) AFTER <see cref="RequestKeyboardOpenAsync"/> has applied the
    /// open value, so a Popup that is already engaged for that value can move focus into its
    /// panel directly. Needed because a keyboard "enter content" on an already-open item does
    /// not toggle <see cref="Value"/> — no Changed, no re-engage — so the EngageAsync focus
    /// path never runs. The closed-&gt;open case is still handled by <see cref="ConsumeKeyboardOpen"/>.
    /// </summary>
    public event Func<string, Task>? FocusPanelRequested;

    /// <summary>Open <paramref name="value"/> and mark its panel to focus the first child on render.</summary>
    public async Task RequestKeyboardOpenAsync(string value)
    {
        _keyboardOpenValue = value;
        await _requestSetValue(value);

        // When the item was ALREADY open the set above was a no-op (no Changed / no engage),
        // so EngageAsync's ConsumeKeyboardOpen focus path never runs. Ask the live Popup to
        // move focus into the panel now.
        if (FocusPanelRequested is not null)
        {
            await FocusPanelRequested.Invoke(value);
        }
    }

    /// <summary>The Popup calls this on engage: true (once) when the open was keyboard-initiated for <paramref name="value"/>.</summary>
    public bool ConsumeKeyboardOpen(string? value)
    {
        if (value is not null && string.Equals(_keyboardOpenValue, value, StringComparison.Ordinal))
        {
            _keyboardOpenValue = null;
            return true;
        }

        return false;
    }

    /// <summary>Clear a pending keyboard-open marker (on close/disengage) so a later hover/focus open can't inherit it.</summary>
    public void ClearKeyboardOpen() => _keyboardOpenValue = null;

    /// <summary>Open the content for <paramref name="value"/>.</summary>
    public Task RequestOpenAsync(string value) => _requestSetValue(value);

    /// <summary>Toggle the content for <paramref name="value"/> (close if already open).</summary>
    public Task RequestToggleAsync(string value) =>
        _requestSetValue(IsOpen(value) ? null : value);
}

/// <summary>
/// Presence snapshot cascaded from a standalone (per-item) Popup down to its child
/// <see cref="NaviusNavigationMenuContent"/>, so the Content can mirror the Popup's
/// discrete presence attributes (<c>data-open</c>/<c>data-closed</c> +
/// <c>data-starting-style</c>/<c>data-ending-style</c>) without owning its own presence
/// machine. In shared-viewport mode there is no such cascade (the Content teleports into
/// the Viewport, outside the Popup's Blazor subtree) and the Content reads the context.
/// </summary>
public sealed record NavigationMenuPanelState(bool Open, bool Entering, bool Exiting);
