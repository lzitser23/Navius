using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.RadioGroup;

/// <summary>Base for radio-group parts that re-render when the selected value changes.</summary>
public abstract class RadioGroupPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected RadioGroupContext Context { get; set; } = default!;

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

        OnDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to release part-specific resources (e.g. context registration).</summary>
    protected virtual void OnDispose()
    {
    }
}
