using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Navius.Primitives.Common;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Overlays;

/// <summary>
/// The shared lifecycle for a Base UI <c>Popup</c>: presence (via
/// <see cref="OverlayPresence"/>) plus the dismissable layer, focus trap, scroll
/// lock and focus management. Modal popups trap focus + lock scroll; non-modal
/// popups leave the page interactive and the dismissable layer still closes on
/// Escape / outside pointer-down. The cancelable layer callbacks
/// (<c>OnEscapeKeyDown</c>, <c>OnPointerDownOutside</c>, …) are a Navius superset
/// over Base UI's single <c>onOpenChange(reason)</c> (ADR-0007 — a documented
/// deviation kept because Blazor cannot reproduce <c>event.preventBaseUIHandler</c>).
///
/// Dialog-family popups (Dialog/Alert Dialog/Drawer) inherit this directly;
/// anchored popups (Popover/Tooltip/Preview Card) inherit
/// <see cref="OverlayAnchoredPopupBase"/>.
/// </summary>
public abstract class OverlayPopupBase : OverlayPresence
{
    private DismissableLayer? _dismiss;
    private FocusTrap? _focusTrap;
    private DotNetObjectReference<OverlayPopupBase>? _selfRef;
    private bool _scrollLocked;

    /// <summary>Keep the popup mounted (hidden) while closed so an exit transition can run.</summary>
    [Parameter] public bool KeepMounted { get; set; }

    /// <summary>Cancelable; PreventDefault keeps focus where it is when the popup opens.</summary>
    [Parameter] public EventCallback<NaviusOpenAutoFocusEventArgs> OnOpenAutoFocus { get; set; }

    /// <summary>Cancelable; PreventDefault skips returning focus to the trigger on close.</summary>
    [Parameter] public EventCallback<NaviusCloseAutoFocusEventArgs> OnCloseAutoFocus { get; set; }

    /// <summary>Cancelable; PreventDefault keeps the popup open when Escape is pressed.</summary>
    [Parameter] public EventCallback<NaviusEscapeKeyDownEventArgs> OnEscapeKeyDown { get; set; }

    /// <summary>Cancelable; PreventDefault keeps the popup open on an outside pointer-down.</summary>
    [Parameter] public EventCallback<NaviusPointerDownOutsideEventArgs> OnPointerDownOutside { get; set; }

    /// <summary>Cancelable; PreventDefault keeps the popup open when focus moves outside.</summary>
    [Parameter] public EventCallback<NaviusFocusOutsideEventArgs> OnFocusOutside { get; set; }

    /// <summary>Cancelable; PreventDefault keeps the popup open on any outside interaction.</summary>
    [Parameter] public EventCallback<NaviusInteractOutsideEventArgs> OnInteractOutside { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }

    protected override bool ShouldStayMounted => KeepMounted;

    /// <summary>Close the popup when Escape is pressed (default true).</summary>
    protected virtual bool CloseOnEscape => true;

    /// <summary>Close the popup on a pointer-down outside it (default true; Alert Dialog overrides to false).</summary>
    protected virtual bool CloseOnOutside => true;

    /// <summary>Trap focus inside the popup while open (default = modal).</summary>
    protected virtual bool TrapFocus => OverlayContext.Modal;

    /// <summary>Lock page scroll while open (default = modal).</summary>
    protected virtual bool LockPageScroll => OverlayContext.Modal;

    /// <summary>Move focus into the popup on open (default true; Tooltip/Preview Card override to false).</summary>
    protected virtual bool MoveFocusInside => true;

    /// <summary>True when the last open's <c>OnOpenAutoFocus</c> was prevented (set during EngageAsync).</summary>
    protected bool OpenAutoFocusPrevented { get; private set; }

    /// <summary>Optional CSS selector for the element to focus first (Alert Dialog targets Cancel).</summary>
    protected virtual string? InitialFocusSelector => null;

    /// <summary>
    /// The element the dismissable layer treats as "inside" in addition to the popup itself,
    /// so a click on it does not dismiss. Defaults to the trigger (a trigger-less overlay
    /// falls back to the popup). Context Menu overrides this to the popup only, so a click
    /// anywhere in its (possibly large) trigger area still dismisses.
    /// </summary>
    protected virtual ElementReference DismissReference =>
        OverlayContext.HasTrigger ? OverlayContext.TriggerElement : Element;

    /// <summary>
    /// An optional additional element the dismissable layer treats as "inside" (besides the
    /// popup and <see cref="DismissReference"/>). Autocomplete uses it for its optional
    /// dropdown Trigger button, so that button toggles the popup rather than racing the
    /// outside-dismiss (pointer-down closes, click reopens). Null (default) = none.
    /// </summary>
    protected virtual ElementReference? DismissSecondaryReference => null;

    /// <summary>
    /// The initial-focus selector, given whether <c>onOpenAutoFocus</c> was prevented.
    /// Default: prevented =&gt; null (the trap falls back to the first focusable). Alert
    /// Dialog overrides this to focus the panel itself when prevented.
    /// </summary>
    protected virtual string? ResolveInitialFocus(bool openAutoFocusPrevented)
        => openAutoFocusPrevented ? null : InitialFocusSelector;

    protected override async Task EngageAsync()
    {
        _selfRef ??= DotNetObjectReference.Create((OverlayPopupBase)this);

        if (LockPageScroll)
        {
            await Interop!.LockScrollAsync();
            _scrollLocked = true;
        }

        // The dismissable layer excludes the trigger (TriggerElement) so the trigger's
        // own click toggles rather than dismiss-then-reopen. When there is no trigger
        // (e.g. a programmatically-opened dialog / the mobile sidebar) fall back to the
        // popup element itself, so a default/unconfigured ElementReference is never
        // marshaled into JS interop. Context Menu overrides DismissReference (see there).
        var dismissReference = DismissReference;
        var secondaryReference = DismissSecondaryReference;
        var dismissOptions = new DismissOptions(CloseOnEscape, CloseOnOutside);
        _dismiss = secondaryReference.HasValue
            ? await Interop!.CreateDismissableLayerAsync(
                Element, dismissReference, _selfRef, dismissOptions, secondaryReference.Value)
            : await Interop!.CreateDismissableLayerAsync(
                Element, dismissReference, _selfRef, dismissOptions);

        // onOpenAutoFocus fires BEFORE focus moves in, so PreventDefault can redirect it
        // (e.g. Alert Dialog targets the panel instead of Cancel).
        var prevented = false;
        if (MoveFocusInside && OnOpenAutoFocus.HasDelegate)
        {
            var openArgs = new NaviusOpenAutoFocusEventArgs();
            await OnOpenAutoFocus.InvokeAsync(openArgs);
            prevented = openArgs.DefaultPrevented;
        }

        // Surfaced to subclasses so a menu popup's roving focus can honour a prevented
        // OnOpenAutoFocus (pass AutoFocus: !OpenAutoFocusPrevented) instead of always
        // focusing the first item — the base only gates its own Element.FocusAsync on this.
        OpenAutoFocusPrevented = prevented;

        if (TrapFocus)
        {
            var selector = ResolveInitialFocus(prevented);
            _focusTrap = selector is null
                ? await Interop!.CreateFocusTrapAsync(Element)
                : await Interop!.CreateFocusTrapAsync(Element, new FocusTrapOptions(selector));
        }
        else if (MoveFocusInside && !prevented)
        {
            try { await Element.FocusAsync(); } catch { /* element may be gone */ }
        }
    }

    /// <summary>
    /// Re-create the dismissable layer against the CURRENT <see cref="DismissReference"/>
    /// (+ <see cref="DismissSecondaryReference"/>), mirroring the layer creation in
    /// <see cref="EngageAsync"/>. A shared popup that stays engaged while its anchor moves
    /// (NavigationMenu's morphing shared viewport) must call this after re-anchoring, or the
    /// layer keeps excluding the FIRST active trigger and a click on the now-active trigger
    /// is treated as outside (dismiss-then-reopen flicker). No-op while not engaged.
    /// </summary>
    protected async Task RebuildDismissableLayerAsync()
    {
        if (_dismiss is null)
        {
            return;
        }

        await _dismiss.DisposeAsync();
        _dismiss = null;

        _selfRef ??= DotNetObjectReference.Create((OverlayPopupBase)this);
        var dismissReference = DismissReference;
        var secondaryReference = DismissSecondaryReference;
        var dismissOptions = new DismissOptions(CloseOnEscape, CloseOnOutside);
        _dismiss = secondaryReference.HasValue
            ? await Interop!.CreateDismissableLayerAsync(
                Element, dismissReference, _selfRef, dismissOptions, secondaryReference.Value)
            : await Interop!.CreateDismissableLayerAsync(
                Element, dismissReference, _selfRef, dismissOptions);
    }

    protected override async Task DisengageAsync()
    {
        var closeArgs = new NaviusCloseAutoFocusEventArgs();
        if (OnCloseAutoFocus.HasDelegate)
        {
            await OnCloseAutoFocus.InvokeAsync(closeArgs);
        }

        var hadTrap = _focusTrap is not null;
        if (_focusTrap is not null)
        {
            await _focusTrap.ReleaseAsync(restore: !closeArgs.DefaultPrevented);
            await _focusTrap.DisposeAsync();
            _focusTrap = null;
        }

        if (_scrollLocked)
        {
            await (Interop?.UnlockScrollAsync() ?? Task.CompletedTask);
            _scrollLocked = false;
        }

        if (_dismiss is not null)
        {
            await _dismiss.DisposeAsync();
            _dismiss = null;
        }

        // Non-modal (no trap restored focus): return focus to the trigger ourselves —
        // but only if focus hasn't already legitimately moved elsewhere. Because disengage
        // is deferred behind the exit animation, another widget (the next menubar menu, a
        // dialog opened from a menu item) may have grabbed focus in the same interaction;
        // stealing it back to this popup's trigger a frame later is wrong.
        if (!hadTrap && MoveFocusInside && !closeArgs.DefaultPrevented && OverlayContext.HasTrigger)
        {
            var restorable = Interop is null || await Interop.IsFocusRestorableAsync(Element);
            if (restorable)
            {
                try { await OverlayContext.TriggerElement.FocusAsync(); } catch { /* trigger may be gone */ }
            }
        }
    }

    /// <summary>
    /// Invoked from the JS dismissable layer. <paramref name="reason"/> is "escape" or
    /// "outside"; each cancelable callback may PreventDefault to keep the popup open.
    /// </summary>
    [JSInvokable]
    public async Task OnDismiss(string reason)
    {
        if (reason == "escape")
        {
            var args = new NaviusEscapeKeyDownEventArgs();
            if (OnEscapeKeyDown.HasDelegate)
            {
                await OnEscapeKeyDown.InvokeAsync(args);
            }
            if (args.DefaultPrevented)
            {
                return;
            }
        }
        else // "outside"
        {
            var pointerArgs = new NaviusPointerDownOutsideEventArgs();
            if (OnPointerDownOutside.HasDelegate)
            {
                await OnPointerDownOutside.InvokeAsync(pointerArgs);
            }

            var focusArgs = new NaviusFocusOutsideEventArgs();
            if (OnFocusOutside.HasDelegate)
            {
                await OnFocusOutside.InvokeAsync(focusArgs);
            }

            var interactArgs = new NaviusInteractOutsideEventArgs();
            if (OnInteractOutside.HasDelegate)
            {
                await OnInteractOutside.InvokeAsync(interactArgs);
            }

            if (pointerArgs.DefaultPrevented || focusArgs.DefaultPrevented || interactArgs.DefaultPrevented)
            {
                return;
            }
        }

        await OverlayContext.RequestCloseAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _selfRef?.Dispose();
        _selfRef = null;
    }
}
