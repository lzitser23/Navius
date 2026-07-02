using Microsoft.AspNetCore.Components;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Overlays;

/// <summary>
/// A Base UI Popup whose placement is anchored to a trigger (Popover, Tooltip,
/// Preview Card). Adds the engine positioner on top of
/// <see cref="OverlayPopupBase"/>: it positions the <see cref="PositionerElement"/>
/// (the <c>Positioner</c> div) against the context's
/// <see cref="IAnchoredOverlayContext.PositionReference"/>, wiring in the arrow (when
/// present) and mirroring <c>data-side</c>/<c>data-align</c>/<c>data-anchor-hidden</c>
/// onto the popup element (<see cref="OverlayPresence.Element"/>) so helm
/// <c>data-[side=…]</c> hooks match on the visual Popup.
///
/// The razor renders <c>Portal &gt; Positioner div (@ref PositionerElement) &gt; Popup
/// div (@ref Element)</c>, reading placement options + the positioner's attributes
/// from the context (published by the Positioner part).
/// </summary>
public abstract class OverlayAnchoredPopupBase : OverlayPopupBase
{
    /// <summary>The <c>Positioner</c> div the engine transforms — razor captures <c>@ref="PositionerElement"</c>.</summary>
    protected ElementReference PositionerElement;

    private Positioner? _positioner;
    private bool _hadArrow;
    private Func<Task>? _onArrowChange;

    /// <summary>The anchored context (also supplies <see cref="OverlayPresence.OverlayContext"/>).</summary>
    protected abstract IAnchoredOverlayContext AnchoredContext { get; }

    protected override IOverlayContext OverlayContext => AnchoredContext;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        // Re-wire the positioner if an arrow registers after the popup engaged.
        _onArrowChange = () => InvokeAsync(ReEngagePositionerAsync);
        AnchoredContext.ArrowChanged += _onArrowChange;
    }

    protected override async Task EngageAsync()
    {
        await EngagePositionerAsync();
        await base.EngageAsync();
    }

    protected override async Task DisengageAsync()
    {
        await base.DisengageAsync();
        if (_positioner is not null)
        {
            await _positioner.DisposeAsync();
            _positioner = null;
        }
    }

    /// <summary>
    /// Re-opened while still engaged (a deferred exit was cancelled): re-run the positioner
    /// against the reference's current rect, so a popup whose anchor moved between close and
    /// reopen (a context menu re-opened at a new cursor point) is re-placed rather than
    /// re-appearing at the stale spot.
    /// </summary>
    protected override async Task ReEngageAsync()
    {
        if (_positioner is not null)
        {
            await _positioner.UpdateAsync();
        }
    }

    private async Task EngagePositionerAsync()
    {
        _hadArrow = AnchoredContext.HasArrow;
        _positioner = await Interop!.CreatePositionerAsync(
            AnchoredContext.PositionReference,
            PositionerElement,
            AnchoredContext.Options,
            AnchoredContext.HasArrow ? AnchoredContext.ArrowElement : (ElementReference?)null,
            Element);
    }

    private async Task ReEngagePositionerAsync()
    {
        if (_positioner is null || AnchoredContext.HasArrow == _hadArrow)
        {
            return;
        }
        await _positioner.DisposeAsync();
        _positioner = null;
        await EngagePositionerAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_onArrowChange is not null)
        {
            AnchoredContext.ArrowChanged -= _onArrowChange;
            _onArrowChange = null;
        }
        if (_positioner is not null)
        {
            await _positioner.DisposeAsync();
            _positioner = null;
        }
        await base.DisposeAsync();
    }
}
