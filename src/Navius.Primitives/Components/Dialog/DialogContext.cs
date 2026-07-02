using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;

namespace Navius.Primitives.Components.Dialog;

/// <summary>
/// Shared state for one dialog, cascaded from <see cref="NaviusDialog"/> to its
/// parts. The root owns the authoritative open/closed state; parts request
/// changes through here and re-render via the <see cref="Changed"/> event.
/// Implements <see cref="IOverlayContext"/> so the dialog reuses the shared overlay
/// machinery (<see cref="OverlayPopupBase"/>, <see cref="OverlayPresence"/>).
/// </summary>
public sealed class DialogContext : IOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public DialogContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    /// <summary>Authoritative open state, written only by the root via <see cref="SetOpenInternalAsync"/>.</summary>
    public bool Open { get; private set; }

    /// <summary>
    /// the spec <c>modal</c> (default true). When false the content does not trap focus,
    /// does not lock scroll, and leaves outside content interactive.
    /// </summary>
    public bool Modal { get; set; } = true;

    /// <summary>Stable ids so the content can wire aria-labelledby / aria-describedby.</summary>
    public string ContentId { get; } = $"navius-dialog-{Guid.NewGuid():N}";
    public string TitleId => $"{ContentId}-title";
    public string DescriptionId => $"{ContentId}-desc";

    /// <summary>
    /// The trigger element — set by the trigger after first render. Read by the
    /// content so the dismissable layer treats clicks on the trigger as "inside"
    /// (otherwise a toggling trigger would re-close immediately).
    /// </summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>
    /// Presence tracking for the labelling parts. the spec only emits
    /// aria-labelledby / aria-describedby when a Title / Description is mounted,
    /// avoiding a dangling IDREF. Ref-counted so multiple parts (or fast
    /// remounts) stay correct.
    /// </summary>
    private int _titleCount;
    private int _descriptionCount;

    public bool HasTitle => _titleCount > 0;
    public bool HasDescription => _descriptionCount > 0;

    public void RegisterTitle() => _titleCount++;
    public void UnregisterTitle() { if (_titleCount > 0) _titleCount--; }
    public void RegisterDescription() => _descriptionCount++;
    public void UnregisterDescription() { if (_descriptionCount > 0) _descriptionCount--; }

    /// <summary>
    /// Portal target + force-mount, supplied by an optional <c>NaviusDialogPortal</c>
    /// wrapper and read by the Overlay/Content. <see cref="PortalContainer"/> mirrors
    /// the spec's <c>Portal.container</c> (a CSS selector here); <see cref="PortalForceMount"/>
    /// mirrors <c>Portal.forceMount</c> and, when set, force-mounts BOTH Overlay and Content.
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
