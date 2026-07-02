using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Accordion;

/// <summary>Base for accordion parts that re-render when an item opens/closes.</summary>
public abstract class AccordionPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected AccordionContext Context { get; set; } = default!;

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
