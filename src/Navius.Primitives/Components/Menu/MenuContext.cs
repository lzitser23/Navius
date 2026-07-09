using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Menu;

/// <summary>
/// Shared state for one menu (the spec Menu). Implements
/// <see cref="IAnchoredOverlayContext"/> so the menu reuses the shared overlay
/// machinery (<see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>):
/// the Trigger is the positioning anchor, the Positioner part publishes placement
/// options, and the Popup engages the engine + dismissable layer (on top of which it
/// adds roving focus, since a menu uses roving — not a focus trap).
/// </summary>
public sealed class MenuContext : IAnchoredOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public MenuContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    public string ContentId { get; } = $"navius-menu-{Guid.NewGuid():N}";

    /// <summary>Stable id of the trigger, referenced by the popup's <c>aria-labelledby</c>.</summary>
    public string TriggerId { get; } = $"navius-menu-trigger-{Guid.NewGuid():N}";

    /// <summary>The trigger element — set after first render, read by the popup to position against.</summary>
    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    /// <summary>A menu has no separate anchor part: the popup anchors to the trigger.</summary>
    public ElementReference PositionReference => TriggerElement;

    /// <summary>Resolved reading direction ("ltr" | "rtl"), set by the root from its Dir param.</summary>
    public string Dir { get; set; } = "ltr";

    public bool IsRtl => Dir == "rtl";

    /// <summary>Root modality (the spec default true). Drives scroll-lock + outside-pointer guard on the Popup.</summary>
    public bool Modal { get; set; } = true;

    /// <summary>Custom portal mount-container selector (the spec <c>Portal.container</c>); null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup) mounted while closed (the spec <c>Portal.keepMounted</c>) for exit animations.</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by <see cref="NaviusMenuPositioner"/>.</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    /// <summary>Published by the Positioner part so the Popup can engage the engine + style the positioning div.</summary>
    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>Optional arrow element (the spec <c>Menu.Arrow</c>), registered by the arrow part.</summary>
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
