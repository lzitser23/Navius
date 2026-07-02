using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.ScrollArea;

/// <summary>
/// Base for scroll-area parts that must re-render when scroll metrics or interaction
/// state change. Subscribes to <see cref="ScrollAreaContext.Changed"/> and re-renders
/// on the UI thread.
/// </summary>
public abstract class ScrollAreaPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected ScrollAreaContext Context { get; set; } = default!;

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
