using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.NavigationMenu;

/// <summary>
/// Base for navigation-menu parts that must re-render when the open value changes.
/// (Mirrors <c>PopoverPart</c> / <c>ToggleGroupPart</c> — subscribes to the context's
/// <c>Changed</c> event and marshals a re-render onto the renderer's sync context.)
/// </summary>
public abstract class NavigationMenuPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected NavigationMenuContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public virtual void Dispose()
    {
        if (_onChange is not null)
        {
            Context.Changed -= _onChange;
        }
    }
}
