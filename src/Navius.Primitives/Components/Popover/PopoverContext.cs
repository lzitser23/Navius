using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Popover;

/// <summary>
/// Shared state for one popover, cascaded from <see cref="NaviusPopover"/> to its
/// parts. Implements <see cref="IAnchoredOverlayContext"/> so the popover reuses the
/// shared overlay machinery (<see cref="OverlayAnchoredPopupBase"/>,
/// <see cref="OverlayPresence"/>). Carries the trigger/anchor element references for
/// positioning and the placement options published by the Positioner part.
/// </summary>
public sealed class PopoverContext : IAnchoredOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public PopoverContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    public string ContentId { get; } = $"navius-popover-{Guid.NewGuid():N}";

    /// <summary>Modal mode (the spec Root <c>modal</c>). When true the popup traps focus and locks scroll.</summary>
    public bool Modal { get; set; }

    /// <summary>The trigger element — set by the trigger after first render, read by the popup to position against.</summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>
    /// Optional explicit anchor element (the spec <c>Popover.Anchor</c>). When set it
    /// replaces the trigger as the positioning reference.
    /// </summary>
    public ElementReference AnchorElement { get; set; }
    public bool HasAnchor { get; set; }

    /// <summary>The element the popup should anchor to: the explicit anchor if present, else the trigger.</summary>
    public ElementReference PositionReference => HasAnchor ? AnchorElement : TriggerElement;

    /// <summary>Custom portal mount-container selector (the spec <c>Portal.container</c>); null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup + backdrop) mounted while closed (the spec <c>Portal.keepMounted</c>).</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by <see cref="NaviusPopoverPositioner"/>.</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    // Title / Description presence (ref-counted) gates the popup's accessible-name wiring.
    private int _titleCount;
    private int _descriptionCount;
    public string TitleId => $"{ContentId}-title";
    public string DescriptionId => $"{ContentId}-desc";
    public bool HasTitle => _titleCount > 0;
    public bool HasDescription => _descriptionCount > 0;
    public void RegisterTitle() => _titleCount++;
    public void UnregisterTitle() => _titleCount = Math.Max(0, _titleCount - 1);
    public void RegisterDescription() => _descriptionCount++;
    public void UnregisterDescription() => _descriptionCount = Math.Max(0, _descriptionCount - 1);

    /// <summary>Published by the Positioner part so the Popup can engage the engine + style the positioning div.</summary>
    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>
    /// Optional arrow element (the spec <c>Popover.Arrow</c>). Registered by the arrow
    /// part so the popup can wire it into the positioner. When the arrow mounts after
    /// the popup engaged, <see cref="ArrowChanged"/> lets the popup re-engage.
    /// </summary>
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

    /// <summary>Raise <see cref="Changed"/> so parts re-render (e.g. after a Title/Description mounts).</summary>
    public Task NotifyChangedAsync() => Changed?.Invoke() ?? Task.CompletedTask;

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
