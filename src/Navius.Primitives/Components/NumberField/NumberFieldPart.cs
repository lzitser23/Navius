using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.NumberField;

/// <summary>
/// Base for number-field parts (group, input, increment, decrement) that re-render
/// when the field's value or disabled state changes. Subscribes to
/// <see cref="NumberFieldContext.Changed"/> on init and detaches on dispose.
/// </summary>
public abstract class NumberFieldPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected NumberFieldContext Context { get; set; } = default!;

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
