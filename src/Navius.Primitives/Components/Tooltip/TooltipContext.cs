using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Tooltip;

/// <summary>
/// How an open tooltip became visible, used to drive Base UI's <c>data-instant</c>
/// attribute: <c>Delayed</c> (the hover delay elapsed — no data-instant) vs
/// <c>Instant</c> (focus, or the skip-delay window — data-instant present).
/// </summary>
public enum TooltipOpenReason
{
    Delayed,
    Instant,
}

/// <summary>
/// Shared state for one tooltip. The root owns the open state and the show/hide
/// timing; the trigger drives it via hover/focus, the popup positions against
/// <see cref="TriggerElement"/>. Implements <see cref="IAnchoredOverlayContext"/> so
/// the tooltip reuses the shared overlay machinery (<see cref="OverlayAnchoredPopupBase"/>).
/// </summary>
public sealed class TooltipContext : IAnchoredOverlayContext
{
    private readonly Func<bool, TooltipOpenReason, Task> _requestSetOpen;
    private readonly Func<Task> _openDelayed;
    private readonly Func<Task> _openNow;
    private readonly Func<Task> _close;
    private readonly Func<Task> _forceClose;

    public TooltipContext(
        Func<bool, TooltipOpenReason, Task> requestSetOpen,
        Func<Task> openDelayed,
        Func<Task> openNow,
        Func<Task> close,
        Func<Task> forceClose)
    {
        _requestSetOpen = requestSetOpen;
        _openDelayed = openDelayed;
        _openNow = openNow;
        _close = close;
        _forceClose = forceClose;
    }

    public bool Open { get; private set; }

    /// <summary>Tooltips are never modal — no focus trap, no scroll lock, no backdrop.</summary>
    public bool Modal => false;

    /// <summary>Why the tooltip last opened (only meaningful while <see cref="Open"/>).</summary>
    public TooltipOpenReason OpenReason { get; private set; } = TooltipOpenReason.Delayed;

    /// <summary>True while open due to focus / the skip-delay window — drives <c>data-instant</c>.</summary>
    public bool IsInstant => Open && OpenReason == TooltipOpenReason.Instant;

    /// <summary>When true the pointer is currently over the popup bubble (hoverable content).</summary>
    public bool PointerInContent { get; set; }

    public string ContentId { get; } = $"navius-tooltip-{Guid.NewGuid():N}";

    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>Tooltips anchor to the trigger (no separate anchor part).</summary>
    public ElementReference PositionReference => TriggerElement;

    /// <summary>Custom portal mount-container selector; null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the popup mounted while closed (the spec <c>Portal.keepMounted</c>).</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by the Positioner part.</summary>
    public PositionOptions Options { get; private set; } = new(Side: "top", SideOffset: 0);

    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>The optional arrow element, registered by <c>NaviusTooltipArrow</c> for the positioner to align.</summary>
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

    internal async Task SetOpenInternalAsync(bool open, TooltipOpenReason reason)
    {
        if (Open == open && (!open || OpenReason == reason))
        {
            return;
        }

        Open = open;
        if (open)
        {
            OpenReason = reason;
        }

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    /// <summary>Route an open/close request through the root (honours controlled Open).</summary>
    public Task RequestSetAsync(bool open, TooltipOpenReason reason) => _requestSetOpen(open, reason);

    /// <summary>Open after the configured delay (hover intent).</summary>
    public Task OpenDelayed() => _openDelayed();

    /// <summary>Open immediately (keyboard focus or skip-delay window — no delay).</summary>
    public Task OpenNow() => _openNow();

    public Task Close() => _close();

    /// <summary>Force-close (Escape / outside pointer / content leave) bypassing the hoverable grace.</summary>
    public Task RequestCloseAsync()
    {
        // Clear the hoverable flag so a stale "pointer in content" doesn't veto the next
        // close (the bubble unmounts on dismiss, so no pointerleave fires to clear it).
        PointerInContent = false;
        return _forceClose();
    }
}
