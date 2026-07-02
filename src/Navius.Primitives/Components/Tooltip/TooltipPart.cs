using Microsoft.AspNetCore.Components;

namespace Navius.Primitives.Components.Tooltip;

/// <summary>Base for tooltip parts that re-render when the tooltip opens/closes.</summary>
public abstract class TooltipPart : ComponentBase, IDisposable
{
    [CascadingParameter] protected TooltipContext Context { get; set; } = default!;

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
    }
}
