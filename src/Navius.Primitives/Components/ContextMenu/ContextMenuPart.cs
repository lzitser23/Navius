using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ContextMenu;

/// <summary>Base for context-menu parts that re-render when the menu opens/closes or the anchor moves.</summary>
public abstract class ContextMenuPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ContextMenuContext Context { get; set; } = default!;

    private Func<Task>? _onChange;

    protected override void OnInitialized()
    {
        _onChange = () => InvokeAsync(StateHasChanged);
        Context.Changed += _onChange;
    }

    public void Dispose()
    {
        if (_onChange is not null)
        {
            Context.Changed -= _onChange;
        }
    }
}
