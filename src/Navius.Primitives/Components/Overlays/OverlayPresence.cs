using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Overlays;

/// <summary>
/// The Base UI presence + enter/exit machine, shared by every animated overlay part
/// (Backdrop, Popup). Generalises the Collapsible.Panel pattern (ADR-0007): C# owns
/// the discrete <c>data-open</c>/<c>data-closed</c> + <c>data-starting-style</c>/
/// <c>data-ending-style</c> attributes; the engine supplies the timing C# cannot do
/// synchronously (<see cref="NaviusJsInterop.NextFrameAsync"/> commits the
/// starting-style frame; <see cref="NaviusJsInterop.WaitForAnimationsAsync"/> defers
/// unmount until the exit transition finishes).
///
/// The razor subclass renders the animated element, captures <c>@ref="Element"</c>,
/// and supplies <see cref="OverlayContext"/>. Subclasses override
/// <see cref="EngageAsync"/>/<see cref="DisengageAsync"/> to attach/detach engine
/// resources on mount/unmount.
/// </summary>
public abstract class OverlayPresence : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = default!;

    protected NaviusJsInterop? Interop;

    /// <summary>The element that animates in/out — the razor must capture <c>@ref="Element"</c>.</summary>
    protected ElementReference Element;

    /// <summary>Render gate: <c>@if (Rendered)</c> in the razor.</summary>
    protected bool Rendered;

    /// <summary>True for the one frame the element carries <c>data-starting-style</c>.</summary>
    protected bool Entering;

    /// <summary>True while the element carries <c>data-ending-style</c> (exit transition running).</summary>
    protected bool Exiting;

    private Func<Task>? _onChange;
    private bool _wasOpen;
    private bool _engaged;
    private bool _enterScheduled;
    private bool _exitScheduled;
    private bool _reEngageScheduled;

    /// <summary>The cascaded overlay context (supplied by the razor subclass).</summary>
    protected abstract IOverlayContext OverlayContext { get; }

    protected bool IsOpen => OverlayContext.Open;

    /// <summary>Keep the node mounted (hidden) while closed instead of unmounting it.</summary>
    protected virtual bool ShouldStayMounted => false;

    /// <summary>Attach engine resources once the node is mounted. Override in subclasses.</summary>
    protected virtual Task EngageAsync() => Task.CompletedTask;

    /// <summary>Detach engine resources as the node unmounts (after the exit animation). Override.</summary>
    protected virtual Task DisengageAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the node is re-opened while still engaged (a deferred exit was cancelled),
    /// so a still-live engine resource can be refreshed against moved state. Default no-op;
    /// anchored popups override it to re-place the positioner (the anchor may have moved).
    /// </summary>
    protected virtual Task ReEngageAsync() => Task.CompletedTask;

    protected override void OnInitialized()
    {
        _wasOpen = IsOpen;
        Rendered = IsOpen || ShouldStayMounted;
        _onChange = OnContextChangedAsync;
        OverlayContext.Changed += _onChange;
    }

    private async Task OnContextChangedAsync()
    {
        var open = IsOpen;
        if (open && !_wasOpen)
        {
            _wasOpen = true;
            Rendered = true;
            Exiting = false;
            _exitScheduled = false;   // cancel a pending exit (re-opened mid-exit)
            Entering = true;          // mount carrying data-starting-style (no open-state flash)
            _enterScheduled = true;
            if (_engaged)
            {
                // Re-opened before the deferred exit disengaged — the node stays engaged,
                // so the active machinery is never re-run. If the anchor moved between
                // close and reopen (e.g. a context menu re-opened at a new cursor point),
                // the positioner still points at the old spot; schedule a re-place.
                _reEngageScheduled = true;
            }
        }
        else if (!open && _wasOpen)
        {
            _wasOpen = false;
            Entering = false;
            _enterScheduled = false;  // cancel a pending enter
            if (_engaged)
            {
                Exiting = true;       // keep rendered through the exit animation, then disengage + unmount
                _exitScheduled = true;
            }
            else if (!ShouldStayMounted)
            {
                // Never engaged (e.g. opened then closed before the first render engaged
                // it) — there is nothing to animate out or tear down, so just drop the node.
                Rendered = false;
            }
        }
        await InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Engage the active machinery only while OPEN — never merely because the node is
        // rendered. A force/keep-mounted popup renders while closed and must stay inert
        // (otherwise it would scroll-lock + focus-trap + arm the positioner on a closed
        // popup, and never disengage).
        if (IsOpen && Rendered && !_engaged)
        {
            _engaged = true;
            Interop ??= new NaviusJsInterop(JS);
            await EngageAsync();
        }

        if (_reEngageScheduled)
        {
            _reEngageScheduled = false;
            await ReEngageAsync();            // re-place the positioner against a moved reference
        }

        if (_enterScheduled)
        {
            _enterScheduled = false;
            Interop ??= new NaviusJsInterop(JS);
            await Interop.NextFrameAsync();   // commit the starting-style frame
            Entering = false;
            StateHasChanged();                // drop data-starting-style -> transition in
        }

        if (_exitScheduled)
        {
            _exitScheduled = false;
            Interop ??= new NaviusJsInterop(JS);
            await Interop.WaitForAnimationsAsync(Element);   // await the exit transition
            Exiting = false;
            if (!IsOpen)                       // not re-opened mid-exit
            {
                _engaged = false;
                await DisengageAsync();
                if (!ShouldStayMounted)
                {
                    Rendered = false;          // unmount once the animation finishes
                }
            }
            StateHasChanged();
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_onChange is not null)
        {
            OverlayContext.Changed -= _onChange;
            _onChange = null;
        }
        if (_engaged)
        {
            _engaged = false;
            await DisengageAsync();
        }
        if (Interop is not null)
        {
            await Interop.DisposeAsync();
            Interop = null;
        }
        GC.SuppressFinalize(this);
    }
}
