using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.MessageScroller;

/// <summary>Base for message-scroller parts that re-render when the shared state changes.</summary>
public abstract class MessageScrollerPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected MessageScrollerContext Context { get; set; } = default!;

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
