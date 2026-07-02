using Microsoft.AspNetCore.Components;
using Navius.Primitives.Components.Overlays;

namespace Navius.Primitives.Components.Drawer;

/// <summary>
/// Shared state for one drawer (Base UI <c>Drawer</c> — a Dialog docked to an edge with
/// drag-to-dismiss). Cascaded from <see cref="NaviusDrawer"/> to its parts. Implements
/// <see cref="IOverlayContext"/> so it reuses the shared overlay machinery
/// (<see cref="OverlayPopupBase"/>). <see cref="Side"/> is the docked edge / drag
/// direction. Snap points, the swipe-area edge, indent/background-scale, multi-drawer
/// nesting and the virtual-keyboard provider are deferred (see docs/base-ui-parity.md).
/// </summary>
public sealed class DrawerContext : IOverlayContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public DrawerContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    /// <summary>Modal (default true): traps focus + locks scroll while open.</summary>
    public bool Modal { get; set; } = true;

    /// <summary>The edge the sheet docks to and is dragged toward: bottom | top | left | right.</summary>
    public string Side { get; set; } = "bottom";

    public string ContentId { get; } = $"navius-drawer-{Guid.NewGuid():N}";
    public string TitleId => $"{ContentId}-title";
    public string DescriptionId => $"{ContentId}-desc";

    public ElementReference TriggerElement { get; set; }
    public bool HasTrigger { get; set; }

    private int _titleCount;
    private int _descriptionCount;
    public bool HasTitle => _titleCount > 0;
    public bool HasDescription => _descriptionCount > 0;
    public void RegisterTitle() => _titleCount++;
    public void UnregisterTitle() { if (_titleCount > 0) _titleCount--; }
    public void RegisterDescription() => _descriptionCount++;
    public void UnregisterDescription() { if (_descriptionCount > 0) _descriptionCount--; }

    public string? PortalContainer { get; set; }
    public bool? PortalForceMount { get; set; }

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
