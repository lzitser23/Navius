using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Menubar;

/// <summary>
/// Base for menubar parts that live inside a single menu (trigger, content, item) and must
/// re-render when that menu opens or closes. Subscribes to the per-menu
/// <see cref="MenubarMenuContext.Changed"/> event.
/// </summary>
public abstract class MenubarPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected MenubarMenuContext MenuContext { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        MenuContext.Changed += _onChange;
    }

    public void Dispose()
    {
        if (_onChange is not null)
        {
            MenuContext.Changed -= _onChange;
        }
    }
}
