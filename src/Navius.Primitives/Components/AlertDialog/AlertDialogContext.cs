using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;

namespace Navius.Primitives.Components.AlertDialog;

/// <summary>
/// Shared state for one alert dialog, cascaded from <see cref="NaviusAlertDialog"/>
/// to its parts. The root owns the authoritative open/closed state; parts request
/// changes through here and re-render via the <see cref="Changed"/> event.
/// This is the Blazor stand-in for the spec's React context.
///
/// Unlike a plain dialog, an alert dialog interrupts the user and MUST NOT be
/// dismissed by an outside click — only an explicit Cancel/Action (and Escape)
/// closes it. Initial focus lands on the Cancel control (per the APG), so the
/// destructive default never receives focus.
/// </summary>
public sealed class AlertDialogContext : IOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public AlertDialogContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    /// <summary>Authoritative open state, written only by the root via <see cref="SetOpenInternalAsync"/>.</summary>
    public bool Open { get; private set; }

    /// <summary>An alert dialog is always modal: it traps focus and locks scroll.</summary>
    public bool Modal => true;

    /// <summary>Stable ids so the content can wire aria-labelledby / aria-describedby.</summary>
    public string ContentId { get; } = $"navius-alert-dialog-{Guid.NewGuid():N}";
    public string TitleId => $"{ContentId}-title";
    public string DescriptionId => $"{ContentId}-desc";

    /// <summary>The element that opened the dialog, so focus can be restored to it on close.</summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>
    /// The Cancel control, registered by <see cref="NaviusAlertDialogCancel"/>. The content
    /// focuses this on open (APG recommends focusing the least destructive action).
    /// </summary>
    public ElementReference CancelElement { get; set; }
    public bool HasCancel { get; set; }

    /// <summary>
    /// Portal target + force-mount, supplied by an optional <c>NaviusAlertDialogPortal</c>
    /// wrapper and read by the Overlay/Content. <see cref="PortalContainer"/> mirrors the spec's
    /// <c>Portal.container</c> (a CSS selector here); <see cref="PortalForceMount"/> mirrors
    /// <c>Portal.forceMount</c> and, when set, force-mounts BOTH Overlay and Content.
    /// </summary>
    public string? PortalContainer { get; set; }
    public bool? PortalForceMount { get; set; }

    /// <summary>Raised after the open state changes so subscribed parts can re-render.</summary>
    public event Func<Task>? Changed;

    internal async Task SetOpenInternalAsync(bool open)
    {
        if (Open == open)
        {
            return;
        }

        Open = open;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task RequestSetAsync(bool open) => _requestSetOpen(open);

    public Task RequestCloseAsync() => _requestSetOpen(false);

    public Task RequestToggleAsync() => _requestSetOpen(!Open);
}
