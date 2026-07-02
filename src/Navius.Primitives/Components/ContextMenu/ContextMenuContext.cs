using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.ContextMenu;

/// <summary>
/// Shared state for one context menu. Implements <see cref="IAnchoredOverlayContext"/>
/// so the menu reuses the shared overlay machinery
/// (<see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>). Unlike a
/// menu — whose anchor is the trigger element — a context menu is anchored to a POINT:
/// the pointer coordinates captured on right-click. The Popup renders a 0x0 fixed anchor
/// div at <see cref="AnchorX"/>/<see cref="AnchorY"/> and sets it as
/// <see cref="AnchorElement"/> (the <see cref="PositionReference"/>) each time it opens.
/// </summary>
public sealed class ContextMenuContext : IAnchoredOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public ContextMenuContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    public string ContentId { get; } = $"navius-context-menu-{Guid.NewGuid():N}";

    /// <summary>Viewport (clientX) coordinate of the last right-click. The anchor sits here.</summary>
    public double AnchorX { get; private set; }

    /// <summary>Viewport (clientY) coordinate of the last right-click. The anchor sits here.</summary>
    public double AnchorY { get; private set; }

    /// <summary>The 0x0 anchor element placed at (<see cref="AnchorX"/>, <see cref="AnchorY"/>). The popup positions against this.</summary>
    public ElementReference AnchorElement { get; private set; }

    /// <summary>The cursor point IS the position reference — not the trigger.</summary>
    public ElementReference PositionReference => AnchorElement;

    /// <summary>Set by the Popup each open (the popup owns the 0x0 anchor div rendered at the cursor point).</summary>
    public void SetAnchorElement(ElementReference el) => AnchorElement = el;

    /// <summary>True once a Trigger has registered the focus-return target (so focus returns there on close).</summary>
    public bool HasTrigger { get; set; }

    /// <summary>The trigger area — focus returns here when the menu closes.</summary>
    public ElementReference TriggerElement { get; set; }

    /// <summary>Root modality (the spec default true). Drives scroll-lock + outside-pointer guard on the Popup.</summary>
    public bool Modal { get; set; } = true;

    /// <summary>Resolved reading direction ("ltr" | "rtl"), set by the root from its Dir param.</summary>
    public string Dir { get; set; } = "ltr";

    public bool IsRtl => Dir == "rtl";

    /// <summary>Custom portal mount-container selector (the spec <c>Portal.container</c>); null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup) mounted while closed (the spec <c>Portal.keepMounted</c>) for exit animations.</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by <see cref="NaviusContextMenuPositioner"/>.</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    /// <summary>Published by the Positioner part so the Popup can engage the engine + style the positioning div.</summary>
    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>Optional arrow element (the spec <c>ContextMenu.Arrow</c>), registered by the arrow part.</summary>
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

    /// <summary>Record the pointer position and notify subscribers so the anchor re-renders before opening.</summary>
    internal async Task SetAnchorAsync(double x, double y)
    {
        AnchorX = x;
        AnchorY = y;

        if (Changed is not null)
        {
            await Changed.Invoke();
        }
    }

    public Task RequestSetAsync(bool open) => _requestSetOpen(open);

    public Task RequestCloseAsync() => _requestSetOpen(false);

    /// <summary>Open the menu at the given pointer coordinates (captured from oncontextmenu).</summary>
    public async Task RequestOpenAtAsync(double x, double y)
    {
        await SetAnchorAsync(x, y);
        await _requestSetOpen(true);
    }
}
