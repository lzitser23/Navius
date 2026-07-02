using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Toolbar;

/// <summary>Base for toolbar parts that re-render when the seated Tab stop changes.</summary>
public abstract class ToolbarPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ToolbarContext Context { get; set; } = default!;

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
