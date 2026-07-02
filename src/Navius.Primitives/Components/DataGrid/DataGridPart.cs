using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.DataGrid;

/// <summary>
/// Base for data-grid parts (in the brain or the helm) that re-render when grid
/// state changes. Consumes the cascaded <see cref="DataGridContext{TItem}"/> and
/// subscribes to its <c>Changed</c> event. A generic base so a styled
/// <c>@typeparam TItem</c> component can <c>@inherits DataGridPart&lt;TItem&gt;</c>.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public abstract class DataGridPart<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] protected DataGridContext<TItem> Context { get; set; } = default!;

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
