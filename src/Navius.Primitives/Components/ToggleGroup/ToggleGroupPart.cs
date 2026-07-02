using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ToggleGroup;

/// <summary>Base for toggle-group parts that re-render when the pressed set changes.</summary>
public abstract class ToggleGroupPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ToggleGroupContext Context { get; set; } = default!;

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
