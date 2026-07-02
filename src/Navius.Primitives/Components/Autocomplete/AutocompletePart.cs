using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Autocomplete;

/// <summary>Base for autocomplete parts that re-render when the shared state changes.</summary>
public abstract class AutocompletePart : ComponentBase, IDisposable
{
    [CascadingParameter] protected AutocompleteContext Context { get; set; } = default!;

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
