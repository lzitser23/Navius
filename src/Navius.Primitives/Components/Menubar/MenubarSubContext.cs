using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Per-submenu state for a <see cref="NaviusMenubarSub"/>. Unlike the per-menu context
/// (whose open state is owned by the menubar root), a submenu owns its OWN open flag so
/// it can nest arbitrarily deep. The <see cref="NaviusMenubarSubTrigger"/> registers its
/// element here for the positioner and toggles <see cref="Open"/>; the
/// <see cref="NaviusMenubarSubContent"/> renders while open, positioned to the side.
/// </summary>
public sealed class MenubarSubContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public MenubarSubContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    /// <summary>True while this submenu is open.</summary>
    public bool Open { get; private set; }

    /// <summary>Stable id wired from sub-trigger (<c>aria-controls</c>) to sub-content.</summary>
    public string ContentId { get; } = $"navius-menubar-sub-content-{Guid.NewGuid():N}";

    /// <summary>Stable id of the sub-trigger, referenced by sub-content's <c>aria-labelledby</c>.</summary>
    public string TriggerId { get; } = $"navius-menubar-sub-trigger-{Guid.NewGuid():N}";

    /// <summary>The sub-trigger element, used as the positioner reference.</summary>
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

    public Task RequestToggleAsync() => _requestSetOpen(!Open);
}
