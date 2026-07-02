using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Combobox;

/// <summary>Base for combobox parts that re-render when the shared state changes.</summary>
public abstract class ComboboxPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ComboboxContext Context { get; set; } = default!;

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
