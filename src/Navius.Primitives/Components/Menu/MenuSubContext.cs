using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Menu;

/// <summary>
/// State for one nested submenu (the spec Sub). Independent open state and anchor from
/// the parent menu: opening/closing a submenu does not close the root. SubTrigger is
/// the anchor; SubContent is the floating roving submenu.
/// </summary>
public sealed class MenuSubContext
{
    private readonly Func<bool, Task> _requestSetOpen;

    public MenuSubContext(Func<bool, Task> requestSetOpen) => _requestSetOpen = requestSetOpen;

    public bool Open { get; private set; }

    public string ContentId { get; } = $"navius-submenu-{Guid.NewGuid():N}";

    /// <summary>Stable id of the SubTrigger, referenced by the SubContent's <c>aria-labelledby</c>.</summary>
    public string TriggerId { get; } = $"navius-submenu-trigger-{Guid.NewGuid():N}";

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

    public Task RequestCloseAsync() => _requestSetOpen(false);

    public Task RequestToggleAsync() => _requestSetOpen(!Open);
}
