using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;
using Navius.Primitives.Interop;

namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Per-menu state for one <see cref="NaviusMenubarMenu"/>. Mirrors the menu
/// context (open flag + anchor element + stable ids) but derives its open state from the
/// menubar root's <see cref="MenubarContext.OpenValue"/>, so opening one menu closes the
/// rest. The trigger registers its <see cref="ElementReference"/> here for the positioner.
/// Implements <see cref="IAnchoredOverlayContext"/> so the menu reuses the shared overlay
/// machinery (<see cref="OverlayAnchoredPopupBase"/>, <see cref="OverlayPresence"/>): the
/// Trigger is the positioning anchor, the Positioner part publishes placement options, and
/// the Popup engages the engine + dismissable layer (on top of which it layers roving focus).
/// </summary>
public sealed class MenubarMenuContext : IAnchoredOverlayContext
{
    private readonly MenubarContext _root;

    public MenubarMenuContext(MenubarContext root, string value)
    {
        _root = root;
        Value = value;
    }

    /// <summary>The identity of this menu within the menubar.</summary>
    public string Value { get; }

    /// <summary>True when this menu is the open one.</summary>
    public bool Open => _root.IsOpen(Value);

    /// <summary>Stable id wired from trigger (<c>aria-controls</c>) to content.</summary>
    public string ContentId { get; } = $"navius-menubar-menu-{Guid.NewGuid():N}";

    /// <summary>Stable id of the trigger, referenced by content's <c>aria-labelledby</c>.</summary>
    public string TriggerId { get; } = $"navius-menubar-trigger-{Guid.NewGuid():N}";

    /// <summary>The trigger element, used as the positioner reference.</summary>
    public ElementReference TriggerElement { get; set; }

    public bool HasTrigger { get; set; }

    /// <summary>A menubar menu has no separate anchor part: the popup anchors to the trigger.</summary>
    public ElementReference PositionReference => TriggerElement;

    /// <summary>Root modality (the spec default true; overridable via <c>NaviusMenubar.Modal</c>).
    /// Drives scroll-lock + outside-pointer guard on the Popup.</summary>
    public bool Modal => _root.Modal;

    /// <summary>Custom portal mount-container selector (the spec <c>Portal.container</c>); null = document.body.</summary>
    public string? PortalContainer { get; set; }

    /// <summary>Keep the portal (popup) mounted while closed (the spec <c>Portal.keepMounted</c>) for exit animations.</summary>
    public bool PortalKeepMounted { get; set; }

    /// <summary>Placement options collected by <see cref="NaviusMenubarPositioner"/>.</summary>
    public PositionOptions Options { get; private set; } = new(SideOffset: 0);

    /// <summary>Unmatched attributes (e.g. <c>class</c>) the consumer set on the Positioner part.</summary>
    public IDictionary<string, object>? PositionerAttributes { get; private set; }

    /// <summary>Published by the Positioner part so the Popup can engage the engine + style the positioning div.</summary>
    public void SetPositioner(PositionOptions options, IDictionary<string, object>? attributes)
    {
        Options = options;
        PositionerAttributes = attributes;
    }

    /// <summary>The optional arrow element registered by <c>NaviusMenubarArrow</c>; the positioner aligns it.</summary>
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

    /// <summary>Re-fires whenever the root's open value changes, so this menu's parts re-render.</summary>
    public event Func<Task>? Changed;

    internal Task RaiseChangedAsync() => Changed is not null ? Changed.Invoke() : Task.CompletedTask;

    /// <summary>Resolved menubar reading direction, for flipping ArrowLeft/Right adjacent navigation.</summary>
    public string Dir => _root.Dir;

    /// <summary>
    /// Set by a SubTrigger when its open key (ArrowRight in ltr) opens a submenu, so the bubbling
    /// keydown does NOT also move the Content to the adjacent top-level menu. Content reads and
    /// clears it once.
    /// </summary>
    public bool SuppressAdjacentOnce { get; set; }

    /// <summary>Atomically read-and-clear <see cref="SuppressAdjacentOnce"/>.</summary>
    public bool ConsumeSuppressAdjacent()
    {
        if (SuppressAdjacentOnce)
        {
            SuppressAdjacentOnce = false;
            return true;
        }
        return false;
    }

    public Task RequestOpenAsync() => _root.RequestOpenAsync(Value);

    public Task RequestCloseAsync() => _root.RequestCloseAsync(Value);

    public Task RequestToggleAsync() => _root.RequestToggleAsync(Value);

    /// <summary>Open the menu adjacent to this one (+1 next / -1 prev), honouring loop.</summary>
    public Task MoveToAdjacentAsync(int direction) => _root.MoveToAdjacentAsync(direction);
}
