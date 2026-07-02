using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Select;

/// <summary>Base for select parts that re-render when open state or value changes.</summary>
public abstract class SelectPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected SelectContext Context { get; set; } = default!;

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

        GC.SuppressFinalize(this);
    }
}
