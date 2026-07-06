using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Rating;

/// <summary>Base for rating parts that re-render when the value or hover preview changes.</summary>
public abstract class RatingPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected RatingContext Context { get; set; } = default!;

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

    /// <summary>Override to release part-specific resources.</summary>
    protected virtual void OnDispose()
    {
    }
}
