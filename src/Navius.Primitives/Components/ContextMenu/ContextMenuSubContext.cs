using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ContextMenu;

/// <summary>
/// Owns the open state of one nested submenu (Sub/SubTrigger/SubContent). Anchored to
/// its SubTrigger element (unlike the root context menu, which is point-anchored).
/// Mirrors the spec ContextMenu.Sub.
/// </summary>
public sealed class ContextMenuSubContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public ContextMenuSubContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    public string ContentId { get; } = $"navius-context-menu-sub-{Guid.NewGuid():N}";

    public string TriggerId { get; } = $"navius-context-menu-subtrigger-{Guid.NewGuid():N}";

    /// <summary>The SubTrigger element — the positioner anchor and focus-return target.</summary>
    public ElementReference TriggerElement { get; set; }

    public bool HasTrigger { get; set; }

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

    public Task RequestOpenAsync() => _requestSetOpen(true);

    public Task RequestCloseAsync() => _requestSetOpen(false);
}
