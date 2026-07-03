using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Navius.Primitives.Interop;

/// <summary>
/// The bridge to the headless engine's JavaScript module. Lazily imports
/// <c>navius-interop.js</c> on first use and exposes the focus-trap, scroll-lock,
/// positioning and dismissable-layer primitives to C#. One instance per
/// consuming component is fine — the browser caches the dynamic <c>import()</c>.
/// </summary>
public sealed class NaviusJsInterop : IAsyncDisposable
{
    private const string ModulePath = "./_content/Navius.Primitives/navius-interop.js";

    private readonly Lazy<Task<IJSObjectReference>> _module;

    public NaviusJsInterop(IJSRuntime js)
    {
        _module = new Lazy<Task<IJSObjectReference>>(
            () => js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    /// <summary>Trap focus inside <paramref name="container"/> and focus its first focusable element.</summary>
    public async Task<FocusTrap> CreateFocusTrapAsync(ElementReference container)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createFocusTrap", container);
        return new FocusTrap(handle);
    }

    /// <summary>
    /// Trap focus inside <paramref name="container"/> with <paramref name="options"/>
    /// (e.g. an <see cref="FocusTrapOptions.InitialFocus"/> selector). Overload of the
    /// option-less <see cref="CreateFocusTrapAsync(ElementReference)"/>.
    /// </summary>
    public async Task<FocusTrap> CreateFocusTrapAsync(ElementReference container, FocusTrapOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createFocusTrap", container, options);
        return new FocusTrap(handle);
    }

    public async Task LockScrollAsync()
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("lockScroll");
    }

    public async Task UnlockScrollAsync()
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("unlockScroll");
    }

    /// <summary>Anchor <paramref name="floating"/> to <paramref name="reference"/> with flip + clamp + auto-update.</summary>
    public async Task<Positioner> CreatePositionerAsync(
        ElementReference reference, ElementReference floating, PositionOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createPositioner", reference, floating, options);
        return new Positioner(handle);
    }

    /// <summary>
    /// Overload that additionally wires an <paramref name="arrowElement"/> the engine
    /// positions to point at the reference centre (see <see cref="PositionOptions.ArrowPadding"/>).
    /// The live DOM node is passed as a separate argument because it cannot be
    /// serialized inside <paramref name="options"/>.
    /// </summary>
    public async Task<Positioner> CreatePositionerAsync(
        ElementReference reference, ElementReference floating, PositionOptions options,
        ElementReference arrowElement)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createPositioner", reference, floating, options, arrowElement);
        return new Positioner(handle);
    }

    /// <summary>
    /// Base UI anatomy overload: anchors the <paramref name="positioner"/> element and
    /// additionally mirrors <c>data-side</c>/<c>data-align</c>/<c>data-anchor-hidden</c>
    /// onto the nested <paramref name="popupElement"/> (the visual Popup), so helm
    /// <c>data-[side=…]</c> style hooks match on the Popup, not just the positioning
    /// wrapper. <paramref name="arrowElement"/> is optional (null = no arrow).
    /// </summary>
    public async Task<Positioner> CreatePositionerAsync(
        ElementReference reference, ElementReference positioner, PositionOptions options,
        ElementReference? arrowElement, ElementReference popupElement)
    {
        var module = await _module.Value;
        object? arrowArg = arrowElement.HasValue ? arrowElement.Value : null;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createPositioner", reference, positioner, options, arrowArg, popupElement);
        return new Positioner(handle);
    }

    /// <summary>
    /// Close-on-Escape / close-on-outside-pointer layer that invokes
    /// <c>OnDismiss</c> on <paramref name="callback"/> when a dismiss is requested.
    /// </summary>
    public async Task<DismissableLayer> CreateDismissableLayerAsync<T>(
        ElementReference content, ElementReference reference,
        DotNetObjectReference<T> callback, DismissOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createDismissableLayer", content, reference, callback, options);
        return new DismissableLayer(handle);
    }

    /// <summary>
    /// Overload treating a <paramref name="reference2"/> element as "inside" in addition to
    /// <paramref name="reference"/>, so a click on a secondary trigger (e.g. Autocomplete's
    /// optional dropdown button) toggles the popup rather than racing an outside-dismiss.
    /// The live node is passed positionally (like the positioner's arrow) since it cannot be
    /// serialized inside <paramref name="options"/>.
    /// </summary>
    public async Task<DismissableLayer> CreateDismissableLayerAsync<T>(
        ElementReference content, ElementReference reference,
        DotNetObjectReference<T> callback, DismissOptions options,
        ElementReference reference2) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createDismissableLayer", content, reference, callback, options, reference2);
        return new DismissableLayer(handle);
    }

    /// <summary>Arrow/Home/End/type-ahead focus movement among the items inside <paramref name="container"/>.</summary>
    public async Task<RovingFocus> CreateRovingFocusAsync(ElementReference container, RovingFocusOptions options)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createRovingFocus", container, options);
        return new RovingFocus(handle);
    }

    /// <summary>
    /// Teleport <paramref name="element"/> into <c>document.body</c> so overlays escape
    /// clipping/stacking ancestors. Dispose the returned handle to move it back. For Portal.
    /// </summary>
    public async Task<Teleport> TeleportToBodyAsync(ElementReference element)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("teleportToBody", element);
        return new Teleport(handle);
    }

    /// <summary>
    /// Teleport <paramref name="element"/> into <paramref name="container"/> (a CSS selector)
    /// instead of <c>document.body</c>.
    /// </summary>
    public async Task<Teleport> TeleportToBodyAsync(ElementReference element, string container)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("teleportToBody", element, container);
        return new Teleport(handle);
    }

    /// <summary>
    /// Publish <paramref name="element"/>'s natural content size as
    /// <c>--{varPrefix}-panel-width/height</c> on mount and resize, for
    /// CSS-animated open/close. For Collapsible / Accordion.
    /// </summary>
    public async Task<SizeObserver> CreateSizeObserverAsync(ElementReference element, string varPrefix)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createSizeObserver", element, varPrefix);
        return new SizeObserver(handle);
    }

    /// <summary>
    /// Resolve after the browser has painted (double rAF) so a just-rendered
    /// <c>data-starting-style</c> frame is committed before C# removes it. The
    /// enter half of the Base UI presence model.
    /// </summary>
    public async Task NextFrameAsync()
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("nextFrame");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Resolve once every running CSS transition/animation on
    /// <paramref name="element"/> has finished (or immediately if there are none),
    /// so C# can defer unmount until an exit animation ends. The exit half of the
    /// Base UI presence model.
    /// </summary>
    public async Task WaitForAnimationsAsync(ElementReference element)
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("waitForAnimations", element);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Whether focus may safely be restored to a trigger when <paramref name="element"/>
    /// closes: true when focus is still inside it (or nothing meaningful holds focus),
    /// false when another widget already grabbed focus — so a deferred close-refocus never
    /// steals focus back from where it legitimately moved (menubar menu-switch, a dialog
    /// opened from a menu item). Defaults to true if interop is gone (preserve behavior).
    /// </summary>
    public async Task<bool> IsFocusRestorableAsync(ElementReference element)
    {
        var module = await _module.Value;
        try
        {
            return await module.InvokeAsync<bool>("isFocusRestorable", element);
        }
        catch (JSDisconnectedException)
        {
            return true;
        }
    }

    /// <summary>
    /// Relay a <c>hidden="until-found"</c> panel's <c>beforematch</c> event (the
    /// browser's in-page find expanding it) to <c>OnBeforeMatch</c> on
    /// <paramref name="callback"/>. For Collapsible / Accordion <c>hiddenUntilFound</c>.
    /// </summary>
    public async Task<BeforeMatchListener> ObserveBeforeMatchAsync<T>(
        ElementReference element, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("observeBeforeMatch", element, callback);
        return new BeforeMatchListener(handle);
    }

    /// <summary>
    /// Mirror the active panel's measured size onto the NavigationMenu
    /// <paramref name="viewport"/> as CSS vars (ResizeObserver/MutationObserver on
    /// <paramref name="slot"/>). CSP-safe replacement for the former eval() IIFE.
    /// </summary>
    public async Task<ViewportMirror> CreateViewportMirrorAsync(ElementReference viewport, ElementReference slot)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>("createViewportMirror", viewport, slot);
        return new ViewportMirror(handle);
    }

    /// <summary>Write the active trigger's <paramref name="size"/>/<paramref name="position"/> onto the NavigationMenu indicator as CSS vars.</summary>
    public async Task SetIndicatorPositionAsync(ElementReference element, double size, double position)
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("setIndicatorPosition", element, size, position);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Focus the first focusable descendant of <paramref name="element"/> (APG keyboard-open), falling back to it.</summary>
    public async Task FocusFirstDescendantAsync(ElementReference element)
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("focusFirstDescendant", element);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Scroll the element with <paramref name="id"/> into view (Command active-item follow).</summary>
    public async Task ScrollIntoViewByIdAsync(string id, string block = "nearest")
    {
        var module = await _module.Value;
        try
        {
            await module.InvokeVoidAsync("scrollIntoViewById", id, block);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Mirror <paramref name="input"/>'s native ValidityState + interaction state
    /// (focus/blur/input + initial value) to <c>OnFieldStateChange</c> on
    /// <paramref name="callback"/>. For Field/Input.
    /// </summary>
    public async Task<ConstraintValidation> CreateConstraintValidationAsync<T>(
        ElementReference input, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createConstraintValidation", input, callback);
        return new ConstraintValidation(handle);
    }

    /// <summary>Submit the form <paramref name="element"/> belongs to via <c>requestSubmit()</c>. For OTP.</summary>
    public async Task SubmitClosestFormAsync(ElementReference element)
    {
        var module = await _module.Value;
        await module.InvokeVoidAsync("submitClosestForm", element);
    }

    /// <summary>Read an element's trimmed <c>textContent</c> (Select item label / Toast announcement text).</summary>
    public async Task<string> GetTextContentAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<string>("getTextContent", element);
    }

    /// <summary>True once an <c>&lt;img&gt;</c> has loaded successfully (covers cached/prerendered images). For Avatar.</summary>
    public async Task<bool> IsImageCompleteAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<bool>("isImageComplete", element);
    }

    /// <summary>
    /// Invoke <c>OnFormSubmit</c>/<c>OnFormReset</c> on <paramref name="callback"/> when
    /// <paramref name="input"/>'s closest form is submitted or reset. For OTP / PasswordToggleField.
    /// </summary>
    public async Task<FormResetSubmitListener> CreateFormResetSubmitListenerAsync<T>(
        ElementReference input, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createFormResetSubmitListener", input, callback);
        return new FormResetSubmitListener(handle);
    }

    /// <summary>
    /// Fire <c>OnLongPress</c> {x,y} on <paramref name="callback"/> after a press-and-hold on
    /// <paramref name="element"/>. For ContextMenu touch.
    /// </summary>
    public async Task<LongPress> CreateLongPressAsync<T>(
        ElementReference element, DotNetObjectReference<T> callback, LongPressOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createLongPress", element, callback, options);
        return new LongPress(handle);
    }

    /// <summary>Return <paramref name="element"/>'s bounding rect as a plain object. For keyboard anchoring.</summary>
    public async Task<DomRect> GetRectAsync(ElementReference element)
    {
        var module = await _module.Value;
        return await module.InvokeAsync<DomRect>("getRect", element);
    }

    /// <summary>
    /// Swipe-to-dismiss + pause/resume timer signals for a toast rooted at
    /// <paramref name="rootElement"/>, invoking the swipe/pause/resume methods on
    /// <paramref name="callback"/>. For Toast.
    /// </summary>
    public async Task<ToastInteractions> CreateToastInteractionsAsync<T>(
        ElementReference rootElement, DotNetObjectReference<T> callback, ToastInteractionOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createToastInteractions", rootElement, callback, options);
        return new ToastInteractions(handle);
    }

    /// <summary>
    /// Global hotkey (default F8) that invokes <c>OnHotkey</c> on <paramref name="callback"/> to
    /// focus the toast region. For Toast.
    /// </summary>
    public async Task<ToastHotkey> CreateToastHotkeyAsync<T>(
        DotNetObjectReference<T> callback, string[]? keys = null) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createToastHotkey", callback, keys ?? new[] { "F8" });
        return new ToastHotkey(handle);
    }

    /// <summary>
    /// Pointer/touch drag carousel rooted at <paramref name="viewport"/> (whose first
    /// child is the slide track). Invokes <c>OnSelect</c>/<c>OnSettle</c>/
    /// <c>OnCanScrollChange</c> on <paramref name="callback"/>. Keyboard + autoplay
    /// stay in C#. For Carousel.
    /// </summary>
    public async Task<Carousel> CreateCarouselAsync<T>(
        ElementReference viewport, DotNetObjectReference<T> callback, CarouselOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createCarousel", viewport, callback, options);
        return new Carousel(handle);
    }

    /// <summary>
    /// Drag-to-dismiss swipe for a side sheet rooted at <paramref name="content"/>,
    /// invoking <c>OnDismiss</c> past the threshold or <c>OnReset</c> (snap back) on
    /// <paramref name="callback"/>. For Drawer.
    /// </summary>
    public async Task<SheetSwipe> CreateSheetSwipeAsync<T>(
        ElementReference content, DotNetObjectReference<T> callback, SheetSwipeOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createSheetSwipe", content, callback, options);
        return new SheetSwipe(handle);
    }

    /// <summary>
    /// Conversation-transcript scroll manager rooted at <paramref name="viewport"/>:
    /// anchored turns, streamed-reply follow, prepend scroll preservation, edge state
    /// (<c>OnScrollableChange</c>) and lazy visibility tracking
    /// (<c>OnVisibilityChange</c>) on <paramref name="callback"/>. For MessageScroller.
    /// </summary>
    public async Task<MessageScroller> CreateMessageScrollerAsync<T>(
        ElementReference viewport, DotNetObjectReference<T> callback, MessageScrollerOptions options) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createMessageScroller", viewport, callback, options);
        return new MessageScroller(handle);
    }

    /// <summary>
    /// Pointer-capture tracker over <paramref name="element"/> that streams a
    /// normalized <c>{ x, y }</c> pair (each 0..1, x mirrored under RTL) to
    /// <c>OnFraction2D</c> on <paramref name="callback"/> during a drag and
    /// <c>OnCommit2D</c> on release. The 2D sibling of the slider drag tracker; C#
    /// owns the channel mapping. For the ColorPicker area thumb.
    /// </summary>
    public async Task<PointerTracker2D> CreatePointerTracker2DAsync<T>(
        ElementReference element, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createPointerTracker2D", element, callback);
        return new PointerTracker2D(handle);
    }

    /// <summary>
    /// Wire drag/drop on <paramref name="element"/> to relay dropped files into the
    /// real hidden <paramref name="inputElement"/> (<c>&lt;input type="file"&gt;</c>)
    /// and dispatch a change event so Blazor's <c>InputFile</c> sees them, toggling
    /// <c>data-dragging</c> on the element and reporting it via
    /// <c>OnDraggingChange</c> on <paramref name="callback"/>. The handle's
    /// <see cref="FileDropzone.ClickToOpenAsync"/> opens the native file dialog. For FileUpload.
    /// </summary>
    public async Task<FileDropzone> CreateFileDropzoneAsync<T>(
        ElementReference element, ElementReference inputElement, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createFileDropzone", element, inputElement, callback);
        return new FileDropzone(handle);
    }

    /// <summary>
    /// Atomic read/write over a text <paramref name="input"/>'s value + selection for
    /// the caret-stable masking pipeline: <see cref="MaskedSelection.GetStateAsync"/>
    /// snapshots value + selection bounds, <see cref="MaskedSelection.SetStateAsync"/>
    /// writes value + <c>setSelectionRange</c> in one call. No callback. For
    /// MaskedInput / CurrencyInput.
    /// </summary>
    public async Task<MaskedSelection> CreateMaskedSelectionAsync(ElementReference input)
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createMaskedSelection", input);
        return new MaskedSelection(handle);
    }

    /// <summary>
    /// Pointer-driven drag-to-reorder over the direct children of
    /// <paramref name="container"/>. Emits <c>OnDragStart</c>/<c>OnDragOver</c>/
    /// <c>OnDrop</c>/<c>OnCancel</c> on <paramref name="callback"/> with list indices
    /// and paints <c>data-dragging</c>/<c>data-drop-target</c> hooks; C# owns the
    /// collection and commits the reorder on re-render (Escape cancels). Keyboard
    /// reordering stays in C#. For Sortable.
    /// </summary>
    public async Task<Sortable> CreateSortableAsync<T>(
        ElementReference container, SortableOptions options, DotNetObjectReference<T> callback) where T : class
    {
        var module = await _module.Value;
        var handle = await module.InvokeAsync<IJSObjectReference>(
            "createSortable", container, options, callback);
        return new Sortable(handle);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated)
        {
            return;
        }

        try
        {
            var module = await _module.Value;
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (Server prerender/teardown) — nothing to clean up.
        }
    }
}

/// <summary>A live focus trap. Dispose to remove the key handler and restore focus.</summary>
public sealed class FocusTrap : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;
    private bool _released;

    internal FocusTrap(IJSObjectReference handle) => _handle = handle;

    /// <summary>
    /// Release the trap explicitly, optionally skipping focus restoration. Pass
    /// <paramref name="restore"/> = false to leave focus where it currently is.
    /// After this, <see cref="DisposeAsync"/> only disposes the JS handle. The
    /// parameterless <c>release()</c> path (restore = true) is what
    /// <see cref="DisposeAsync"/> uses by default, so existing callers are unchanged.
    /// </summary>
    public async Task ReleaseAsync(bool restore = true)
    {
        if (_released)
        {
            return;
        }

        try
        {
            await _handle.InvokeVoidAsync("release", restore);
            _released = true;
        }
        catch (JSDisconnectedException)
        {
            _released = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_released)
            {
                await _handle.InvokeVoidAsync("release");
            }
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone; the DOM (and its listeners) are gone with it.
        }
    }
}

/// <summary>A live anchored positioner. Call <see cref="UpdateAsync"/> to re-place; dispose to detach listeners.</summary>
public sealed class Positioner : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal Positioner(IJSObjectReference handle) => _handle = handle;

    public async Task UpdateAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("update");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live dismissable layer. Dispose to remove its document listeners.</summary>
public sealed class DismissableLayer : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal DismissableLayer(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live roving-focus controller. Dispose to detach its key handler.</summary>
public sealed class RovingFocus : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal RovingFocus(IJSObjectReference handle) => _handle = handle;

    public async Task FocusFirstAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("focusFirst");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live body teleport. Dispose to move the element back to its original DOM position.</summary>
public sealed class Teleport : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal Teleport(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live content-size observer. Call <see cref="UpdateAsync"/> to re-measure; dispose to disconnect.</summary>
public sealed class SizeObserver : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal SizeObserver(IJSObjectReference handle) => _handle = handle;

    public async Task UpdateAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("update");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live <c>beforematch</c> listener (hidden-until-found panels). Dispose to detach it.</summary>
public sealed class BeforeMatchListener : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal BeforeMatchListener(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live NavigationMenu viewport size mirror. Dispose to disconnect its observers.</summary>
public sealed class ViewportMirror : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal ViewportMirror(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live constraint-validation listener. Dispose to detach its input/form handlers.</summary>
public sealed class ConstraintValidation : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal ConstraintValidation(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live form submit/reset listener. Dispose to detach its form handlers.</summary>
public sealed class FormResetSubmitListener : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal FormResetSubmitListener(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live long-press detector. Dispose to detach its pointer handlers and clear the timer.</summary>
public sealed class LongPress : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal LongPress(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live toast interaction controller (swipe + pause/resume). Dispose to detach all handlers.</summary>
public sealed class ToastInteractions : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal ToastInteractions(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live global toast hotkey listener. Dispose to remove the document keydown handler.</summary>
public sealed class ToastHotkey : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal ToastHotkey(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live pointer/touch carousel. Drive it with <see cref="ScrollNextAsync"/>/
/// <see cref="ScrollPrevAsync"/>/<see cref="ScrollToAsync"/>, re-measure with
/// <see cref="ReInitAsync"/>, and dispose to detach its pointer handlers.
/// </summary>
public sealed class Carousel : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal Carousel(IJSObjectReference handle) => _handle = handle;

    /// <summary>Advance to the next snap (wraps when the carousel loops).</summary>
    public async Task ScrollNextAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("scrollNext");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Go to the previous snap (wraps when the carousel loops).</summary>
    public async Task ScrollPrevAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("scrollPrev");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Scroll to the snap at <paramref name="index"/>.</summary>
    public async Task ScrollToAsync(int index)
    {
        try
        {
            await _handle.InvokeVoidAsync("scrollTo", index);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Re-measure slide geometry (after a resize or content change) and re-snap.</summary>
    public async Task ReInitAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("reInit");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A live drag-to-dismiss sheet swipe. Dispose to detach its pointer handlers.</summary>
public sealed class SheetSwipe : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal SheetSwipe(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live conversation-transcript scroll manager. Drive it with
/// <see cref="ScrollToMessageAsync"/>/<see cref="ScrollToStartAsync"/>/
/// <see cref="ScrollToEndAsync"/>, push option changes with <see cref="UpdateAsync"/>,
/// gate visibility reporting with <see cref="SetVisibilityTrackingAsync"/>, and
/// dispose to detach its observers and listeners.
/// </summary>
public sealed class MessageScroller : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MessageScroller(IJSObjectReference handle) => _handle = handle;

    /// <summary>
    /// Scroll the row with <paramref name="messageId"/> into view. Queues the target
    /// when called before any rows exist; returns false for an id missing from a
    /// mounted transcript.
    /// </summary>
    public async Task<bool> ScrollToMessageAsync(string messageId, MessageScrollerScrollOptions? options = null)
    {
        try
        {
            return await _handle.InvokeAsync<bool>("scrollToMessage", messageId, options);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Scroll to the start of the transcript (releases any streamed-reply follow).</summary>
    public async Task<bool> ScrollToStartAsync(MessageScrollerScrollOptions? options = null)
    {
        try
        {
            return await _handle.InvokeAsync<bool>("scrollToStart", options);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Scroll to the live edge (re-engages follow when autoScroll is enabled).</summary>
    public async Task<bool> ScrollToEndAsync(MessageScrollerScrollOptions? options = null)
    {
        try
        {
            return await _handle.InvokeAsync<bool>("scrollToEnd", options);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Push changed provider options into the engine.</summary>
    public async Task UpdateAsync(MessageScrollerOptions options)
    {
        try
        {
            await _handle.InvokeVoidAsync("update", options);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>Start or stop visibility tracking (it costs nothing while stopped).</summary>
    public async Task SetVisibilityTrackingAsync(bool enabled)
    {
        try
        {
            await _handle.InvokeVoidAsync("setVisibilityTracking", enabled);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live 2D pointer tracker (ColorPicker area thumb). Streams <c>OnFraction2D</c>
/// during a drag and <c>OnCommit2D</c> on release to the callback it was created
/// with. Dispose to detach its pointer handlers.
/// </summary>
public sealed class PointerTracker2D : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal PointerTracker2D(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live file dropzone (FileUpload). Relays dropped files into the hidden native
/// input; <see cref="ClickToOpenAsync"/> opens the native file dialog. Dispose to
/// detach its drag/drop handlers.
/// </summary>
public sealed class FileDropzone : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal FileDropzone(IJSObjectReference handle) => _handle = handle;

    /// <summary>Forward a Trigger/Dropzone activation to the hidden input's <c>click()</c> (opens the OS file dialog).</summary>
    public async Task ClickToOpenAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("clickToOpen");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// Atomic value + selection access over a text input for the caret-stable masking
/// pipeline. <see cref="GetStateAsync"/> snapshots the live value and selection;
/// <see cref="SetStateAsync"/> writes value + <c>setSelectionRange</c> in one call.
/// Dispose to release the JS handle (there are no listeners to detach).
/// </summary>
public sealed class MaskedSelection : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal MaskedSelection(IJSObjectReference handle) => _handle = handle;

    /// <summary>Read the input's live value and selection bounds.</summary>
    public async Task<MaskedSelectionState> GetStateAsync()
        => await _handle.InvokeAsync<MaskedSelectionState>("getState");

    /// <summary>
    /// Write <paramref name="value"/> and set the selection to
    /// [<paramref name="selectionStart"/>, <paramref name="selectionEnd"/>] in one
    /// synchronous call. Pass equal bounds for a collapsed caret.
    /// </summary>
    public async Task SetStateAsync(string value, int selectionStart, int selectionEnd)
    {
        try
        {
            await _handle.InvokeVoidAsync("setState", value, selectionStart, selectionEnd);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>
/// A live pointer-driven sortable (drag-to-reorder). Emits <c>OnDragStart</c>/
/// <c>OnDragOver</c>/<c>OnDrop</c>/<c>OnCancel</c> to the callback it was created
/// with; C# owns the collection and commits reorders on re-render. Dispose to
/// detach its pointer + Escape handlers.
/// </summary>
public sealed class Sortable : IAsyncDisposable
{
    private readonly IJSObjectReference _handle;

    internal Sortable(IJSObjectReference handle) => _handle = handle;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _handle.InvokeVoidAsync("destroy");
            await _handle.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}

/// <summary>A plain bounding rectangle returned by <see cref="NaviusJsInterop.GetRectAsync"/>.</summary>
public sealed record DomRect(
    double X,
    double Y,
    double Top,
    double Right,
    double Bottom,
    double Left,
    double Width,
    double Height);

/// <summary>Placement options for <see cref="NaviusJsInterop.CreatePositionerAsync"/> (serialized to camelCase).</summary>
/// <remarks>
/// New collision/arrow fields are appended after <see cref="Padding"/> so existing
/// positional calls (e.g. <c>new PositionOptions(Side, Align, SideOffset, AlignOffset, Flip)</c>)
/// keep working unchanged. All new fields default to today's behaviour:
/// <see cref="AvoidCollisions"/> true maps to the original flip+clamp;
/// <see cref="CollisionPadding"/> null falls back to <see cref="Padding"/> in JS;
/// <see cref="Sticky"/>/<see cref="HideWhenDetached"/> off; <see cref="ArrowPadding"/> 0.
/// </remarks>
public sealed record PositionOptions(
    string Side = "bottom",
    string Align = "center",
    double SideOffset = 8,
    double AlignOffset = 0,
    bool Flip = true,
    double Padding = 8,
    bool AvoidCollisions = true,
    double? CollisionPadding = null,
    string? Sticky = null,
    bool HideWhenDetached = false,
    double ArrowPadding = 0);

/// <summary>Behaviour options for <see cref="NaviusJsInterop.CreateDismissableLayerAsync"/>.</summary>
public sealed record DismissOptions(
    bool CloseOnEscape = true,
    bool CloseOnOutside = true);

/// <summary>Options for <see cref="NaviusJsInterop.CreateRovingFocusAsync"/>.</summary>
/// <remarks>
/// New fields are appended after <see cref="Selector"/> and default to today's
/// behaviour: <see cref="Loop"/> true (modulo wrap), <see cref="AutoFocus"/> true
/// (focus an item on creation), <see cref="DataHighlight"/> false, <see cref="Dir"/>
/// "ltr". Existing positional calls (<c>new RovingFocusOptions()</c>,
/// <c>new RovingFocusOptions("horizontal")</c>) are unaffected.
/// </remarks>
public sealed record RovingFocusOptions(
    string Orientation = "vertical",
    string? Selector = null,
    bool Loop = true,
    bool AutoFocus = true,
    bool DataHighlight = false,
    string Dir = "ltr",
    string InitialFocus = "selected");

/// <summary>Options for <see cref="NaviusJsInterop.CreateFocusTrapAsync(ElementReference, FocusTrapOptions)"/>.</summary>
public sealed record FocusTrapOptions(
    string? InitialFocus = null);

/// <summary>Options for <see cref="NaviusJsInterop.CreateLongPressAsync"/>.</summary>
public sealed record LongPressOptions(
    double Duration = 700,
    double MoveThreshold = 10);

/// <summary>Options for <see cref="NaviusJsInterop.CreateToastInteractionsAsync"/>.</summary>
public sealed record ToastInteractionOptions(
    string Direction = "right",
    double Threshold = 50);

/// <summary>
/// Options for <see cref="NaviusJsInterop.CreateCarouselAsync"/> (serialized to
/// camelCase). Defaults: a horizontal, non-looping,
/// start-aligned track that scrolls one slide at a time.
/// </summary>
public sealed record CarouselOptions(
    string Orientation = "horizontal",
    bool Loop = false,
    string Align = "start",
    int SlidesToScroll = 1,
    double Duration = 350);

/// <summary>
/// Options for <see cref="NaviusJsInterop.CreateSheetSwipeAsync"/> (serialized to
/// camelCase). <see cref="Side"/> is the edge the sheet is docked to;
/// <see cref="DismissThreshold"/> is the fraction of the sheet's size on that axis
/// the drag must pass to dismiss.
/// </summary>
public sealed record SheetSwipeOptions(
    string Side = "bottom",
    double DismissThreshold = 0.25);

/// <summary>
/// Options for <see cref="NaviusJsInterop.CreateMessageScrollerAsync"/> (serialized
/// to camelCase). <see cref="DefaultScrollPosition"/> is one of
/// <c>start</c>/<c>end</c>/<c>last-anchor</c>; <see cref="ScrollEdgeThreshold"/> is
/// how many pixels from an edge still count as being at it;
/// <see cref="ScrollPreviousItemPeek"/> is added to <see cref="ScrollMargin"/> when
/// an appended anchor row is positioned so part of the previous item stays visible.
/// </summary>
public sealed record MessageScrollerOptions(
    bool AutoScroll = false,
    string DefaultScrollPosition = "end",
    double ScrollEdgeThreshold = 8,
    double ScrollMargin = 0,
    double ScrollPreviousItemPeek = 64,
    bool PreserveScrollOnPrepend = true);

/// <summary>
/// Per-call options for the <see cref="MessageScroller"/> scroll methods (serialized
/// to camelCase). <see cref="Align"/> is one of
/// <c>start</c>/<c>center</c>/<c>end</c>/<c>nearest</c>; a null
/// <see cref="ScrollMargin"/> falls back to the provider's margin.
/// </summary>
public sealed record MessageScrollerScrollOptions(
    string Align = "start",
    string Behavior = "auto",
    double? ScrollMargin = null);

/// <summary>
/// The normalized 2D pointer fraction streamed by <c>createPointerTracker2D</c> to
/// <c>OnFraction2D</c>/<c>OnCommit2D</c>. Both fields are 0..1; <see cref="X"/> is
/// mirrored under RTL, <see cref="Y"/> grows downward (top edge = 0). Use this as
/// the <c>[JSInvokable]</c> parameter type on the consuming component.
/// </summary>
public sealed record PointerFraction2D(
    double X,
    double Y);

/// <summary>
/// The value + selection snapshot returned by <see cref="MaskedSelection.GetStateAsync"/>
/// (deserialized from the engine's camelCase <c>{ value, selectionStart, selectionEnd }</c>).
/// </summary>
public sealed record MaskedSelectionState(
    string Value,
    int SelectionStart,
    int SelectionEnd);

/// <summary>
/// Options for <see cref="NaviusJsInterop.CreateSortableAsync"/> (serialized to
/// camelCase). <see cref="Axis"/> is one of <c>vertical</c>/<c>horizontal</c>/
/// <c>grid</c> and selects how the target index is computed from item midpoints; a
/// non-null <see cref="Handle"/> is a CSS selector that must contain the press
/// target for a drag to start (otherwise the whole item is the drag handle).
/// </summary>
public sealed record SortableOptions(
    string Axis = "vertical",
    string? Handle = null);
